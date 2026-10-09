using System.Collections.Concurrent;
using System.Reflection;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.DTOs.Cities;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.UnitTests;

/// <summary>
/// DAO en memoria que imita el comportamiento relevante de PostgreSQL:
/// - cada transacción trabaja sobre una COPIA de la fila (como EF Core),
/// - GetByNumberForUpdateAsync toma un bloqueo exclusivo por cuenta que se libera al terminar
///   la transacción (como SELECT ... FOR UPDATE),
/// - los cambios solo se publican al confirmar; si la transacción falla, se descartan.
/// Con <see cref="UseRowLocks"/> = false se reproduce la condición de carrera clásica
/// (leer-modificar-escribir sin bloqueo) para demostrar que la prueba no es trivial.
/// </summary>
internal sealed class InMemoryBank : IAccountDao, ICityDao
{
    private sealed record AccountRow(long Id, string Number, AccountType Type, long CustomerId,
        CustomerType CustomerType, int OriginCityId, decimal Balance, DateTime OpenedAtUtc);

    private sealed class Transaction
    {
        public List<SemaphoreSlim> HeldLocks { get; } = [];
        public List<Account> Loaded { get; } = [];
        public List<Movement> PendingMovements { get; } = [];
    }

    private static readonly PropertyInfo MovementId = typeof(Movement).GetProperty(nameof(Movement.Id))!;

    private readonly ConcurrentDictionary<string, AccountRow> _accounts = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _rowLocks = new();
    private readonly Dictionary<int, string> _cities = new() { [1] = "Bogotá", [2] = "Medellín" };
    private readonly ConcurrentQueue<Movement> _committedMovements = new();
    private readonly AsyncLocal<Transaction?> _current = new();
    private readonly object _commitGate = new();
    private long _nextMovementId;

    public bool UseRowLocks { get; init; } = true;

    /// <summary>Se invoca justo después de leer la cuenta; permite forzar intercalados en las pruebas.</summary>
    public Func<Task>? AfterRead { get; init; }

    public IReadOnlyList<Movement> CommittedMovements => [.. _committedMovements.OrderBy(m => m.Id)];

    public void SeedAccount(string number, decimal balance, AccountType type = AccountType.Ahorros, long customerId = FakeCurrentUser.DefaultCustomerId)
    {
        var customerType = type == AccountType.Ahorros ? CustomerType.Natural : CustomerType.Juridica;
        _accounts[number] = new AccountRow(_accounts.Count + 1, number, type, customerId, customerType, 1, balance, DateTime.UtcNow);
    }

    public decimal BalanceOf(string number) => _accounts[number].Balance;

    // ---------------------------------------------------------------- ICityDao

    public Task<string?> GetNameAsync(int cityId, CancellationToken ct) =>
        Task.FromResult(_cities.GetValueOrDefault(cityId));

    public Task<List<CityDto>> GetAllAsync(CancellationToken ct) =>
        Task.FromResult(_cities.Select(c => new CityDto { Id = c.Key, Name = c.Value }).ToList());

    // ---------------------------------------------------------------- IAccountDao (escrituras)

    public async Task<Account?> GetByNumberForUpdateAsync(string accountNumber, CancellationToken ct)
    {
        var tx = _current.Value ?? throw new InvalidOperationException("GetByNumberForUpdateAsync requiere una transacción activa.");

        if (UseRowLocks)
        {
            var rowLock = _rowLocks.GetOrAdd(accountNumber, _ => new SemaphoreSlim(1, 1));
            await rowLock.WaitAsync(ct);
            tx.HeldLocks.Add(rowLock);
        }

        // Simula la latencia de ida y vuelta a la base de datos.
        await Task.Yield();

        if (!_accounts.TryGetValue(accountNumber, out var row))
            return null;

        var account = new Account(row.Id, row.Number, row.Type, row.CustomerId, row.CustomerType,
            row.OriginCityId, row.Balance, row.OpenedAtUtc);
        tx.Loaded.Add(account);

        if (AfterRead is not null)
            await AfterRead();

        return account;
    }

    public void AddMovement(Movement movement)
    {
        var tx = _current.Value ?? throw new InvalidOperationException("AddMovement requiere una transacción activa.");
        tx.PendingMovements.Add(movement);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var tx = new Transaction();
        _current.Value = tx;
        try
        {
            var result = await work(ct);

            lock (_commitGate)
            {
                foreach (var account in tx.Loaded)
                    _accounts[account.Number] = _accounts[account.Number] with { Balance = account.Balance };

                foreach (var movement in tx.PendingMovements)
                {
                    MovementId.SetValue(movement, ++_nextMovementId);
                    _committedMovements.Enqueue(movement);
                }
            }

            return result;
        }
        finally
        {
            foreach (var rowLock in tx.HeldLocks)
                rowLock.Release();
            _current.Value = null;
        }
    }

    // ---------------------------------------------------------------- IAccountDao (lecturas: no se usan aquí)

    // Explícita: ICityDao también tiene un GetAllAsync con otro tipo de retorno.
    Task<List<AccountSummaryDto>> IAccountDao.GetAllAsync(long? customerId, CancellationToken ct) => throw new NotSupportedException();
    public Task<AccountSummaryDto?> GetSummaryAsync(string accountNumber, long? customerId, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<MovementDto>> GetRecentMovementsAsync(string accountNumber, int limit, CancellationToken ct) => throw new NotSupportedException();
    public Task<decimal> GetBalanceBeforeAsync(string accountNumber, DateTime beforeUtc, CancellationToken ct) => throw new NotSupportedException();
    public Task<List<MovementDto>> GetMovementsInRangeAsync(string accountNumber, DateTime fromUtc, DateTime toUtc, CancellationToken ct) => throw new NotSupportedException();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>Usuario de la petición para las pruebas: por defecto, el cliente dueño de las cuentas sembradas.</summary>
internal sealed class FakeCurrentUser : ICurrentUser
{
    public const long DefaultCustomerId = 1;

    public bool IsAuthenticated { get; init; } = true;
    public long? UserId { get; init; } = 10;
    public long? CustomerId { get; init; } = DefaultCustomerId;
    public UserRole? Role { get; init; } = UserRole.Cliente;
    public bool IsBanco => Role == UserRole.Banco;

    public static FakeCurrentUser Banco() => new() { UserId = 99, CustomerId = null, Role = UserRole.Banco };
}
