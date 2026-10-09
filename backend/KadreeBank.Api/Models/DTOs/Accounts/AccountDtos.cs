using System.ComponentModel.DataAnnotations;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Models.DTOs.Accounts
{
    /// <summary>Datos de una cuenta con su saldo actual.</summary>
    public class AccountSummaryDto
    {
        /// <summary>Número de cuenta, por ejemplo <c>AH-1001</c>.</summary>
        public string Number { get; set; } = null!;

        /// <summary>Ahorros o corriente.</summary>
        public AccountType Type { get; set; }

        /// <summary>Cédula o NIT del titular.</summary>
        public string CustomerDocument { get; set; } = null!;

        /// <summary>Nombre o razón social del titular.</summary>
        public string CustomerName { get; set; } = null!;

        /// <summary>Ciudad donde se abrió la cuenta.</summary>
        public string OriginCity { get; set; } = null!;

        /// <summary>Saldo actual en pesos.</summary>
        public decimal Balance { get; set; }
    }

    /// <summary>Un movimiento de la cuenta (consignación o retiro).</summary>
    public class MovementDto
    {
        /// <summary>Identificador del movimiento.</summary>
        public long Id { get; set; }

        /// <summary>Consignación o retiro.</summary>
        public MovementType Type { get; set; }

        /// <summary>Monto en pesos (siempre positivo; el tipo indica si suma o resta).</summary>
        public decimal Amount { get; set; }

        /// <summary>Ciudad donde se hizo la operación.</summary>
        public string City { get; set; } = null!;

        /// <summary>Saldo de la cuenta inmediatamente después del movimiento.</summary>
        public decimal BalanceAfter { get; set; }

        /// <summary>Texto libre opcional.</summary>
        public string? Description { get; set; }

        /// <summary>Fecha y hora en UTC. Para mostrarla en hora de Colombia hay que restar 5 horas.</summary>
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>Cuerpo de una consignación o un retiro.</summary>
    public class OperationRequestDto
    {
        /// <summary>Monto en pesos, mayor que cero y con máximo dos decimales.</summary>
        // Límites en cultura invariante: en es-CO "0.01" no se interpreta como número.
        [Range(typeof(decimal), "0.01", "1000000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
            ErrorMessage = "El monto debe estar entre 0,01 y 1.000.000.000.000.")]
        [MaxDecimalPlaces(2, ErrorMessage = "El monto admite como máximo dos decimales.")]
        public decimal Amount { get; set; }

        /// <summary>Ciudad donde se realiza la operación (ver GET /api/cities).</summary>
        [Range(1, int.MaxValue, ErrorMessage = "Debe indicar la ciudad de la operación.")]
        public int CityId { get; set; }

        /// <summary>Texto libre opcional (máximo 200 caracteres).</summary>
        [MaxLength(200, ErrorMessage = "La descripción admite máximo 200 caracteres.")]
        public string? Description { get; set; }
    }

    /// <summary>Resultado de una consignación o un retiro exitoso.</summary>
    public class OperationResultDto
    {
        /// <summary>Número de la cuenta afectada.</summary>
        public string AccountNumber { get; set; } = null!;

        /// <summary>Saldo después de la operación.</summary>
        public decimal Balance { get; set; }

        /// <summary>Movimiento registrado.</summary>
        public MovementDto Movement { get; set; } = null!;
    }

    /// <summary>Extracto mensual de una cuenta.</summary>
    public class StatementDto
    {
        /// <summary>Número de cuenta.</summary>
        public string AccountNumber { get; set; } = null!;

        /// <summary>Año del extracto.</summary>
        public int Year { get; set; }

        /// <summary>Mes del extracto (1-12).</summary>
        public int Month { get; set; }

        /// <summary>Inicio del mes en UTC (medianoche del día 1 en hora del banco).</summary>
        public DateTime PeriodStartUtc { get; set; }

        /// <summary>Fin del mes en UTC, excluido (medianoche del día 1 del mes siguiente en hora del banco).</summary>
        public DateTime PeriodEndUtc { get; set; }

        /// <summary>Saldo al comenzar el mes.</summary>
        public decimal OpeningBalance { get; set; }

        /// <summary>Suma de las consignaciones del mes.</summary>
        public decimal TotalDeposits { get; set; }

        /// <summary>Suma de los retiros del mes.</summary>
        public decimal TotalWithdrawals { get; set; }

        /// <summary>Saldo al terminar el mes (saldo inicial + consignaciones − retiros).</summary>
        public decimal ClosingBalance { get; set; }

        /// <summary>Movimientos del mes en orden cronológico.</summary>
        public List<MovementDto> Movements { get; set; } = new();
    }
}
