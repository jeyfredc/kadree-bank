namespace KadreeBank.Api.Models.Entities
{
    /// <summary>Titular de una o varias cuentas: persona natural o empresa.</summary>
    public class Customer
    {
        /// <summary>Identificador interno (columna identity).</summary>
        public long Id { get; set; }

        /// <summary>Cédula, pasaporte o NIT. Es único.</summary>
        public string DocumentNumber { get; set; } = null!;

        /// <summary>Tipo del documento; NIT solo para empresas.</summary>
        public DocumentType DocumentType { get; set; }

        /// <summary>Nombre completo o razón social.</summary>
        public string FullName { get; set; } = null!;

        /// <summary>Persona natural o jurídica; define qué tipo de cuenta puede tener.</summary>
        public CustomerType Type { get; set; }

        /// <summary>Correo de contacto. Opcional en los clientes de los datos de prueba antiguos.</summary>
        public string? Email { get; set; }

        /// <summary>Celular de contacto (10 dígitos).</summary>
        public string? Phone { get; set; }

        /// <summary>Cuentas del cliente (navegación de EF Core; se usa en los reportes).</summary>
        public ICollection<Account> Accounts { get; set; } = new List<Account>();

        /// <summary>Indica qué tipo de cuenta puede abrir un tipo de cliente.</summary>
        /// <param name="type">Tipo de cliente.</param>
        /// <returns>Ahorros para personas naturales; corriente para empresas.</returns>
        public static AccountType AllowedAccountType(CustomerType type) =>
            type == CustomerType.Natural ? AccountType.Ahorros : AccountType.Corriente;

        /// <summary>Indica si un tipo de documento es válido para un tipo de cliente.</summary>
        /// <param name="customerType">Tipo de cliente.</param>
        /// <param name="documentType">Tipo de documento.</param>
        /// <returns>true si la combinación es válida (NIT solo para empresas; CC, CE y PA solo para personas).</returns>
        public static bool IsDocumentAllowed(CustomerType customerType, DocumentType documentType) =>
            customerType == CustomerType.Juridica ? documentType == DocumentType.NIT : documentType != DocumentType.NIT;
    }
}
