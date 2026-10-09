using KadreeBank.Api.Exceptions;

namespace KadreeBank.Api.Models.Entities
{
    /// <summary>
    /// Cuenta bancaria. Es la única que modifica el saldo y garantiza que nunca sea negativo.
    /// La consistencia ante concurrencia NO se resuelve aquí: el DAO entrega la cuenta
    /// con bloqueo exclusivo de fila antes de operar (ver AccountDao y el README).
    /// </summary>
    public class Account
    {
        /// <summary>Tope por operación. Mantiene los cálculos lejos del límite de NUMERIC(18,2).</summary>
        public const decimal MaxOperationAmount = 1_000_000_000_000m;

        /// <summary>Identificador interno (columna identity).</summary>
        public long Id { get; private set; }

        /// <summary>Número de cuenta visible para el cliente, por ejemplo <c>AH-1001</c>. Es único.</summary>
        public string Number { get; private set; } = null!;

        /// <summary>Ahorros o corriente.</summary>
        public AccountType Type { get; private set; }

        /// <summary>Id del titular.</summary>
        public long CustomerId { get; private set; }

        /// <summary>
        /// Tipo del titular, copiado en la cuenta: la base lo usa con una FK compuesta y un CHECK
        /// para exigir ahorros ↔ persona natural y corriente ↔ empresa.
        /// </summary>
        public CustomerType CustomerType { get; private set; }

        /// <summary>Id de la ciudad donde se abrió la cuenta.</summary>
        public int OriginCityId { get; private set; }

        /// <summary>Saldo actual. Nunca es negativo; solo cambia con <see cref="Deposit"/> y <see cref="Withdraw"/>.</summary>
        public decimal Balance { get; private set; }

        /// <summary>Fecha de apertura en UTC.</summary>
        public DateTime OpenedAtUtc { get; private set; }

        /// <summary>Titular de la cuenta (navegación de EF Core).</summary>
        public Customer Customer { get; private set; } = null!;

        /// <summary>Ciudad de origen (navegación de EF Core).</summary>
        public City OriginCity { get; private set; } = null!;

        /// <summary>Movimientos de la cuenta (navegación de EF Core; se usa en los reportes).</summary>
        public ICollection<Movement> Movements { get; private set; } = new List<Movement>();

        /// <summary>Constructor para Entity Framework al materializar desde la base de datos.</summary>
        private Account() { }

        /// <summary>Crea una cuenta validando las reglas de apertura.</summary>
        /// <param name="id">Id de la cuenta.</param>
        /// <param name="number">Número de cuenta, por ejemplo <c>AH-1001</c>.</param>
        /// <param name="type">Tipo de cuenta; debe corresponder al tipo de cliente.</param>
        /// <param name="customerId">Id del titular.</param>
        /// <param name="customerType">Tipo del titular (persona natural o empresa).</param>
        /// <param name="originCityId">Ciudad de origen de la cuenta; se usa en el reporte de retiros fuera de la ciudad.</param>
        /// <param name="balance">Saldo inicial; no puede ser negativo.</param>
        /// <param name="openedAtUtc">Fecha de apertura en UTC.</param>
        /// <exception cref="AccountTypeNotAllowedException">Ahorros para empresa o corriente para persona natural.</exception>
        /// <exception cref="InvalidAmountException">Saldo inicial negativo.</exception>
        public Account(long id, string number, AccountType type, long customerId, CustomerType customerType,
            int originCityId, decimal balance, DateTime openedAtUtc)
        {
            if (Customer.AllowedAccountType(customerType) != type)
                throw new AccountTypeNotAllowedException(customerType, type);
            if (balance < 0)
                throw new InvalidAmountException("El saldo inicial no puede ser negativo.");

            Id = id;
            Number = number;
            Type = type;
            CustomerId = customerId;
            CustomerType = customerType;
            OriginCityId = originCityId;
            Balance = balance;
            OpenedAtUtc = openedAtUtc;
        }

        /// <summary>Suma <paramref name="amount"/> al saldo y crea el movimiento de consignación.</summary>
        /// <param name="amount">Monto a consignar: mayor que cero, máximo dos decimales y hasta <see cref="MaxOperationAmount"/>.</param>
        /// <param name="cityId">Ciudad donde se hace la consignación.</param>
        /// <param name="nowUtc">Fecha y hora de la operación en UTC.</param>
        /// <param name="description">Texto libre opcional.</param>
        /// <param name="userId">Usuario que hace la operación (auditoría).</param>
        /// <returns>El movimiento con el saldo resultante; hay que agregarlo con <c>IAccountDao.AddMovement</c>.</returns>
        /// <exception cref="InvalidAmountException">El monto no es válido. El saldo no cambia.</exception>
        public Movement Deposit(decimal amount, int cityId, DateTime nowUtc, string? description = null, long? userId = null)
        {
            EnsureValidAmount(amount);
            Balance += amount;
            return new Movement(Id, MovementType.Consignacion, amount, cityId, Balance, description, nowUtc, userId);
        }

        /// <summary>
        /// Resta <paramref name="amount"/> del saldo y crea el movimiento de retiro.
        /// Aquí se aplica la regla principal del banco: <b>la cuenta nunca queda con saldo negativo</b>.
        /// </summary>
        /// <param name="amount">Monto a retirar: mayor que cero, máximo dos decimales y no mayor que el saldo.</param>
        /// <param name="cityId">Ciudad donde se hace el retiro (se compara con la de origen en el reporte 2).</param>
        /// <param name="nowUtc">Fecha y hora de la operación en UTC.</param>
        /// <param name="description">Texto libre opcional.</param>
        /// <returns>El movimiento con el saldo resultante; hay que agregarlo con <c>IAccountDao.AddMovement</c>.</returns>
        /// <exception cref="InvalidAmountException">El monto no es válido. El saldo no cambia.</exception>
        /// <param name="userId">Usuario que hace la operación (auditoría).</param>
        /// <exception cref="InsufficientFundsException">El monto supera el saldo. El saldo no cambia.</exception>
        public Movement Withdraw(decimal amount, int cityId, DateTime nowUtc, string? description = null, long? userId = null)
        {
            EnsureValidAmount(amount);
            if (amount > Balance)
                throw new InsufficientFundsException(Number, Balance, amount);

            Balance -= amount;
            return new Movement(Id, MovementType.Retiro, amount, cityId, Balance, description, nowUtc, userId);
        }

        /// <summary>Valida que el monto sea positivo, con máximo dos decimales y dentro del tope por operación.</summary>
        /// <param name="amount">Monto a validar.</param>
        /// <exception cref="InvalidAmountException">El monto no cumple alguna de las condiciones.</exception>
        private static void EnsureValidAmount(decimal amount)
        {
            if (amount <= 0)
                throw new InvalidAmountException("El monto debe ser mayor que cero.");
            if (amount > MaxOperationAmount)
                throw new InvalidAmountException($"El monto no puede superar {MaxOperationAmount:N0} por operación.");
            if (decimal.Round(amount, 2) != amount)
                throw new InvalidAmountException("El monto admite como máximo dos decimales.");
        }
    }
}
