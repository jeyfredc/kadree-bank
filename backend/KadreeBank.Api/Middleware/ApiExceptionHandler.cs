using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Responses;
using Microsoft.AspNetCore.Diagnostics;

namespace KadreeBank.Api.Middleware
{
    /// <summary>
    /// Manejo centralizado de errores: toda excepción se convierte en un <see cref="ApiResponse"/>
    /// con Success = false, un ErrorCode estable y el código HTTP que corresponde.
    /// Los errores 500 nunca exponen detalles internos al cliente; quedan en el log.
    /// </summary>
    public class ApiExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<ApiExceptionHandler> _logger;

        /// <summary>Crea el manejador.</summary>
        /// <param name="logger">Registra los errores 500 con su detalle y los rechazos 4xx como información.</param>
        public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Escribe la respuesta de error. Autenticación: <see cref="AuthenticationFailedException"/> y
        /// <see cref="InvalidSessionException"/> → 401, <see cref="ForbiddenException"/> → 403,
        /// <see cref="ConflictException"/> → 409, <see cref="AccountLockedException"/> → 423.
        /// Resto: <see cref="RequestValidationException"/> → 400,
        /// <see cref="NotFoundException"/> → 404, <see cref="ConcurrencyConflictException"/> → 409,
        /// <see cref="BusinessRuleException"/> → 422 y cualquier otra → 500.
        /// </summary>
        /// <param name="http">Contexto de la petición que falló.</param>
        /// <param name="exception">Excepción no controlada.</param>
        /// <param name="ct">Token para cancelar la escritura de la respuesta.</param>
        /// <returns>Siempre true: todas las excepciones quedan manejadas.</returns>
        public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
        {
            var (status, response) = exception switch
            {
                RequestValidationException ex => (StatusCodes.Status400BadRequest, new ApiResponse
                {
                    Success = false,
                    Message = "La solicitud tiene datos inválidos.",
                    ErrorCode = "validacion",
                    ValidationErrors = ex.ValidationErrors,
                    Errors = ex.ValidationErrors.Values.SelectMany(v => v).ToList(),
                }),
                AuthenticationFailedException ex => (StatusCodes.Status401Unauthorized, ApiResponse.Fail(ex.Message, "credenciales_invalidas")),
                InvalidSessionException ex => (StatusCodes.Status401Unauthorized, ApiResponse.Fail(ex.Message, "sesion_invalida")),
                AccountLockedException ex => (StatusCodes.Status423Locked, ApiResponse.Fail(ex.Message, "acceso_bloqueado")),
                ForbiddenException ex => (StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message, "sin_permiso")),
                ConflictException ex => (StatusCodes.Status409Conflict, new ApiResponse
                {
                    Success = false,
                    Message = ex.Message,
                    ErrorCode = ex.ErrorCode,
                    Errors = new[] { ex.Message },
                    ValidationErrors = new Dictionary<string, string[]> { [ex.Field] = new[] { ex.Message } },
                }),
                NotFoundException ex => (StatusCodes.Status404NotFound, ApiResponse.Fail(ex.Message, "no_encontrado")),
                ConcurrencyConflictException ex => (StatusCodes.Status409Conflict, ApiResponse.Fail(ex.Message, "operacion_concurrente")),
                BusinessRuleException ex => (StatusCodes.Status422UnprocessableEntity, ApiResponse.Fail(ex.Message, ex.ErrorCode)),
                BadHttpRequestException ex => (StatusCodes.Status400BadRequest, ApiResponse.Fail(ex.Message, "solicitud_invalida")),
                _ => (StatusCodes.Status500InternalServerError,
                    ApiResponse.Fail("Ocurrió un error interno. Si persiste, contacte al banco.", "error_interno")),
            };

            if (status >= 500)
                _logger.LogError(exception, "Error no controlado procesando {Method} {Path}", http.Request.Method, http.Request.Path);
            else
                _logger.LogInformation("Solicitud rechazada ({Status}): {Message}", status, exception.Message);

            http.Response.StatusCode = status;
            await http.Response.WriteAsJsonAsync(response, ct);
            return true;
        }
    }
}
