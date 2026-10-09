using System.Data;
using KadreeBank.Api.Data;
using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KadreeBank.Api.Models.DAO
{
    /// <summary>
    /// Implementación con Entity Framework Core del acceso a cuentas y movimientos.
    /// Las escrituras usan bloqueo pesimista de fila; las lecturas usan proyecciones sin seguimiento.
    /// </summary>
    public class AccountDao : IAccountDao
    {
        /// <summary>Tiempo máximo que una operación espera el bloqueo de una cuenta antes de responder 409.</summary>
        public const int LockTimeoutMs = 5_000;

        /// <summary>Nombre del CHECK de db/01_schema.sql que impide saldos negativos.</summary>
        private const string BalanceCheckConstraint = "ck_accounts_balance_non_negative";

        private readonly KadreeBankDbContext _db;
        private readonly ILogger<AccountDao> _logger;

        /// <summary>Crea el DAO con sus dependencias (inyectadas por DI).</summary>
        /// <param name="db">Contexto de EF Core; uno por petición HTTP.</param>
        /// <param name="logger">Registra los tiempos de espera agotados en los bloqueos.</param>
        public AccountDao(KadreeBankDbContext db, ILogger<AccountDao> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ---------------------------------------------------------------- Escrituras

        /// <inheritdoc/>
        public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            // READ COMMITTED basta: la consistencia la da el bloqueo de fila, no el nivel de aislamiento.
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                await _db.Database.ExecuteSqlRawAsync($"SET LOCAL lock_timeout = '{LockTimeoutMs}ms'", ct);

                var result = await work(ct);
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception ex)
            {
                // Al salir del using la transacción hace ROLLBACK y libera los bloqueos.
                _db.ChangeTracker.Clear();
                throw Translate(ex);
            }
        }

        /// <inheritdoc/>
        public async Task<Account?> GetByNumberForUpdateAsync(string accountNumber, CancellationToken ct)
        {
            if (_db.Database.CurrentTransaction is null)
                throw new InvalidOperationException("El bloqueo de cuenta requiere una transacción activa.");

            // LINQ no tiene una forma de pedir bloqueos de fila; FromSql es la API de EF Core para eso.
            // FOR UPDATE bloquea la fila hasta COMMIT/ROLLBACK: otra operación sobre la misma cuenta
            // espera y, cuando entra, lee el saldo ya actualizado.
            // ToListAsync (y no SingleAsync) evita que EF envuelva la consulta en una subconsulta.
            var rows = await _db.Accounts
                .FromSql($"SELECT * FROM accounts WHERE number = {accountNumber} FOR UPDATE")
                .ToListAsync(ct);
            return rows.SingleOrDefault();
        }

        /// <inheritdoc/>
        public void AddMovement(Movement movement) => _db.Movements.Add(movement);

        // ---------------------------------------------------------------- Lecturas (sin seguimiento)

        /// <inheritdoc/>
        public Task<List<AccountSummaryDto>> GetAllAsync(long? customerId, CancellationToken ct) =>
            Summaries(OwnedBy(customerId)).OrderBy(a => a.Number).ToListAsync(ct);

        /// <inheritdoc/>
        public Task<AccountSummaryDto?> GetSummaryAsync(string accountNumber, long? customerId, CancellationToken ct) =>
            Summaries(OwnedBy(customerId).Where(a => a.Number == accountNumber)).FirstOrDefaultAsync(ct);

        /// <summary>Cuentas de un cliente, o todas si <paramref name="customerId"/> es null.</summary>
        private IQueryable<Account> OwnedBy(long? customerId) =>
            customerId is null ? _db.Accounts : _db.Accounts.Where(a => a.CustomerId == customerId);

        /// <inheritdoc/>
        public Task<List<MovementDto>> GetRecentMovementsAsync(string accountNumber, int limit, CancellationToken ct) =>
            MovementsOf(accountNumber)
                .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
                .Take(limit)
                .Select(ToDto)
                .ToListAsync(ct);

        /// <inheritdoc/>
        public async Task<decimal> GetBalanceBeforeAsync(string accountNumber, DateTime beforeUtc, CancellationToken ct) =>
            await MovementsOf(accountNumber)
                .Where(m => m.CreatedAtUtc < beforeUtc)
                .OrderByDescending(m => m.CreatedAtUtc).ThenByDescending(m => m.Id)
                .Select(m => (decimal?)m.BalanceAfter)
                .FirstOrDefaultAsync(ct) ?? 0m;

        /// <inheritdoc/>
        public Task<List<MovementDto>> GetMovementsInRangeAsync(string accountNumber, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            MovementsOf(accountNumber)
                .Where(m => m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc < toUtc)
                .OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)
                .Select(ToDto)
                .ToListAsync(ct);

        // ---------------------------------------------------------------- Auxiliares

        /// <summary>Proyecta cuentas a <see cref="AccountSummaryDto"/> con el nombre del titular y de la ciudad de origen.</summary>
        /// <param name="accounts">Consulta de cuentas, ya filtrada si hace falta.</param>
        /// <returns>Consulta sin ejecutar; EF la traduce a un solo SELECT con JOIN.</returns>
        private static IQueryable<AccountSummaryDto> Summaries(IQueryable<Account> accounts) =>
            accounts.AsNoTracking().Select(a => new AccountSummaryDto
            {
                Number = a.Number,
                Type = a.Type,
                CustomerDocument = a.Customer.DocumentNumber,
                CustomerName = a.Customer.FullName,
                OriginCity = a.OriginCity.Name,
                Balance = a.Balance,
            });

        /// <summary>Movimientos de una cuenta, sin seguimiento de cambios.</summary>
        /// <param name="accountNumber">Número de cuenta.</param>
        /// <returns>Consulta sin ejecutar, lista para ordenar y paginar.</returns>
        private IQueryable<Movement> MovementsOf(string accountNumber) =>
            _db.Movements.AsNoTracking().Where(m => m.Account.Number == accountNumber);

        /// <summary>
        /// Proyección de <see cref="Movement"/> a <see cref="MovementDto"/>. Es una expresión (no un método)
        /// para que EF la traduzca a SQL y solo traiga las columnas necesarias.
        /// </summary>
        private static readonly System.Linq.Expressions.Expression<Func<Movement, MovementDto>> ToDto = m => new MovementDto
        {
            Id = m.Id,
            Type = m.Type,
            Amount = m.Amount,
            City = m.City.Name,
            BalanceAfter = m.BalanceAfter,
            Description = m.Description,
            CreatedAtUtc = m.CreatedAtUtc,
        };

        /// <summary>
        /// Convierte los errores de PostgreSQL que tienen significado de negocio en excepciones de la API:
        /// tiempo de bloqueo agotado → <see cref="ConcurrencyConflictException"/> (409) y
        /// violación del CHECK de saldo → <see cref="BusinessRuleException"/> (422).
        /// </summary>
        /// <param name="ex">Excepción original.</param>
        /// <returns>La excepción traducida, o la original si no es un caso conocido.</returns>
        private Exception Translate(Exception ex)
        {
            var pg = FindPostgresException(ex);
            if (pg?.SqlState == PostgresErrorCodes.LockNotAvailable)
            {
                _logger.LogWarning("Tiempo de espera agotado esperando el bloqueo de una cuenta");
                return new ConcurrencyConflictException("La cuenta está siendo usada por otra operación. Intente de nuevo.", pg);
            }
            if (pg?.SqlState == PostgresErrorCodes.CheckViolation && pg.ConstraintName == BalanceCheckConstraint)
            {
                // Defensa en profundidad: si alguna ruta de código saltara la validación de la entidad,
                // el CHECK de la base de datos igual impide el saldo negativo.
                return new BusinessRuleException("saldo_insuficiente", "La operación dejaría la cuenta con saldo negativo.");
            }
            return ex;
        }

        /// <summary>Busca una <see cref="PostgresException"/> en la cadena de excepciones internas (EF Core la envuelve).</summary>
        /// <param name="ex">Excepción a revisar.</param>
        /// <returns>La excepción de PostgreSQL, o null si no hay.</returns>
        private static PostgresException? FindPostgresException(Exception? ex)
        {
            for (; ex is not null; ex = ex.InnerException)
                if (ex is PostgresException pg) return pg;
            return null;
        }
    }
}
