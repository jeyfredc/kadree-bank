using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KadreeBank.Api.Models.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Emite los access tokens (JWT) y los refresh tokens, y define cómo se validan.</summary>
    public class JwtTokenFactory
    {
        /// <summary>Claim con el id del cliente (solo usuarios con rol Cliente).</summary>
        public const string CustomerIdClaim = "customer_id";

        /// <summary>Claim con el rol: <c>Cliente</c> o <c>Banco</c>.</summary>
        public const string RoleClaim = "role";

        /// <summary>Claim con el nombre para mostrar.</summary>
        public const string NameClaim = "name";

        private readonly JwtSettings _settings;
        private readonly SigningCredentials _credentials;
        private readonly JsonWebTokenHandler _handler = new();

        /// <summary>Crea la fábrica con la configuración JWT (ya validada al arrancar).</summary>
        /// <param name="settings">Emisor, audiencia, clave y vigencias.</param>
        public JwtTokenFactory(JwtSettings settings)
        {
            _settings = settings;
            _credentials = new SigningCredentials(SigningKey(settings), SecurityAlgorithms.HmacSha256);
        }

        /// <summary>Vigencia del refresh token.</summary>
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_settings.RefreshTokenDays);

        /// <summary>Emite un access token para el usuario.</summary>
        /// <param name="user">Usuario autenticado.</param>
        /// <param name="displayName">Nombre para mostrar (cliente o usuario del banco).</param>
        /// <param name="nowUtc">Instante actual.</param>
        /// <returns>El JWT y su vencimiento.</returns>
        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, string displayName, DateTime nowUtc)
        {
            var expires = nowUtc.AddMinutes(_settings.AccessTokenMinutes);
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.Username),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(NameClaim, displayName),
                new(RoleClaim, user.Role.ToString()),
            };
            if (user.CustomerId is not null)
                claims.Add(new Claim(CustomerIdClaim, user.CustomerId.Value.ToString()));

            var token = _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                Subject = new ClaimsIdentity(claims),
                IssuedAt = nowUtc,
                NotBefore = nowUtc,
                Expires = expires,
                SigningCredentials = _credentials,
            });
            return (token, expires);
        }

        /// <summary>Genera un refresh token aleatorio de 256 bits.</summary>
        /// <returns>El token (va en la cookie) y su hash (va en la base de datos).</returns>
        public static (string Token, string Hash) CreateRefreshToken()
        {
            var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            return (token, HashRefreshToken(token));
        }

        /// <summary>SHA-256 de un refresh token, en hexadecimal minúscula.</summary>
        /// <param name="token">Token recibido en la cookie.</param>
        /// <returns>El hash con el que se busca en la base de datos.</returns>
        public static string HashRefreshToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        /// <summary>Parámetros con los que la API valida cada access token.</summary>
        /// <param name="settings">Configuración JWT.</param>
        /// <returns>Firma, emisor, audiencia y vigencia obligatorios; tolerancia de reloj de 30 segundos.</returns>
        public static TokenValidationParameters CreateValidationParameters(JwtSettings settings) => new()
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey(settings),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = NameClaim,
            RoleClaimType = RoleClaim,
        };

        private static SymmetricSecurityKey SigningKey(JwtSettings settings) =>
            new(Encoding.UTF8.GetBytes(settings.Key));
    }
}
