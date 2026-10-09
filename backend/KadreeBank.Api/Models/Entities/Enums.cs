namespace KadreeBank.Api.Models.Entities
{
    /// <summary>Tipo de cliente. Determina qué tipo de cuenta puede abrir.</summary>
    public enum CustomerType
    {
        /// <summary>Persona natural: solo cuentas de ahorros.</summary>
        Natural = 1,

        /// <summary>Persona jurídica (empresa): solo cuentas corrientes.</summary>
        Juridica = 2,
    }

    /// <summary>Tipo de cuenta. En la base de datos se guarda como texto: <c>AHORROS</c> o <c>CORRIENTE</c>.</summary>
    public enum AccountType
    {
        /// <summary>Cuenta de ahorros, para personas naturales.</summary>
        Ahorros = 1,

        /// <summary>Cuenta corriente, para empresas.</summary>
        Corriente = 2,
    }

    /// <summary>Tipo de documento de identidad. En la base de datos se guarda como texto (<c>CC</c>, <c>CE</c>, <c>PA</c>, <c>NIT</c>).</summary>
    public enum DocumentType
    {
        /// <summary>Cédula de ciudadanía (persona natural).</summary>
        CC = 1,

        /// <summary>Cédula de extranjería (persona natural).</summary>
        CE = 2,

        /// <summary>Pasaporte (persona natural).</summary>
        PA = 3,

        /// <summary>NIT (empresa).</summary>
        NIT = 4,
    }

    /// <summary>Rol del usuario de la banca en línea.</summary>
    public enum UserRole
    {
        /// <summary>Cliente: solo ve y opera sus propias cuentas.</summary>
        Cliente = 1,

        /// <summary>Personal del banco: consulta todas las cuentas y los reportes; no opera cuentas ajenas.</summary>
        Banco = 2,
    }

    /// <summary>Tipo de movimiento. En la base de datos se guarda como texto: <c>CONSIGNACION</c> o <c>RETIRO</c>.</summary>
    public enum MovementType
    {
        /// <summary>Entrada de dinero: suma al saldo.</summary>
        Consignacion = 1,

        /// <summary>Salida de dinero: resta del saldo.</summary>
        Retiro = 2,
    }
}
