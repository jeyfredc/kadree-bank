using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Cities;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KadreeBank.Api.Controllers
{
    /// <summary>Catálogo de ciudades (origen de cuentas y lugar de cada operación).</summary>
    [Route("api/[controller]")]
    [AllowAnonymous]
    [ApiController]
    [Produces("application/json")]
    public class CitiesController : ControllerBase
    {
        private readonly ICityService _cityService;

        /// <summary>Crea el controlador.</summary>
        /// <param name="cityService">Consulta del catálogo de ciudades.</param>
        public CitiesController(ICityService cityService)
        {
            _cityService = cityService ?? throw new ArgumentNullException(nameof(cityService));
        }

        /// <summary>Lista las ciudades.</summary>
        /// <param name="ct">Token de cancelación de la petición.</param>
        /// <response code="200">Ciudades ordenadas por nombre; el Id se usa como <c>cityId</c> al operar.</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<List<CityDto>>))]
        public async Task<IActionResult> GetCities(CancellationToken ct) =>
            Ok(await _cityService.GetCities(ct));
    }
}
