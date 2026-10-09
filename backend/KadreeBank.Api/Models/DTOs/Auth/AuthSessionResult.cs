using KadreeBank.Api.Models.DTOs.Responses;

namespace KadreeBank.Api.Models.DTOs.Auth
{
    /// <summary>
    /// Lo que el servicio entrega al controlador al abrir o renovar una sesión: la respuesta para el
    /// cuerpo y el refresh token que el controlador pone en la cookie. No se serializa.
    /// </summary>
    public class AuthSessionResult
    {
        /// <summary>Respuesta con el access token y el usuario.</summary>
        public ApiResponse<LoginResultDto> Response { get; set; } = null!;

        /// <summary>Refresh token en claro (solo para la cookie; en la base de datos va su hash).</summary>
        public string RefreshToken { get; set; } = null!;

        /// <summary>Vencimiento del refresh token, para la cookie.</summary>
        public DateTime RefreshTokenExpiresAtUtc { get; set; }
    }
}
