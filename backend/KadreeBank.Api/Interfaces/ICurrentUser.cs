using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>
    /// Quién hace la petición, según los claims del access token. Los servicios lo usan para que un
    /// cliente solo vea y opere sus propias cuentas.
    /// </summary>
    public interface ICurrentUser
    {
        /// <summary>true si la petición trae un access token válido.</summary>
        bool IsAuthenticated { get; }

        /// <summary>Id del usuario (claim <c>sub</c>).</summary>
        long? UserId { get; }

        /// <summary>Id del cliente (claim <c>customer_id</c>); null para el personal del banco.</summary>
        long? CustomerId { get; }

        /// <summary>Rol del usuario.</summary>
        UserRole? Role { get; }

        /// <summary>true si el usuario es personal del banco.</summary>
        bool IsBanco { get; }
    }
}
