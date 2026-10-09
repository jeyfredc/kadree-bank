using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Auth;
using KadreeBank.Api.Models.DTOs.Responses;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>Inicio y cierre de sesión, renovación de tokens y registro de clientes.</summary>
    public interface IAuthService
    {
        /// <summary>
        /// Valida usuario (o documento) y contraseña. Tras <c>Auth:MaxFailedAttempts</c> intentos fallidos
        /// seguidos bloquea el acceso <c>Auth:LockoutMinutes</c> minutos.
        /// </summary>
        /// <param name="request">Identificación y contraseña.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Access token, usuario y el refresh token para la cookie.</returns>
        /// <exception cref="AuthenticationFailedException">Usuario o contraseña incorrectos (mismo mensaje en ambos casos).</exception>
        /// <exception cref="AccountLockedException">El acceso está bloqueado.</exception>
        Task<AuthSessionResult> Login(LoginRequestDto request, CancellationToken ct);

        /// <summary>
        /// Renueva la sesión con el refresh token de la cookie y lo rota. Si el token ya se había usado,
        /// revoca toda la sesión (posible robo).
        /// </summary>
        /// <param name="refreshToken">Valor de la cookie; null si no vino.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Access token nuevo y refresh token nuevo.</returns>
        /// <exception cref="InvalidSessionException">No hay token, no existe, venció o fue reutilizado.</exception>
        Task<AuthSessionResult> Refresh(string? refreshToken, CancellationToken ct);

        /// <summary>Cierra la sesión: revoca la familia del refresh token. No falla si no hay sesión.</summary>
        /// <param name="refreshToken">Valor de la cookie; null si no vino.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Confirmación.</returns>
        Task<ApiResponse> Logout(string? refreshToken, CancellationToken ct);

        /// <summary>
        /// Crea cliente, cuenta y usuario. Persona natural → cuenta de ahorros; empresa → cuenta corriente.
        /// La ciudad elegida queda como ciudad de origen de la cuenta.
        /// </summary>
        /// <param name="request">Datos del formulario de registro.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>La cuenta abierta.</returns>
        /// <exception cref="RequestValidationException">Datos inválidos, con el detalle por campo.</exception>
        /// <exception cref="ConflictException">El documento o el usuario ya están registrados.</exception>
        Task<ApiResponse<RegisterResultDto>> Register(RegisterRequestDto request, CancellationToken ct);

        /// <summary>Indica si un nombre de usuario cumple las reglas y está libre.</summary>
        /// <param name="username">Usuario a consultar.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Disponibilidad y mensaje para el formulario.</returns>
        Task<ApiResponse<UsernameAvailabilityDto>> CheckUsername(string username, CancellationToken ct);

        /// <summary>Datos del usuario en sesión.</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Usuario, nombre y rol.</returns>
        /// <exception cref="InvalidSessionException">El usuario del token ya no existe.</exception>
        Task<ApiResponse<AuthUserDto>> Me(CancellationToken ct);
    }
}
