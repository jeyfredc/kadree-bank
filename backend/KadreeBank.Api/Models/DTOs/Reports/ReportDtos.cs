using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Models.DTOs.Reports
{
    /// <summary>
    /// Página del reporte por sede. Se pagina por CLIENTE (cada cliente en una sede es un bloque):
    /// <see cref="PagedResult{T}.TotalItems"/> y el tamaño de página cuentan clientes, y
    /// <see cref="PagedResult{T}.Items"/> trae todas las transacciones de los clientes de la página,
    /// así un bloque nunca queda partido entre dos páginas.
    /// </summary>
    public class BranchTransactionsPageDto : PagedResult<BranchTransactionDto>
    {
        /// <summary>Total de transacciones de todos los clientes (todas las páginas) con los filtros.</summary>
        public int TotalTransactions { get; set; }
    }

    /// <summary>Fila del reporte 1: transacciones de un cliente en el mes.</summary>
    public class CustomerTransactionCountDto
    {
        /// <summary>Id del cliente.</summary>
        public long CustomerId { get; set; }

        /// <summary>Cédula o NIT.</summary>
        public string DocumentNumber { get; set; } = null!;

        /// <summary>Nombre o razón social.</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Consignaciones y retiros del mes, sumando todas las cuentas del cliente.</summary>
        public int TransactionCount { get; set; }

        /// <summary>Fecha (UTC) de la primera transacción del mes; null si no tuvo ninguna.</summary>
        public DateTime? FirstTransactionAtUtc { get; set; }

        /// <summary>Fecha (UTC) de la última transacción del mes; null si no tuvo ninguna.</summary>
        public DateTime? LastTransactionAtUtc { get; set; }
    }

    /// <summary>
    /// Fila del reporte por sede: UNA transacción (consignación o retiro) de un cliente en una sede
    /// (ciudad donde se hizo) durante el mes. Las filas de un cliente van seguidas y en orden cronológico.
    /// </summary>
    public class BranchTransactionDto
    {
        /// <summary>Id de la sede (ciudad donde se hizo la transacción).</summary>
        public int CityId { get; set; }

        /// <summary>Nombre de la sede, por ejemplo Bogotá.</summary>
        public string City { get; set; } = null!;

        /// <summary>Id del cliente.</summary>
        public long CustomerId { get; set; }

        /// <summary>Cédula o NIT.</summary>
        public string DocumentNumber { get; set; } = null!;

        /// <summary>Nombre o razón social.</summary>
        public string FullName { get; set; } = null!;

        /// <summary>
        /// Total de transacciones del cliente en la sede (con los filtros aplicados); se repite en cada
        /// fila del cliente. Es el valor por el que se ordena el reporte, de mayor a menor.
        /// </summary>
        public int CustomerTransactionCount { get; set; }

        /// <summary>Id del movimiento (número de comprobante).</summary>
        public long MovementId { get; set; }

        /// <summary>Número de la cuenta del movimiento, por ejemplo AH-1001.</summary>
        public string AccountNumber { get; set; } = null!;

        /// <summary>Motivo: Consignacion o Retiro.</summary>
        public MovementType Type { get; set; }

        /// <summary>Monto en pesos.</summary>
        public decimal Amount { get; set; }

        /// <summary>Descripción del movimiento (opcional).</summary>
        public string? Description { get; set; }

        /// <summary>Fecha y hora (UTC) del movimiento.</summary>
        public DateTime CreatedAtUtc { get; set; }
    }

    /// <summary>Fila del reporte 2: retiros fuera de la ciudad de origen de la cuenta.</summary>
    public class OutOfCityWithdrawalsDto
    {
        /// <summary>Id del cliente.</summary>
        public long CustomerId { get; set; }

        /// <summary>Cédula o NIT.</summary>
        public string DocumentNumber { get; set; } = null!;

        /// <summary>Nombre o razón social.</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Total retirado fuera de la ciudad de origen (supera el umbral consultado).</summary>
        public decimal TotalWithdrawn { get; set; }

        /// <summary>Cantidad de retiros que suman ese total.</summary>
        public int WithdrawalCount { get; set; }

        /// <summary>Fecha (UTC) del primer retiro fuera de la ciudad en el período consultado.</summary>
        public DateTime FirstTransactionAtUtc { get; set; }

        /// <summary>Fecha (UTC) del último retiro fuera de la ciudad en el período consultado.</summary>
        public DateTime LastTransactionAtUtc { get; set; }
    }
}
