using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Auth;
using KadreeBank.Api.Models.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KadreeBank.Api.Controllers
{
    /// <summary>
    /// Sesión y registro. El access token va en el cuerpo de la respuesta; el refresh token, en una cookie
    /// HttpOnly, Secure y SameSite=Strict limitada a <c>/api/auth</c>, que JavaScript no puede leer.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private const string CookiePath = "/api/auth";

        private readonly IAuthService _authService;
        private readonly AuthSettings _settings;

        /// <summary>Crea el controlador.</summary>
        /// <param name="authService">Lógica de sesión y registro.</param>
        /// <param name="settings">Nombre y opciones de la cookie.</param>
        public AuthController(IAuthService authService, AuthSettings settings)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Inicia sesión con usuario o número de documento y la contraseña de 4 dígitos.</summary>
        /// <param name="request">Identificación y contraseña.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Sesión iniciada: access token en el cuerpo y refresh token en la cookie.</response>
        /// <response code="400">Faltan datos o la contraseña no tiene 4 dígitos (<c>validacion</c>).</response>
        /// <response code="401">Usuario o contraseña incorrectos (<c>credenciales_invalidas</c>).</response>
        /// <response code="423">Acceso bloqueado por intentos fallidos (<c>acceso_bloqueado</c>).</response>
        /// <response code="429">Demasiadas solicitudes desde esta conexión (<c>demasiados_intentos</c>).</response>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LoginResultDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status423Locked, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
        {
            var session = await _authService.Login(request, ct);
            SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);
            return Ok(session.Response);
        }

        /// <summary>Renueva el access token con la cookie del refresh token, que se rota en cada uso.</summary>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Access token nuevo; la cookie trae un refresh token nuevo.</response>
        /// <response code="401">No hay sesión, venció o el token se reutilizó (<c>sesion_invalida</c>).</response>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<LoginResultDto>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            try
            {
                var session = await _authService.Refresh(Request.Cookies[_settings.RefreshCookieName], ct);
                SetRefreshCookie(session.RefreshToken, session.RefreshTokenExpiresAtUtc);
                return Ok(session.Response);
            }
            catch
            {
                DeleteRefreshCookie();
                throw;
            }
        }

        /// <summary>Cierra la sesión: revoca el refresh token y borra la cookie.</summary>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Sesión cerrada (también si no había sesión).</response>
        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var result = await _authService.Logout(Request.Cookies[_settings.RefreshCookieName], ct);
            DeleteRefreshCookie();
            return Ok(result);
        }

        /// <summary>Crea cliente, cuenta y usuario (registro en línea).</summary>
        /// <param name="request">Datos del formulario de registro.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="201">Cuenta creada.</response>
        /// <response code="400">Datos inválidos, con el detalle por campo (<c>validacion</c>).</response>
        /// <response code="409">El documento o el usuario ya están registrados (<c>documento_registrado</c>, <c>usuario_no_disponible</c>).</response>
        [HttpPost("register")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(ApiResponse<RegisterResultDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse))]
        [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct) =>
            StatusCode(StatusCodes.Status201Created, await _authService.Register(request, ct));

        /// <summary>Indica si un nombre de usuario cumple las reglas y está libre.</summary>
        /// <param name="username">Usuario a consultar.</param>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Disponibilidad y mensaje para el formulario.</response>
        [HttpGet("username-availability")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Auth)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<UsernameAvailabilityDto>))]
        public async Task<IActionResult> UsernameAvailability([FromQuery] string username, CancellationToken ct) =>
            Ok(await _authService.CheckUsername(username, ct));

        /// <summary>Datos del usuario en sesión.</summary>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Usuario, nombre y rol.</response>
        /// <response code="401">Sin access token válido (<c>no_autenticado</c>).</response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<AuthUserDto>))]
        [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ApiResponse))]
        public async Task<IActionResult> Me(CancellationToken ct) => Ok(await _authService.Me(ct));

        private void SetRefreshCookie(string token, DateTime expiresAtUtc) =>
            Response.Cookies.Append(_settings.RefreshCookieName, token, CookieOptions(expiresAtUtc));

        private void DeleteRefreshCookie() =>
            Response.Cookies.Delete(_settings.RefreshCookieName, CookieOptions(null));

        private CookieOptions CookieOptions(DateTime? expiresAtUtc) => new()
        {
            HttpOnly = true,
            Secure = _settings.RefreshCookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expiresAtUtc,
            IsEssential = true,
        };
    }
}
