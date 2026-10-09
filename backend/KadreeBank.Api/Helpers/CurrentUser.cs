using System.Security.Claims;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KadreeBank.Api.Helpers
{
    /// <summary>Lee el usuario actual de los claims del access token ya validado.</summary>
    public class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        /// <summary>Crea el lector de la petición actual.</summary>
        /// <param name="accessor">Acceso al <see cref="HttpContext"/> de la petición.</param>
        public CurrentUser(IHttpContextAccessor accessor)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
        }

        private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

        /// <inheritdoc/>
        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        /// <inheritdoc/>
        public long? UserId => ReadLong(JwtRegisteredClaimNames.Sub);

        /// <inheritdoc/>
        public long? CustomerId => ReadLong(JwtTokenFactory.CustomerIdClaim);

        /// <inheritdoc/>
        public UserRole? Role =>
            Enum.TryParse<UserRole>(Principal?.FindFirst(JwtTokenFactory.RoleClaim)?.Value, out var role) ? role : null;

        /// <inheritdoc/>
        public bool IsBanco => Role == UserRole.Banco;

        private long? ReadLong(string claim) =>
            long.TryParse(Principal?.FindFirst(claim)?.Value, out var value) ? value : null;
    }
}
