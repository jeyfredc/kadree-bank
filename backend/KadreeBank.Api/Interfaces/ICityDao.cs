using KadreeBank.Api.Models.DTOs.Cities;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>Acceso a datos del catálogo de ciudades.</summary>
    public interface ICityDao
    {
        /// <summary>Lista todas las ciudades.</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Ciudades ordenadas por nombre.</returns>
        Task<List<CityDto>> GetAllAsync(CancellationToken ct);

        /// <summary>Obtiene el nombre de una ciudad. Sirve también para validar que exista.</summary>
        /// <param name="cityId">Id de la ciudad.</param>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>El nombre, o null si la ciudad no existe.</returns>
        Task<string?> GetNameAsync(int cityId, CancellationToken ct);
    }
}
