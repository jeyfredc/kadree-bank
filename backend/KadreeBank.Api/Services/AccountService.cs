using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Services
{
    /// <summary>
    /// Implementa los casos de uso de las cuentas. Valida lo que depende de datos (que la cuenta
    /// y la ciudad existan, el rango de meses), coordina la transacción con <see cref="IAccountDao"/>
    /// y deja las reglas del dinero a la entidad <see cref="Account"/>.
    /// </summary>
    public class AccountService : IAccountService
    {
        /// <summary>Movimientos que se devuelven cuando no se indica un límite.</summary>
        public const int DefaultMovementLimit = 20;

        /// <summary>Máximo de movimientos por consulta, para no devolver históricos completos.</summary>
        public const int MaxMovementLimit = 100;

        private readonly IAccountDao _accountDao;
        private readonly ICityDao _cityDao;
        private readonly BankCalendar _calendar;
        private readonly TimeProvider _time;
        private readonly ICurrentUser _currentUser;

        /// <summary>Crea el servicio con sus dependencias (inyectadas por DI).</summary>
        /// <param name="accountDao">Acceso a datos de cuentas y movimientos.</param>
        /// <param name="cityDao">Acceso a datos de ciudades, para validar y nombrar la ciudad de la operación.</param>
        /// <param name="calendar">Convierte meses de la zona horaria del banco a rangos UTC.</param>
        /// <param name="time">Reloj del sistema; en pruebas se reemplaza por uno fijo.</param>
        /// <param name="currentUser">Usuario de la petición: define qué cuentas puede ver y operar.</param>
        public AccountService(IAccountDao accountDao, ICityDao cityDao, BankCalendar calendar, TimeProvider time, ICurrentUser currentUser)
        {
            _accountDao = accountDao ?? throw new ArgumentNullException(nameof(accountDao));
            _cityDao = cityDao ?? throw new ArgumentNullException(nameof(cityDao));
            _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        /// <summary>
        /// Filtro de propiedad: el personal del banco ve todas las cuentas (null); un cliente, solo las suyas.
        /// Un usuario sin cliente ni rol Banco no ve ninguna (-1 no coincide con ningún cliente).
        /// </summary>
        private long? OwnerFilter => _currentUser.IsBanco ? null : _currentUser.CustomerId ?? -1;

        /// <inheritdoc/>
        public async Task<ApiResponse<List<AccountSummaryDto>>> GetAccounts(CancellationToken ct)
        {
            var accounts = await _accountDao.GetAllAsync(OwnerFilter, ct);
            return ApiResponse<List<AccountSummaryDto>>.Ok(accounts, "Cuentas obtenidas exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<AccountSummaryDto>> GetAccount(string accountNumber, CancellationToken ct)
        {
            var account = await FindAccount(accountNumber, ct);
            return ApiResponse<AccountSummaryDto>.Ok(account, "Cuenta obtenida exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<List<MovementDto>>> GetRecentMovements(string accountNumber, int? limit, CancellationToken ct)
        {
            var take = limit ?? DefaultMovementLimit;
            if (take is < 1 or > MaxMovementLimit)
                throw new RequestValidationException("limit", $"El límite debe estar entre 1 y {MaxMovementLimit}.");

            await FindAccount(accountNumber, ct);
            var movements = await _accountDao.GetRecentMovementsAsync(accountNumber, take, ct);
            return ApiResponse<List<MovementDto>>.Ok(movements, "Movimientos obtenidos exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<StatementDto>> GetMonthlyStatement(string accountNumber, int year, int month, CancellationToken ct)
        {
            _calendar.ValidateMonth(year, month);
            await FindAccount(accountNumber, ct);

            var (fromUtc, toUtc) = _calendar.MonthRangeUtc(year, month);
            var opening = await _accountDao.GetBalanceBeforeAsync(accountNumber, fromUtc, ct);
            var movements = await _accountDao.GetMovementsInRangeAsync(accountNumber, fromUtc, toUtc, ct);

            var statement = new StatementDto
            {
                AccountNumber = accountNumber,
                Year = year,
                Month = month,
                PeriodStartUtc = fromUtc,
                PeriodEndUtc = toUtc,
                OpeningBalance = opening,
                TotalDeposits = movements.Where(m => m.Type == MovementType.Consignacion).Sum(m => m.Amount),
                TotalWithdrawals = movements.Where(m => m.Type == MovementType.Retiro).Sum(m => m.Amount),
                ClosingBalance = movements.Count > 0 ? movements[^1].BalanceAfter : opening,
                Movements = movements,
            };
            return ApiResponse<StatementDto>.Ok(statement, "Extracto generado exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<OperationResultDto>> Deposit(string accountNumber, OperationRequestDto request, CancellationToken ct)
        {
            var result = await ExecuteOperation(accountNumber, request, MovementType.Consignacion, ct);
            return ApiResponse<OperationResultDto>.Ok(result, "Consignación realizada exitosamente");
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<OperationResultDto>> Withdraw(string accountNumber, OperationRequestDto request, CancellationToken ct)
        {
            var result = await ExecuteOperation(accountNumber, request, MovementType.Retiro, ct);
            return ApiResponse<OperationResultDto>.Ok(result, "Retiro realizado exitosamente");
        }

        /// <summary>
        /// Lógica común de consignaciones y retiros: valida la ciudad, bloquea la cuenta, aplica la
        /// operación en la entidad, registra el movimiento y confirma, todo en una sola transacción.
        /// </summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="request">Monto, ciudad y descripción de la operación.</param>
        /// <param name="type">Consignación o retiro.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Nuevo saldo y el movimiento ya guardado (con su Id).</returns>
        private async Task<OperationResultDto> ExecuteOperation(
            string accountNumber, OperationRequestDto request, MovementType type, CancellationToken ct)
        {
            // Solo el titular opera su cuenta. El personal del banco consulta, pero no mueve dinero ajeno.
            if (_currentUser.IsBanco || _currentUser.CustomerId is null)
                throw new ForbiddenException("Solo el titular de la cuenta puede consignar o retirar desde la banca en línea.");

            var cityName = await _cityDao.GetNameAsync(request.CityId, ct)
                ?? throw new RequestValidationException("cityId", "La ciudad indicada no existe.");

            // Patrón: bloquear -> leer -> validar regla -> escribir -> confirmar, todo en una transacción.
            // El bloqueo de fila serializa las operaciones sobre la MISMA cuenta; cuentas distintas no se esperan.
            var (account, movement) = await _accountDao.ExecuteInTransactionAsync(async innerCt =>
            {
                var account = await _accountDao.GetByNumberForUpdateAsync(accountNumber, innerCt);
                // Una cuenta ajena responde igual que una inexistente: no revelamos que existe.
                if (account is null || account.CustomerId != _currentUser.CustomerId)
                    throw new NotFoundException($"La cuenta {accountNumber} no existe.");

                var now = _time.GetUtcNow().UtcDateTime;
                var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
                var movement = type == MovementType.Consignacion
                    ? account.Deposit(request.Amount, request.CityId, now, description, _currentUser.UserId)
                    : account.Withdraw(request.Amount, request.CityId, now, description, _currentUser.UserId);

                _accountDao.AddMovement(movement);
                return (account, movement);
            }, ct);

            // Tras el commit el movimiento ya tiene el Id generado por la base de datos.
            return new OperationResultDto
            {
                AccountNumber = account.Number,
                Balance = account.Balance,
                Movement = new MovementDto
                {
                    Id = movement.Id,
                    Type = movement.Type,
                    Amount = movement.Amount,
                    City = cityName,
                    BalanceAfter = movement.BalanceAfter,
                    Description = movement.Description,
                    CreatedAtUtc = movement.CreatedAtUtc,
                },
            };
        }

        /// <summary>
        /// Obtiene el resumen de la cuenta o lanza <see cref="NotFoundException"/> si no existe
        /// o si no es del cliente en sesión.
        /// </summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El resumen de la cuenta.</returns>
        private async Task<AccountSummaryDto> FindAccount(string accountNumber, CancellationToken ct) =>
            await _accountDao.GetSummaryAsync(accountNumber, OwnerFilter, ct)
            ?? throw new NotFoundException($"La cuenta {accountNumber} no existe.");
    }
}
