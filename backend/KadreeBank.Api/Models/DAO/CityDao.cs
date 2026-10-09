using KadreeBank.Api.Data;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Cities;
using Microsoft.EntityFrameworkCore;

namespace KadreeBank.Api.Models.DAO
{
    /// <summary>Implementación con Entity Framework Core del acceso a ciudades.</summary>
    public class CityDao : ICityDao
    {
        private readonly KadreeBankDbContext _db;

        /// <summary>Crea el DAO con sus dependencias (inyectadas por DI).</summary>
        /// <param name="db">Contexto de EF Core; uno por petición HTTP.</param>
        public CityDao(KadreeBankDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        public Task<List<CityDto>> GetAllAsync(CancellationToken ct) =>
            _db.Cities.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CityDto { Id = c.Id, Name = c.Name })
                .ToListAsync(ct);

        /// <inheritdoc/>
        public Task<string?> GetNameAsync(int cityId, CancellationToken ct) =>
            _db.Cities.AsNoTracking()
                .Where(c => c.Id == cityId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);
    }
}
