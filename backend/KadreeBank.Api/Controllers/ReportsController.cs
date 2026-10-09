using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Reports;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KadreeBank.Api.Controllers
{
    /// <summary>
    /// Reportes calculados en línea sobre los datos transaccionales. Todos se paginan en el servidor
    /// con <c>page</c> (desde 1) y <c>pageSize</c> (1 a 100; por defecto 20).
    /// </summary>
    [Route("api/[controller]")]
    [Authorize(Policy = AuthPolicies.Banco)]
    [ApiController]
    [Produces("application/json")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        /// <summary>Crea el controlador.</summary>
        /// <param name="reportService">Generación de los reportes.</param>
        public ReportsController(IReportService reportService)
        {
            _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        }

        /// <summary>Reporte 1: clientes y su número de transacciones en el mes, de mayor a menor.</summary>
        /// <param name="year">Año, por ejemplo 2026.</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Una página de clientes (incluye los que tienen 0 transacciones).</response>
        /// <response code="400">Año o mes inválido, mes futuro o paginación inválida (<c>validacion</c>).</response>
        [HttpGet("transactions-by-customer")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PagedResult<CustomerTransactionCountDto>>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetTransactionsByCustomer(
            [FromQuery] int year, [FromQuery] int month,
            [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken ct = default) =>
            Ok(await _reportService.GetTransactionsByCustomer(year, month, page, pageSize, ct));

        /// <summary>
        /// Reporte por sede (administrador): clientes con su número de transacciones en el mes por sede
        /// (ciudad donde se hicieron), de mayor a menor, con el detalle de cada transacción.
        /// Filtros opcionales de sede y de tipo. Se pagina por cliente.
        /// </summary>
        /// <param name="year">Año, por ejemplo 2026.</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="cityId">Sede (ver GET /api/cities). Sin valor = todas las sedes.</param>
        /// <param name="type">Consignacion o Retiro. Sin valor = ambos.</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Una página de clientes; en <c>items</c>, una fila por transacción.</response>
        /// <response code="400">Mes inválido o futuro, sede inexistente, tipo inválido o paginación inválida (<c>validacion</c>).</response>
        [HttpGet("transactions-by-branch")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<BranchTransactionsPageDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetTransactionsByBranch(
            [FromQuery] int year, [FromQuery] int month, [FromQuery] int? cityId, [FromQuery] MovementType? type,
            [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken ct = default) =>
            Ok(await _reportService.GetTransactionsByBranch(year, month, cityId, type, page, pageSize, ct));

        /// <summary>
        /// Reporte 2: clientes que retiran fuera de la ciudad de origen de su cuenta
        /// y cuyo total de esos retiros supera el umbral (por defecto $1.000.000).
        /// </summary>
        /// <param name="minTotal">Umbral en pesos; se listan los totales estrictamente mayores.</param>
        /// <param name="from">Fecha inicial opcional (yyyy-MM-dd, inclusiva).</param>
        /// <param name="to">Fecha final opcional (yyyy-MM-dd, inclusiva).</param>
        /// <param name="page">Página, desde 1.</param>
        /// <param name="pageSize">Clientes por página (1-100).</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Una página de clientes, de mayor a menor total retirado.</response>
        /// <response code="400">Umbral negativo, rango de fechas invertido o paginación inválida (<c>validacion</c>).</response>
        [HttpGet("out-of-city-withdrawals")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<PagedResult<OutOfCityWithdrawalsDto>>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetOutOfCityWithdrawals(
            [FromQuery] decimal? minTotal, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
            [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken ct = default) =>
            Ok(await _reportService.GetOutOfCityWithdrawals(minTotal, from, to, page, pageSize, ct));
    }
}
