namespace KadreeBank.Api.Helpers
{
    /// <summary>Nombres de las políticas de límite de solicitudes.</summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Inicio de sesión, renovación, registro y disponibilidad de usuario: 10 solicitudes por minuto por IP.
        /// Complementa el bloqueo por usuario frenando a quien prueba muchos usuarios distintos.
        /// </summary>
        public const string Auth = "auth";
    }
}
