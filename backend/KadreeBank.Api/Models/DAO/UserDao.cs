using System.Data;
using KadreeBank.Api.Data;
using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KadreeBank.Api.Models.DAO
{
    /// <summary>Implementación con Entity Framework Core del acceso a usuarios, sesiones y registro.</summary>
    public class UserDao : IUserDao
    {
        private readonly KadreeBankDbContext _db;

        /// <summary>Crea el DAO con sus dependencias (inyectadas por DI).</summary>
        /// <param name="db">Contexto de EF Core; uno por petición HTTP.</param>
        public UserDao(KadreeBankDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <inheritdoc/>
        public Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct)
        {
            var users = _db.Users.Include(u => u.Customer);
            return identifier.Any(char.IsLetter)
                ? users.FirstOrDefaultAsync(u => u.Username == identifier, ct)
                : users.FirstOrDefaultAsync(u => u.Customer != null && u.Customer.DocumentNumber == identifier, ct);
        }

        /// <inheritdoc/>
        public Task<User?> FindByIdAsync(long userId, CancellationToken ct) =>
            _db.Users.Include(u => u.Customer).FirstOrDefaultAsync(u => u.Id == userId, ct);

        /// <inheritdoc/>
        public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) =>
            _db.Users.AnyAsync(u => u.Username == username, ct);

        /// <inheritdoc/>
        public Task<bool> DocumentExistsAsync(string documentNumber, CancellationToken ct) =>
            _db.Customers.AnyAsync(c => c.DocumentNumber == documentNumber, ct);

        /// <inheritdoc/>
        public async Task<Account> RegisterAsync(Customer customer, int originCityId, string username, string pinHash, DateTime nowUtc, CancellationToken ct)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                _db.Customers.Add(customer);
                await _db.SaveChangesAsync(ct);

                var accountType = Customer.AllowedAccountType(customer.Type);
                var sequence = await _db.Database
                    .SqlQuery<long>($"SELECT nextval('account_number_seq') AS \"Value\"")
                    .SingleAsync(ct);
                var prefix = accountType == AccountType.Ahorros ? "AH" : "CC";
                var account = new Account(0, $"{prefix}-{sequence}", accountType, customer.Id, customer.Type, originCityId, 0m, nowUtc);
                _db.Accounts.Add(account);
                _db.Users.Add(new User(username, customer, pinHash, nowUtc));

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return account;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                _db.ChangeTracker.Clear();
                throw pg.ConstraintName == "uq_users_username"
                    ? new ConflictException("usuario_no_disponible", "username", "Ese nombre de usuario ya está en uso. Elige otro.")
                    : new ConflictException("documento_registrado", "documentNumber", "Ya existe un cliente con ese documento. Si es tuyo, ingresa con él.");
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        /// <inheritdoc/>
        public Task<List<User>> GetUsersWithPendingPinAsync(CancellationToken ct) =>
            _db.Users.Where(u => u.PinHash == User.PendingPinHash).ToListAsync(ct);

        /// <inheritdoc/>
        public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct) =>
            _db.RefreshTokens
                .Include(t => t.User).ThenInclude(u => u.Customer)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        /// <inheritdoc/>
        public void AddRefreshToken(RefreshToken token) => _db.RefreshTokens.Add(token);

        /// <inheritdoc/>
        public Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken ct) =>
            _db.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, nowUtc), ct);

        /// <inheritdoc/>
        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }
}
