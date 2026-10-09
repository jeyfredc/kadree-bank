using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KadreeBank.Api.Controllers
{
    /// <summary>Cuentas: saldo, consignaciones, retiros, movimientos y extractos.</summary>
    /// <remarks>
    /// Los errores no se capturan aquí: el ApiExceptionHandler los convierte en un ApiResponse
    /// con el código HTTP correcto (400, 404, 409, 422 o 500).
    /// </remarks>
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    [Produces("application/json")]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;

        /// <summary>Crea el controlador.</summary>
        /// <param name="accountService">Casos de uso de las cuentas.</param>
        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService ?? throw new ArgumentNullException(nameof(accountService));
        }

        /// <summary>Lista las cuentas con su saldo actual.</summary>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Cuentas ordenadas por número.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<AccountSummaryDto>>))]
        public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
            Ok(await _accountService.GetAccounts(ct));

        /// <summary>Consulta el saldo y los datos de una cuenta.</summary>
        /// <param name="number">Número de cuenta, por ejemplo AH-1001.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Datos y saldo de la cuenta.</response>
        /// <response code="404">La cuenta no existe (<c>no_encontrado</c>).</response>
        [HttpGet("{number}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AccountSummaryDto>))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetAccount(string number, CancellationToken ct) =>
            Ok(await _accountService.GetAccount(number, ct));

        /// <summary>Movimientos más recientes de la cuenta, del más nuevo al más antiguo.</summary>
        /// <param name="number">Número de cuenta.</param>
        /// <param name="limit">Cantidad de movimientos (1-100). Por defecto 20.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Movimientos con su ciudad y saldo resultante.</response>
        /// <response code="400">El límite está fuera de rango (<c>validacion</c>).</response>
        /// <response code="404">La cuenta no existe (<c>no_encontrado</c>).</response>
        [HttpGet("{number}/movements")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<MovementDto>>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetRecentMovements(string number, [FromQuery] int? limit, CancellationToken ct) =>
            Ok(await _accountService.GetRecentMovements(number, limit, ct));

        /// <summary>Extracto mensual: saldo inicial, movimientos del mes, totales y saldo final.</summary>
        /// <remarks>El mes se delimita en la zona horaria del banco (America/Bogota por defecto).</remarks>
        /// <param name="number">Número de cuenta.</param>
        /// <param name="year">Año, por ejemplo 2026.</param>
        /// <param name="month">Mes (1-12). No puede ser un mes futuro.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">El extracto del mes.</response>
        /// <response code="400">Año o mes inválido, o mes futuro (<c>validacion</c>).</response>
        /// <response code="404">La cuenta no existe (<c>no_encontrado</c>).</response>
        [HttpGet("{number}/statements/{year:int}/{month:int}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<StatementDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse))]
        public async Task<IActionResult> GetMonthlyStatement(string number, int year, int month, CancellationToken ct) =>
            Ok(await _accountService.GetMonthlyStatement(number, year, month, ct));

        /// <summary>Consigna dinero en la cuenta.</summary>
        /// <param name="number">Número de cuenta.</param>
        /// <param name="request">Monto, ciudad de la operación y descripción opcional.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="201">Consignación aplicada: nuevo saldo y movimiento registrado.</response>
        /// <response code="400">Monto, ciudad o descripción inválidos (<c>validacion</c>).</response>
        /// <response code="404">La cuenta no existe (<c>no_encontrado</c>).</response>
        /// <response code="409">Otra operación tiene la cuenta bloqueada; reintentar (<c>operacion_concurrente</c>).</response>
        [HttpPost("{number}/deposits")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OperationResultDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Deposit(string number, [FromBody] OperationRequestDto request, CancellationToken ct) =>
            StatusCode(StatusCodes.Status201Created, await _accountService.Deposit(number, request, ct));

        /// <summary>Retira dinero de la cuenta.</summary>
        /// <param name="number">Número de cuenta.</param>
        /// <param name="request">Monto, ciudad de la operación y descripción opcional.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="201">Retiro aplicado: nuevo saldo y movimiento registrado.</response>
        /// <response code="400">Monto, ciudad o descripción inválidos (<c>validacion</c>).</response>
        /// <response code="404">La cuenta no existe (<c>no_encontrado</c>).</response>
        /// <response code="409">Otra operación tiene la cuenta bloqueada; reintentar (<c>operacion_concurrente</c>).</response>
        /// <response code="422">La cuenta quedaría con saldo negativo (<c>saldo_insuficiente</c>).</response>
        [HttpPost("{number}/withdrawals")]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<OperationResultDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Withdraw(string number, [FromBody] OperationRequestDto request, CancellationToken ct) =>
            StatusCode(StatusCodes.Status201Created, await _accountService.Withdraw(number, request, ct));
    }
}
