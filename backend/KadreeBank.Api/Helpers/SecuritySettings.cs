using System.Text;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Configuración de los JWT, sección <c>Jwt</c> (variables <c>Jwt__*</c>).</summary>
    public class JwtSettings
    {
        /// <summary>Nombre de la sección en la configuración.</summary>
        public const string Section = "Jwt";

        /// <summary>Emisor (<c>iss</c>) de los tokens.</summary>
        public string Issuer { get; set; } = "kadree-bank-api";

        /// <summary>Audiencia (<c>aud</c>) de los tokens.</summary>
        public string Audience { get; set; } = "kadree-bank-web";

        /// <summary>Clave de firma HMAC-SHA256: secreta y de al menos 32 bytes. Va en user-secrets o variables de entorno, nunca en el repositorio.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Vigencia del access token, en minutos.</summary>
        public int AccessTokenMinutes { get; set; } = 15;

        /// <summary>Vigencia del refresh token, en días.</summary>
        public int RefreshTokenDays { get; set; } = 7;
    }

    /// <summary>Configuración de autenticación, sección <c>Auth</c> (variables <c>Auth__*</c>).</summary>
    public class AuthSettings
    {
        /// <summary>Nombre de la sección en la configuración.</summary>
        public const string Section = "Auth";

        /// <summary>
        /// Clave secreta (pepper) que se mezcla con la contraseña antes del hash. Sin ella, una copia
        /// robada de la base de datos no permite descubrir contraseñas de 4 dígitos. Al menos 32 caracteres.
        /// </summary>
        public string PinPepper { get; set; } = string.Empty;

        /// <summary>Intentos fallidos permitidos antes de bloquear el acceso.</summary>
        public int MaxFailedAttempts { get; set; } = 3;

        /// <summary>Duración del bloqueo, en minutos.</summary>
        public int LockoutMinutes { get; set; } = 15;

        /// <summary>Nombre de la cookie del refresh token.</summary>
        public string RefreshCookieName { get; set; } = "kb_refresh";

        /// <summary>Marca la cookie como <c>Secure</c>. Los navegadores la aceptan en http://localhost; en producción va siempre en true.</summary>
        public bool RefreshCookieSecure { get; set; } = true;

        /// <summary>
        /// Contraseña de 4 dígitos que se asigna al arrancar a los usuarios de prueba que no tienen una
        /// ('PENDIENTE'). Vacía = no se asigna. Solo para desarrollo y demostración.
        /// </summary>
        public string DemoPin { get; set; } = string.Empty;
    }

    /// <summary>Validación de la configuración de seguridad al arrancar: falla rápido y con un mensaje claro.</summary>
    public static class SecuritySettingsValidator
    {
        /// <summary>Verifica la longitud de la clave JWT y del pepper.</summary>
        /// <param name="jwt">Configuración JWT.</param>
        /// <param name="auth">Configuración de autenticación.</param>
        /// <exception cref="InvalidOperationException">Falta un secreto o es demasiado corto.</exception>
        public static void Validate(JwtSettings jwt, AuthSettings auth)
        {
            if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
                throw new InvalidOperationException(
                    "Falta 'Jwt:Key' o tiene menos de 32 bytes. Configúrala con user-secrets o con la variable Jwt__Key (ver README).");
            if (auth.PinPepper.Length < 32)
                throw new InvalidOperationException(
                    "Falta 'Auth:PinPepper' o tiene menos de 32 caracteres. Configúralo con user-secrets o con la variable Auth__PinPepper (ver README).");
            if (auth.MaxFailedAttempts < 1 || auth.LockoutMinutes < 1)
                throw new InvalidOperationException("'Auth:MaxFailedAttempts' y 'Auth:LockoutMinutes' deben ser mayores que cero.");
        }
    }
}
