namespace KadreeBank.Api.Models.Entities
{
    /// <summary>
    /// Usuario de la banca en línea. Ingresa con su nombre de usuario o con el documento del cliente,
    /// y una contraseña numérica de 4 dígitos. Aquí vive la regla de bloqueo por intentos fallidos.
    /// </summary>
    public class User
    {
        /// <summary>Valor de <see cref="PinHash"/> de un usuario de prueba que aún no tiene contraseña asignada.</summary>
        public const string PendingPinHash = "PENDIENTE";

        /// <summary>Identificador interno (columna identity).</summary>
        public long Id { get; private set; }

        /// <summary>Nombre de usuario: 6 a 20 caracteres en minúscula, números o punto, con al menos una letra.</summary>
        public string Username { get; private set; } = null!;

        /// <summary>Cliente al que pertenece; null para el personal del banco.</summary>
        public long? CustomerId { get; private set; }

        /// <summary>Cliente o Banco.</summary>
        public UserRole Role { get; private set; }

        /// <summary>Hash de la contraseña (ver <c>Helpers/PinHasher</c>). Nunca la contraseña.</summary>
        public string PinHash { get; private set; } = null!;

        /// <summary>Intentos fallidos seguidos desde el último ingreso correcto o el último bloqueo.</summary>
        public int FailedAttempts { get; private set; }

        /// <summary>Hasta cuándo está bloqueado el acceso (UTC); null si no está bloqueado.</summary>
        public DateTime? LockedUntilUtc { get; private set; }

        /// <summary>Fecha de creación en UTC.</summary>
        public DateTime CreatedAtUtc { get; private set; }

        /// <summary>Cliente (navegación de EF Core); null para el personal del banco.</summary>
        public Customer? Customer { get; private set; }

        /// <summary>Constructor para Entity Framework.</summary>
        private User() { }

        /// <summary>Crea un usuario cliente.</summary>
        /// <param name="username">Nombre de usuario ya validado y en minúscula.</param>
        /// <param name="customer">Cliente dueño del usuario.</param>
        /// <param name="pinHash">Hash de la contraseña.</param>
        /// <param name="nowUtc">Fecha de creación.</param>
        public User(string username, Customer customer, string pinHash, DateTime nowUtc)
        {
            Username = username;
            Customer = customer;
            Role = UserRole.Cliente;
            PinHash = pinHash;
            CreatedAtUtc = nowUtc;
        }

        /// <summary>Indica si el acceso está bloqueado en este momento.</summary>
        /// <param name="nowUtc">Instante actual.</param>
        /// <returns>true mientras no haya pasado <see cref="LockedUntilUtc"/>.</returns>
        public bool IsLocked(DateTime nowUtc) => LockedUntilUtc is not null && LockedUntilUtc > nowUtc;

        /// <summary>
        /// Registra un intento fallido. Al llegar a <paramref name="maxAttempts"/> bloquea el acceso durante
        /// <paramref name="lockout"/> y reinicia el contador, para que después del bloqueo haya otros
        /// <paramref name="maxAttempts"/> intentos.
        /// </summary>
        /// <param name="nowUtc">Instante actual.</param>
        /// <param name="maxAttempts">Intentos permitidos antes de bloquear.</param>
        /// <param name="lockout">Duración del bloqueo.</param>
        /// <returns>true si este intento produjo el bloqueo.</returns>
        public bool RegisterFailedAttempt(DateTime nowUtc, int maxAttempts, TimeSpan lockout)
        {
            FailedAttempts++;
            if (FailedAttempts < maxAttempts)
                return false;

            LockedUntilUtc = nowUtc.Add(lockout);
            FailedAttempts = 0;
            return true;
        }

        /// <summary>Registra un ingreso correcto: reinicia el contador y levanta un bloqueo ya vencido.</summary>
        public void RegisterSuccessfulLogin()
        {
            FailedAttempts = 0;
            LockedUntilUtc = null;
        }

        /// <summary>Cambia la contraseña (por ejemplo, al asignar la contraseña de prueba).</summary>
        /// <param name="pinHash">Nuevo hash.</param>
        public void SetPinHash(string pinHash) => PinHash = pinHash;
    }
}
