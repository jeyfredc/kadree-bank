namespace KadreeBank.Api.Helpers
{
    /// <summary>Nombres de las políticas de autorización.</summary>
    public static class AuthPolicies
    {
        /// <summary>Solo personal del banco (rol <c>Banco</c>): reportes.</summary>
        public const string Banco = "Banco";
    }
}
