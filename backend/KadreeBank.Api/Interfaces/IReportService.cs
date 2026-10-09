using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Reports;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>
    /// Reportes en tiempo real: se calculan en el momento de la consulta sobre los datos
    /// transaccionales, sin tablas intermedias ni procesos nocturnos. Todos se paginan en el servidor
    /// (<c>page</c> desde 1, <c>pageSize</c> de 1 a 100).
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Reporte 1: todos los clientes con su número de transacciones (consignaciones y retiros,
        /// de todas sus cuentas) en un mes, ordenados de mayor a menor.
        /// </summary>
        /// <remarks>
        /// Incluye a los clientes sin transacciones (con 0). Los empates se ordenan por nombre.
        /// El mes se delimita en la zona horaria del banco.
        /// </remarks>
        /// <param name="year">Año (2000-2100).</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Una página de clientes.</returns>
        /// <exception cref="RequestValidationException">Año o mes inválido, mes futuro, o paginación inválida.</exception>
        Task<ApiResponse<PagedResult<CustomerTransactionCountDto>>> GetTransactionsByCustomer(
            int year, int month, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Reporte por sede (solo el administrador): clientes con su número de transacciones en un mes,
        /// por sede, ordenados de mayor a menor, con el detalle de cada transacción.
        /// </summary>
        /// <remarks>
        /// La sede es la ciudad donde se hizo la transacción. Sin <paramref name="cityId"/> se
        /// consultan todas las sedes. Se pagina por cliente (bloque cliente-sede): cada página trae
        /// todas las transacciones de sus clientes, una fila por transacción, y
        /// <c>CustomerTransactionCount</c> repite el total del cliente en la sede, que define el orden.
        /// Solo aparecen los clientes con al menos una transacción que cumpla los filtros. El mes se
        /// delimita en la zona horaria del banco.
        /// </remarks>
        /// <param name="year">Año (2000-2100).</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="cityId">Sede; null = todas.</param>
        /// <param name="type">Consignacion o Retiro; null = ambos.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Una página de clientes con sus transacciones y el total de transacciones del filtro.</returns>
        /// <exception cref="RequestValidationException">Mes inválido o futuro, sede inexistente, o paginación inválida.</exception>
        Task<ApiResponse<BranchTransactionsPageDto>> GetTransactionsByBranch(
            int year, int month, int? cityId, MovementType? type, int page, int pageSize, CancellationToken ct);

        /// <summary>
        /// Reporte 2: clientes que retiran dinero fuera de la ciudad de origen de su cuenta
        /// y cuyo total de esos retiros supera el umbral.
        /// </summary>
        /// <remarks>
        /// Cada retiro se compara con la ciudad de origen de la cuenta desde la que se hizo, y los
        /// totales se suman por cliente (todas sus cuentas). "Supera" significa estrictamente mayor:
        /// un cliente con exactamente el umbral no aparece.
        /// </remarks>
        /// <param name="minTotal">Umbral en pesos. Si es null se usa $1.000.000.</param>
        /// <param name="from">Fecha inicial opcional, inclusiva, en hora del banco.</param>
        /// <param name="to">Fecha final opcional, inclusiva, en hora del banco.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Una página de clientes, de mayor a menor total retirado.</returns>
        /// <exception cref="RequestValidationException">Umbral negativo, <paramref name="from"/> posterior a <paramref name="to"/>, o paginación inválida.</exception>
        Task<ApiResponse<PagedResult<OutOfCityWithdrawalsDto>>> GetOutOfCityWithdrawals(
            decimal? minTotal, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken ct);
    }
}
