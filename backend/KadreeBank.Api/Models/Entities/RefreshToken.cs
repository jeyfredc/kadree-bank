namespace KadreeBank.Api.Models.Entities
{
    /// <summary>
    /// Refresh token de una sesión. Solo se guarda su SHA-256. Cada uso lo rota: se revoca y se emite
    /// otro de la misma familia. Si alguien presenta uno ya revocado, se revoca toda la familia
    /// (indicio de que el token fue robado).
    /// </summary>
    public class RefreshToken
    {
        /// <summary>Identificador interno.</summary>
        public long Id { get; private set; }

        /// <summary>Usuario dueño de la sesión.</summary>
        public long UserId { get; private set; }

        /// <summary>SHA-256 del token, en hexadecimal (64 caracteres).</summary>
        public string TokenHash { get; private set; } = null!;

        /// <summary>Familia: todos los tokens de una misma sesión comparten este id.</summary>
        public Guid FamilyId { get; private set; }

        /// <summary>Vencimiento en UTC.</summary>
        public DateTime ExpiresAtUtc { get; private set; }

        /// <summary>Emisión en UTC.</summary>
        public DateTime CreatedAtUtc { get; private set; }

        /// <summary>Cuándo se revocó (por uso, cierre de sesión o reutilización); null si está vigente.</summary>
        public DateTime? RevokedAtUtc { get; private set; }

        /// <summary>Hash del token que lo reemplazó al rotarlo.</summary>
        public string? ReplacedByHash { get; private set; }

        /// <summary>Usuario (navegación de EF Core).</summary>
        public User User { get; private set; } = null!;

        /// <summary>Constructor para Entity Framework.</summary>
        private RefreshToken() { }

        /// <summary>Crea un refresh token.</summary>
        /// <param name="userId">Usuario dueño.</param>
        /// <param name="tokenHash">SHA-256 del token.</param>
        /// <param name="familyId">Familia (nueva al iniciar sesión; la misma al rotar).</param>
        /// <param name="nowUtc">Emisión.</param>
        /// <param name="lifetime">Vigencia.</param>
        public RefreshToken(long userId, string tokenHash, Guid familyId, DateTime nowUtc, TimeSpan lifetime)
        {
            UserId = userId;
            TokenHash = tokenHash;
            FamilyId = familyId;
            CreatedAtUtc = nowUtc;
            ExpiresAtUtc = nowUtc.Add(lifetime);
        }

        /// <summary>Indica si el token sirve para renovar la sesión.</summary>
        /// <param name="nowUtc">Instante actual.</param>
        /// <returns>true si no está revocado ni vencido.</returns>
        public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

        /// <summary>Revoca el token.</summary>
        /// <param name="nowUtc">Instante actual.</param>
        /// <param name="replacedByHash">Hash del token que lo reemplaza, si es una rotación.</param>
        public void Revoke(DateTime nowUtc, string? replacedByHash = null)
        {
            RevokedAtUtc ??= nowUtc;
            ReplacedByHash ??= replacedByHash;
        }
    }
}
