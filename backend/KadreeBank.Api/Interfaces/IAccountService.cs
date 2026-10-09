using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.DTOs.Responses;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>
    /// Casos de uso de las cuentas: consultar saldo, movimientos y extractos, y realizar
    /// consignaciones y retiros. Todas las respuestas exitosas vienen en un <see cref="ApiResponse{T}"/>;
    /// los errores se lanzan como excepciones que <c>ApiExceptionHandler</c> convierte en la respuesta HTTP.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>Lista todas las cuentas con su titular, ciudad de origen y saldo actual.</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Cuentas ordenadas por número.</returns>
        Task<ApiResponse<List<AccountSummaryDto>>> GetAccounts(CancellationToken ct);

        /// <summary>Consulta el saldo y los datos de una cuenta.</summary>
        /// <param name="accountNumber">Número de cuenta, por ejemplo <c>AH-1001</c>.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Resumen de la cuenta con su saldo actual.</returns>
        /// <exception cref="NotFoundException">La cuenta no existe.</exception>
        Task<ApiResponse<AccountSummaryDto>> GetAccount(string accountNumber, CancellationToken ct);

        /// <summary>Obtiene los movimientos más recientes de una cuenta, del más nuevo al más antiguo.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="limit">Cantidad de movimientos (1 a 100). Si es null se usan 20.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Movimientos con su ciudad y el saldo resultante de cada uno.</returns>
        /// <exception cref="RequestValidationException">El límite está fuera del rango 1-100.</exception>
        /// <exception cref="NotFoundException">La cuenta no existe.</exception>
        Task<ApiResponse<List<MovementDto>>> GetRecentMovements(string accountNumber, int? limit, CancellationToken ct);

        /// <summary>
        /// Genera el extracto de un mes: saldo inicial, movimientos del mes en orden cronológico,
        /// total de consignaciones, total de retiros y saldo final.
        /// </summary>
        /// <remarks>
        /// El mes se delimita en la zona horaria del banco (America/Bogota por defecto), no en UTC.
        /// El saldo inicial es el saldo resultante del último movimiento anterior al mes (0 si no hay).
        /// </remarks>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="year">Año (2000-2100).</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El extracto del mes.</returns>
        /// <exception cref="RequestValidationException">Año o mes inválido, o mes futuro.</exception>
        /// <exception cref="NotFoundException">La cuenta no existe.</exception>
        Task<ApiResponse<StatementDto>> GetMonthlyStatement(string accountNumber, int year, int month, CancellationToken ct);

        /// <summary>Consigna dinero en una cuenta y registra el movimiento.</summary>
        /// <remarks>
        /// Se ejecuta en una transacción con bloqueo exclusivo de la cuenta, así que es seguro
        /// ante operaciones simultáneas sobre la misma cuenta.
        /// </remarks>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="request">Monto, ciudad donde se hace la consignación y descripción opcional.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Nuevo saldo y el movimiento registrado (con su Id).</returns>
        /// <exception cref="RequestValidationException">La ciudad no existe.</exception>
        /// <exception cref="NotFoundException">La cuenta no existe.</exception>
        /// <exception cref="InvalidAmountException">El monto no es válido.</exception>
        /// <exception cref="ConcurrencyConflictException">Otra operación retuvo la cuenta más de 5 segundos; se puede reintentar.</exception>
        Task<ApiResponse<OperationResultDto>> Deposit(string accountNumber, OperationRequestDto request, CancellationToken ct);

        /// <summary>Retira dinero de una cuenta y registra el movimiento.</summary>
        /// <remarks>
        /// Se ejecuta en una transacción con bloqueo exclusivo de la cuenta: aunque lleguen varios
        /// retiros a la vez, la cuenta nunca queda con saldo negativo.
        /// </remarks>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <param name="request">Monto, ciudad donde se hace el retiro y descripción opcional.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Nuevo saldo y el movimiento registrado (con su Id).</returns>
        /// <exception cref="RequestValidationException">La ciudad no existe.</exception>
        /// <exception cref="NotFoundException">La cuenta no existe.</exception>
        /// <exception cref="InvalidAmountException">El monto no es válido.</exception>
        /// <exception cref="InsufficientFundsException">El retiro dejaría la cuenta con saldo negativo.</exception>
        /// <exception cref="ConcurrencyConflictException">Otra operación retuvo la cuenta más de 5 segundos; se puede reintentar.</exception>
        Task<ApiResponse<OperationResultDto>> Withdraw(string accountNumber, OperationRequestDto request, CancellationToken ct);
    }
}
