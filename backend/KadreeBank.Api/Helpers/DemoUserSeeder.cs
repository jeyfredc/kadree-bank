using KadreeBank.Api.Interfaces;

namespace KadreeBank.Api.Helpers
{
    /// <summary>
    /// Asigna la contraseña de prueba (<see cref="AuthSettings.DemoPin"/>) a los usuarios de los datos semilla
    /// que todavía están en 'PENDIENTE'. Así el SQL no lleva hashes, que dependen del pepper de cada entorno.
    /// Solo actúa si <c>Auth:DemoPin</c> está configurado; en producción se deja vacío.
    /// </summary>
    public static class DemoUserSeeder
    {
        /// <summary>Ejecuta la asignación al arrancar la API.</summary>
        /// <param name="services">Proveedor de servicios de la aplicación.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <exception cref="InvalidOperationException"><c>Auth:DemoPin</c> no tiene 4 dígitos.</exception>
        public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
        {
            await using var scope = services.CreateAsyncScope();
            var settings = scope.ServiceProvider.GetRequiredService<AuthSettings>();
            if (string.IsNullOrWhiteSpace(settings.DemoPin))
                return;
            if (settings.DemoPin.Length != 4 || !settings.DemoPin.All(char.IsAsciiDigit))
                throw new InvalidOperationException("'Auth:DemoPin' debe tener exactamente 4 dígitos.");

            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoUserSeeder));
            var userDao = scope.ServiceProvider.GetRequiredService<IUserDao>();
            var hasher = scope.ServiceProvider.GetRequiredService<PinHasher>();

            try
            {
                var pending = await userDao.GetUsersWithPendingPinAsync(ct);
                if (pending.Count == 0)
                    return;

                foreach (var user in pending)
                    user.SetPinHash(hasher.Hash(settings.DemoPin));
                await userDao.SaveChangesAsync(ct);
                logger.LogInformation("Contraseña de prueba asignada a {Count} usuarios de los datos semilla", pending.Count);
            }
            catch (Exception ex)
            {
                // La base de datos puede no estar lista todavía; la API arranca igual y lo intenta en el próximo inicio.
                logger.LogWarning(ex, "No se pudo asignar la contraseña de prueba a los usuarios semilla");
            }
        }
    }
}
