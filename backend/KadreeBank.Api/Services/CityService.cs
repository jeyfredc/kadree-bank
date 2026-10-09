using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Cities;
using KadreeBank.Api.Models.DTOs.Responses;

namespace KadreeBank.Api.Services
{
    /// <summary>Implementa la consulta del catálogo de ciudades.</summary>
    public class CityService : ICityService
    {
        private readonly ICityDao _cityDao;

        /// <summary>Crea el servicio con sus dependencias (inyectadas por DI).</summary>
        /// <param name="cityDao">Acceso a datos de ciudades.</param>
        public CityService(ICityDao cityDao)
        {
            _cityDao = cityDao ?? throw new ArgumentNullException(nameof(cityDao));
        }

        /// <inheritdoc/>
        public async Task<ApiResponse<List<CityDto>>> GetCities(CancellationToken ct)
        {
            var cities = await _cityDao.GetAllAsync(ct);
            return ApiResponse<List<CityDto>>.Ok(cities, "Ciudades obtenidas exitosamente");
        }
    }
}
