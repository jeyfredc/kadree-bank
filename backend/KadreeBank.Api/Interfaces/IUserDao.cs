using KadreeBank.Api.Models.Entities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>Acceso a datos de usuarios, refresh tokens y registro de clientes.</summary>
    public interface IUserDao
    {
        /// <summary>
        /// Busca el usuario por nombre de usuario o, si el identificador no tiene letras, por el documento
        /// del cliente. Incluye el cliente. Con seguimiento de cambios (se actualizan los intentos).
        /// </summary>
        /// <param name="identifier">Usuario (ya en minúscula) o número de documento.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El usuario, o null si no existe.</returns>
        Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct);

        /// <summary>Busca un usuario por id, con su cliente.</summary>
        /// <param name="userId">Id del usuario.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El usuario, o null si no existe.</returns>
        Task<User?> FindByIdAsync(long userId, CancellationToken ct);

        /// <summary>Indica si un nombre de usuario ya está en uso.</summary>
        /// <param name="username">Usuario en minúscula.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>true si existe.</returns>
        Task<bool> UsernameExistsAsync(string username, CancellationToken ct);

        /// <summary>Indica si ya hay un cliente con ese documento.</summary>
        /// <param name="documentNumber">Documento normalizado.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>true si existe.</returns>
        Task<bool> DocumentExistsAsync(string documentNumber, CancellationToken ct);

        /// <summary>
        /// Crea en UNA transacción el cliente, su cuenta (con número de la secuencia, saldo 0) y su usuario.
        /// Si algo falla no queda nada a medias.
        /// </summary>
        /// <param name="customer">Cliente nuevo (sin id).</param>
        /// <param name="originCityId">Ciudad de origen de la cuenta.</param>
        /// <param name="username">Usuario en minúscula.</param>
        /// <param name="pinHash">Hash de la contraseña.</param>
        /// <param name="nowUtc">Fecha de apertura.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>La cuenta abierta.</returns>
        /// <exception cref="Exceptions.ConflictException">Otro registro tomó el documento o el usuario al mismo tiempo.</exception>
        Task<Account> RegisterAsync(Customer customer, int originCityId, string username, string pinHash, DateTime nowUtc, CancellationToken ct);

        /// <summary>Usuarios de prueba que aún no tienen contraseña ('PENDIENTE').</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Los usuarios, con seguimiento de cambios.</returns>
        Task<List<User>> GetUsersWithPendingPinAsync(CancellationToken ct);

        /// <summary>Busca un refresh token por su hash, con su usuario y el cliente del usuario.</summary>
        /// <param name="tokenHash">SHA-256 del token.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El refresh token, o null si no existe.</returns>
        Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct);

        /// <summary>Agrega un refresh token para guardarlo en el próximo <see cref="SaveChangesAsync"/>.</summary>
        /// <param name="token">Token nuevo.</param>
        void AddRefreshToken(RefreshToken token);

        /// <summary>Revoca todos los tokens vigentes de una familia (una sesión), directamente en la base de datos.</summary>
        /// <param name="familyId">Familia.</param>
        /// <param name="nowUtc">Instante de revocación.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken ct);

        /// <summary>Guarda los cambios pendientes (intentos fallidos, tokens).</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        Task SaveChangesAsync(CancellationToken ct);
    }
}
