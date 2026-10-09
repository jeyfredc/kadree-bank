using KadreeBank.Api.Data;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Reports;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KadreeBank.Api.Models.DAO
{
    /// <summary>
    /// Implementación con Entity Framework Core de los reportes. Los conteos, el orden y el corte de
    /// página (OFFSET/LIMIT) se traducen a SQL: no se cargan en memoria más filas que las de la página.
    /// Cada orden termina en una clave única (id) para que la paginación sea estable.
    /// </summary>
    public class ReportDao : IReportDao
    {
        private readonly KadreeBankDbContext _db;

        /// <summary>Crea el DAO con sus dependencias (inyectadas por DI).</summary>
        /// <param name="db">Contexto de EF Core; uno por petición HTTP.</param>
        public ReportDao(KadreeBankDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Dos consultas: COUNT de clientes y un SELECT sobre clientes con subconsultas de conteo y de
        /// fechas, ordenado y cortado con OFFSET/LIMIT.
        /// </remarks>
        public async Task<PagedResult<CustomerTransactionCountDto>> GetTransactionCountByCustomerAsync(
            DateTime fromUtc, DateTime toUtc, int page, int pageSize, CancellationToken ct)
        {
            var total = await _db.Customers.CountAsync(ct);

            var items = await _db.Customers.AsNoTracking()
                .Select(c => new
                {
                    Customer = c,
                    Movements = c.Accounts
                        .SelectMany(a => a.Movements)
                        .Where(m => m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtc),
                })
                .Select(x => new CustomerTransactionCountDto
                {
                    CustomerId = x.Customer.Id,
                    DocumentNumber = x.Customer.DocumentNumber,
                    FullName = x.Customer.FullName,
                    TransactionCount = x.Movements.Count(),
                    FirstTransactionAtUtc = x.Movements.Min(m => (DateTime?)m.CreatedAtUtc),
                    LastTransactionAtUtc = x.Movements.Max(m => (DateTime?)m.CreatedAtUtc),
                })
                .OrderByDescending(r => r.TransactionCount)
                .ThenBy(r => r.FullName)
                .ThenBy(r => r.CustomerId)
                .Skip(Paging.Skip(page, pageSize))
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<CustomerTransactionCountDto> { Items = items, Page = page, PageSize = pageSize, TotalItems = total };
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Se pagina por bloques cliente-sede para no partir un cliente entre páginas:
        /// <list type="number">
        /// <item>COUNT de movimientos y de bloques (<c>GROUP BY</c> sede, cliente).</item>
        /// <item>Los bloques de la página: <c>GROUP BY</c> con su total, ordenados y cortados con OFFSET/LIMIT.</item>
        /// <item>Las transacciones de esos clientes y sedes (filtro <c>IN</c> por cliente y por sede).</item>
        /// </list>
        /// Como máximo se traen las transacciones de <paramref name="pageSize"/> clientes.
        /// </remarks>
        public async Task<BranchTransactionsPageDto> GetTransactionsByBranchAsync(
            DateTime fromUtc, DateTime toUtc, int? cityId, MovementType? type, int page, int pageSize, CancellationToken ct)
        {
            var movements = _db.Movements.AsNoTracking()
                .Where(m => m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtc);

            if (cityId.HasValue)
                movements = movements.Where(m => m.CityId == cityId.Value);
            if (type.HasValue)
                movements = movements.Where(m => m.Type == type.Value);

            // Un bloque por cliente y sede, con su total de transacciones.
            var blocks = movements
                .GroupBy(m => new { m.CityId, CityName = m.City.Name, m.Account.CustomerId, m.Account.Customer.FullName })
                .Select(g => new { g.Key.CityId, g.Key.CityName, g.Key.CustomerId, g.Key.FullName, Count = g.Count() });

            var totalBlocks = await blocks.CountAsync(ct);
            var totalTransactions = await movements.CountAsync(ct);

            var pageBlocks = await blocks
                .OrderByDescending(b => b.Count)
                .ThenBy(b => b.CityName)
                .ThenBy(b => b.FullName)
                .ThenBy(b => b.CustomerId)
                .ThenBy(b => b.CityId)
                .Skip(Paging.Skip(page, pageSize))
                .Take(pageSize)
                .ToListAsync(ct);

            var result = new BranchTransactionsPageDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalBlocks,
                TotalTransactions = totalTransactions,
            };
            if (pageBlocks.Count == 0)
                return result;

            // Posición de cada bloque en la página y su total, para ordenar y completar las filas.
            var position = pageBlocks
                .Select((b, index) => (Key: (b.CityId, b.CustomerId), Index: index, b.Count))
                .ToDictionary(x => x.Key, x => (x.Index, x.Count));
            var customerIds = pageBlocks.Select(b => b.CustomerId).Distinct().ToList();
            var cityIds = pageBlocks.Select(b => b.CityId).Distinct().ToList();

            var rows = await movements
                .Where(m => customerIds.Contains(m.Account.CustomerId) && cityIds.Contains(m.CityId))
                .Select(m => new BranchTransactionDto
                {
                    CityId = m.CityId,
                    City = m.City.Name,
                    CustomerId = m.Account.CustomerId,
                    DocumentNumber = m.Account.Customer.DocumentNumber,
                    FullName = m.Account.Customer.FullName,
                    MovementId = m.Id,
                    AccountNumber = m.Account.Number,
                    Type = m.Type,
                    Amount = m.Amount,
                    Description = m.Description,
                    CreatedAtUtc = m.CreatedAtUtc,
                })
                .ToListAsync(ct);

            // El filtro IN por cliente y por sede puede traer combinaciones de otra página: se descartan.
            result.Items = rows
                .Where(r => position.ContainsKey((r.CityId, r.CustomerId)))
                .Select(r =>
                {
                    r.CustomerTransactionCount = position[(r.CityId, r.CustomerId)].Count;
                    return r;
                })
                .OrderBy(r => position[(r.CityId, r.CustomerId)].Index)
                .ThenBy(r => r.CreatedAtUtc)
                .ThenBy(r => r.MovementId)
                .ToList();
            return result;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Se traduce a un SELECT con <c>GROUP BY</c> por cliente y <c>HAVING SUM(amount) &gt; minTotal</c>,
        /// más su COUNT para el total. Los filtros de fecha solo se agregan si se indicaron.
        /// </remarks>
        public async Task<PagedResult<OutOfCityWithdrawalsDto>> GetOutOfCityWithdrawalsAsync(
            decimal minTotal, DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
        {
            var withdrawals = _db.Movements.AsNoTracking()
                .Where(m => m.Type == MovementType.Retiro && m.CityId != m.Account.OriginCityId);

            if (fromUtc.HasValue)
                withdrawals = withdrawals.Where(m => m.CreatedAtUtc >= fromUtc.Value);
            if (toUtc.HasValue)
                withdrawals = withdrawals.Where(m => m.CreatedAtUtc < toUtc.Value);

            var customers = withdrawals
                .GroupBy(m => new { m.Account.CustomerId, m.Account.Customer.DocumentNumber, m.Account.Customer.FullName })
                .Select(g => new OutOfCityWithdrawalsDto
                {
                    CustomerId = g.Key.CustomerId,
                    DocumentNumber = g.Key.DocumentNumber,
                    FullName = g.Key.FullName,
                    TotalWithdrawn = g.Sum(m => m.Amount),
                    WithdrawalCount = g.Count(),
                    FirstTransactionAtUtc = g.Min(m => m.CreatedAtUtc),
                    LastTransactionAtUtc = g.Max(m => m.CreatedAtUtc),
                })
                .Where(r => r.TotalWithdrawn > minTotal);

            var total = await customers.CountAsync(ct);
            var items = await customers
                .OrderByDescending(r => r.TotalWithdrawn)
                .ThenBy(r => r.FullName)
                .ThenBy(r => r.CustomerId)
                .Skip(Paging.Skip(page, pageSize))
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<OutOfCityWithdrawalsDto> { Items = items, Page = page, PageSize = pageSize, TotalItems = total };
        }
    }
}
