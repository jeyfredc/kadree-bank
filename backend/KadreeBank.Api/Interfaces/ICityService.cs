using KadreeBank.Api.Models.DTOs.Cities;
using KadreeBank.Api.Models.DTOs.Responses;

namespace KadreeBank.Api.Interfaces
{
    /// <summary>Catálogo de ciudades donde el banco tiene cuentas y donde se hacen operaciones.</summary>
    public interface ICityService
    {
        /// <summary>Lista todas las ciudades.</summary>
        /// <param name="ct">Token para cancelar la operación.</param>
        /// <returns>Ciudades ordenadas por nombre. El <c>Id</c> es el que se envía como <c>cityId</c> al operar.</returns>
        Task<ApiResponse<List<CityDto>>> GetCities(CancellationToken ct);
    }
}
