using KadreeBank.Api.Models.DTOs.Reports;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>
    /// Consultas de los reportes, paginadas en la base de datos: los conteos, el orden y el corte
    /// de página los hace PostgreSQL y solo viaja la página pedida. Los rangos de fechas llegan ya
    /// convertidos a UTC; <c>page</c> y <c>pageSize</c> llegan ya validados.
    /// </summary>
    public interface IReportDao
    {
        /// <summary>Cuenta las transacciones de cada cliente (todas sus cuentas) en un rango.</summary>
        /// <param name="fromUtc">Inicio del rango en UTC (incluido).</param>
        /// <param name="toUtc">Fin del rango en UTC (excluido).</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>
        /// Una página de clientes (todos, incluso con 0), de mayor a menor número de transacciones y
        /// luego por nombre. <c>TotalItems</c> es el total de clientes.
        /// </returns>
        Task<PagedResult<CustomerTransactionCountDto>> GetTransactionCountByCustomerAsync(
            DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken ct);

        /// <summary>Lista las transacciones de un rango con su sede (ciudad donde se hicieron) y su cliente.</summary>
        /// <param name="fromUtc">Inicio del rango en UTC (incluido).</param>
        /// <param name="toUtc">Fin del rango en UTC (excluido).</param>
        /// <param name="cityId">Sede a consultar; null = todas las sedes.</param>
        /// <param name="type">Solo consignaciones o solo retiros; null = ambos.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes (bloques cliente-sede) por página.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>
        /// Una página por clientes: todas las transacciones de los clientes de la página, una fila por
        /// transacción. Los clientes van de mayor a menor total en la sede (luego por sede y nombre) y,
        /// dentro de cada cliente, sus transacciones en orden cronológico.
        /// </returns>
        Task<BranchTransactionsPageDto> GetTransactionsByBranchAsync(
            DateTime fromUtc, DateTime toUtc, int? cityId, MovementType? type, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Suma por cliente los retiros hechos en una ciudad distinta a la ciudad de origen de la cuenta
        /// y devuelve solo los clientes cuyo total es estrictamente mayor que <paramref name="minTotal"/>.
        /// </summary>
        /// <param name="minTotal">Umbral en pesos.</param>
        /// <param name="fromUtc">Inicio opcional del rango en UTC (incluido). Null = sin límite.</param>
        /// <param name="toUtc">Fin opcional del rango en UTC (excluido). Null = sin límite.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Una página de clientes, de mayor a menor total retirado.</returns>
        Task<PagedResult<OutOfCityWithdrawalsDto>> GetOutOfCityWithdrawalsAsync(
            decimal minTotal, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct);
    }
}
