namespace KadreeBank.Api.Models.DTOs.Cities
{
    /// <summary>Ciudad del catálogo.</summary>
    public class CityDto
    {
        /// <summary>Id que se envía como <c>cityId</c> al consignar o retirar.</summary>
        public int Id { get; set; }

        /// <summary>Nombre de la ciudad.</summary>
        public string Name { get; set; } = null!;
    }
}
