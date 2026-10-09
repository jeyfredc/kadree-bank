namespace KadreeBank.Api.Models.Entities
{
    /// <summary>
    /// Movimiento inmutable (consignación o retiro). Guarda el saldo resultante
    /// para poder generar extractos sin recalcular el histórico.
    /// Solo se crea desde <see cref="Account.Deposit"/> o <see cref="Account.Withdraw"/>.
    /// </summary>
    public class Movement
    {
        /// <summary>Identificador; lo genera la base de datos al guardar.</summary>
        public long Id { get; private set; }

        /// <summary>Id de la cuenta afectada.</summary>
        public long AccountId { get; private set; }

        /// <summary>Consignación o retiro.</summary>
        public MovementType Type { get; private set; }

        /// <summary>Monto de la operación (siempre positivo; el tipo indica si suma o resta).</summary>
        public decimal Amount { get; private set; }

        /// <summary>Id de la ciudad donde se hizo la operación.</summary>
        public int CityId { get; private set; }

        /// <summary>Saldo de la cuenta inmediatamente después de este movimiento.</summary>
        public decimal BalanceAfter { get; private set; }

        /// <summary>Texto libre opcional.</summary>
        public string? Description { get; private set; }

        /// <summary>Fecha y hora de la operación en UTC.</summary>
        public DateTime CreatedAtUtc { get; private set; }

        /// <summary>Usuario que hizo la operación (auditoría). Null en movimientos históricos de prueba.</summary>
        public long? CreatedByUserId { get; private set; }

        /// <summary>Cuenta afectada (navegación de EF Core).</summary>
        public Account Account { get; private set; } = null!;

        /// <summary>Ciudad de la operación (navegación de EF Core).</summary>
        public City City { get; private set; } = null!;

        /// <summary>Constructor para Entity Framework al materializar desde la base de datos.</summary>
        private Movement() { }

        /// <summary>Crea el movimiento. Es interno: solo <see cref="Account"/> puede crear movimientos.</summary>
        /// <param name="accountId">Cuenta afectada.</param>
        /// <param name="type">Consignación o retiro.</param>
        /// <param name="amount">Monto de la operación (siempre positivo).</param>
        /// <param name="cityId">Ciudad donde se hizo la operación.</param>
        /// <param name="balanceAfter">Saldo de la cuenta después de la operación.</param>
        /// <param name="description">Texto libre opcional.</param>
        /// <param name="createdAtUtc">Fecha y hora en UTC.</param>
        /// <param name="createdByUserId">Usuario que hizo la operación.</param>
        internal Movement(long accountId, MovementType type, decimal amount, int cityId,
            decimal balanceAfter, string? description, DateTime createdAtUtc, long? createdByUserId)
        {
            CreatedByUserId = createdByUserId;
            AccountId = accountId;
            Type = type;
            Amount = amount;
            CityId = cityId;
            BalanceAfter = balanceAfter;
            Description = description;
            CreatedAtUtc = createdAtUtc;
        }
    }
}
