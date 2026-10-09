using System.Security.Cryptography;
using System.Text;

namespace KadreeBank.Api.Helpers
{
    /// <summary>
    /// Hash de la contraseña de 4 dígitos.
    /// <para>
    /// Solo existen 10.000 contraseñas posibles, así que un hash lento por sí solo no basta: con una copia
    /// de la base de datos se probarían todas en segundos. Por eso primero se aplica un HMAC-SHA256 con una
    /// clave secreta (<see cref="AuthSettings.PinPepper"/>) que no está en la base de datos, y sobre ese
    /// resultado PBKDF2-SHA256 con sal aleatoria y 600.000 iteraciones.
    /// </para>
    /// Formato guardado: <c>pbkdf2-sha256$&lt;iteraciones&gt;$&lt;sal base64&gt;$&lt;hash base64&gt;</c>.
    /// </summary>
    public class PinHasher
    {
        private const string Algorithm = "pbkdf2-sha256";
        private const int Iterations = 600_000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private readonly byte[] _pepper;

        /// <summary>Crea el hasher con el pepper configurado.</summary>
        /// <param name="settings">Configuración de autenticación (ya validada al arrancar).</param>
        public PinHasher(AuthSettings settings)
        {
            _pepper = Encoding.UTF8.GetBytes(settings.PinPepper);
        }

        /// <summary>Calcula el hash de una contraseña.</summary>
        /// <param name="pin">Contraseña de 4 dígitos.</param>
        /// <returns>El texto que se guarda en <c>users.pin_hash</c>.</returns>
        public string Hash(string pin)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var hash = Derive(pin, salt, Iterations);
            return $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        /// <summary>Verifica una contraseña contra un hash guardado, en tiempo constante.</summary>
        /// <param name="pin">Contraseña escrita por el usuario.</param>
        /// <param name="stored">Hash guardado. Un valor que no tenga el formato esperado (por ejemplo 'PENDIENTE') nunca coincide.</param>
        /// <returns>true si coincide.</returns>
        public bool Verify(string pin, string stored)
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Algorithm || !int.TryParse(parts[1], out var iterations) || iterations < 1)
            {
                VerifyDummy(pin);
                return false;
            }

            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                VerifyDummy(pin);
                return false;
            }

            var actual = Derive(pin, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>
        /// Hace el mismo trabajo que una verificación real. Se usa cuando el usuario no existe, para que el
        /// tiempo de respuesta no revele si un usuario o documento está registrado.
        /// </summary>
        /// <param name="pin">Contraseña escrita.</param>
        public void VerifyDummy(string pin) => Derive(pin, new byte[SaltSize], Iterations);

        private byte[] Derive(string pin, byte[] salt, int iterations)
        {
            var peppered = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(pin));
            return Rfc2898DeriveBytes.Pbkdf2(peppered, salt, iterations, HashAlgorithmName.SHA256, HashSize);
        }
    }
}
