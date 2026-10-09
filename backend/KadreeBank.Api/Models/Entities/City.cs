namespace KadreeBank.Api.Models.Entities
{
    /// <summary>Ciudad del catálogo: origen de las cuentas y lugar donde se hace cada operación.</summary>
    public class City
    {
        /// <summary>Identificador (fijo, definido en los datos semilla).</summary>
        public int Id { get; set; }

        /// <summary>Nombre de la ciudad. Es único.</summary>
        public string Name { get; set; } = null!;
    }
}
