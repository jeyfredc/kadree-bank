using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.Entities;
using KadreeBank.Api.Services;

namespace KadreeBank.UnitTests;

public class BankCalendarTests
{
    private static BankCalendar Calendar(DateTimeOffset now) =>
        new(new BankOptions { TimeZone = "America/Bogota" }, new FixedTimeProvider(now));

    [Fact]
    public void El_mes_se_delimita_en_hora_de_Bogota()
    {
        var (from, to) = Calendar(DateTimeOffset.UtcNow).MonthRangeUtc(2026, 9);

        Assert.Equal(new DateTime(2026, 9, 1, 5, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc), to);
    }

    [Fact]
    public void El_mes_actual_se_calcula_en_hora_local()
    {
        // 1 de octubre 03:00 UTC = 30 de septiembre 22:00 en Bogotá.
        var calendar = Calendar(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal((2026, 9), calendar.CurrentMonth());
        Assert.True(calendar.IsFutureMonth(2026, 10));
    }

    [Theory]
    [InlineData(2026, 11)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public void Meses_futuros_o_invalidos_se_rechazan(int year, int month)
    {
        var calendar = Calendar(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

        Assert.Throws<RequestValidationException>(() => calendar.ValidateMonth(year, month));
    }
}

public class StatementTests
{
    /// <summary>DAO de solo lectura con datos fijos para probar el cálculo del extracto.</summary>
    private sealed class StubAccountDao(decimal opening, params MovementDto[] movements) : IAccountDao
    {
        public Task<AccountSummaryDto?> GetSummaryAsync(string accountNumber, long? customerId, CancellationToken ct) =>
            Task.FromResult<AccountSummaryDto?>(new AccountSummaryDto { Number = accountNumber, CustomerName = "Ana", Type = AccountType.Ahorros });
        public Task<decimal> GetBalanceBeforeAsync(string accountNumber, DateTime beforeUtc, CancellationToken ct) => Task.FromResult(opening);
        public Task<List<MovementDto>> GetMovementsInRangeAsync(string accountNumber, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
            Task.FromResult(movements.ToList());

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => throw new NotSupportedException();
        public Task<Account?> GetByNumberForUpdateAsync(string accountNumber, CancellationToken ct) => throw new NotSupportedException();
        public void AddMovement(Movement movement) => throw new NotSupportedException();
        public Task<List<AccountSummaryDto>> GetAllAsync(long? customerId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<MovementDto>> GetRecentMovementsAsync(string accountNumber, int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private static AccountService Service(IAccountDao dao)
    {
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));
        return new AccountService(dao, new InMemoryBank(), new BankCalendar(new BankOptions(), time), time, new FakeCurrentUser());
    }

    private static MovementDto Mov(MovementType type, decimal amount, decimal after) => new()
    {
        Id = 1, Type = type, Amount = amount, City = "Bogotá", BalanceAfter = after,
        CreatedAtUtc = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task El_extracto_calcula_saldo_inicial_totales_y_saldo_final()
    {
        var dao = new StubAccountDao(1_000m,
            Mov(MovementType.Consignacion, 500m, 1_500m),
            Mov(MovementType.Retiro, 200m, 1_300m),
            Mov(MovementType.Retiro, 300m, 1_000m));

        var response = await Service(dao).GetMonthlyStatement("AH-1", 2026, 9, default);
        var statement = response.Data!;

        Assert.True(response.Success);
        Assert.Equal(1_000m, statement.OpeningBalance);
        Assert.Equal(500m, statement.TotalDeposits);
        Assert.Equal(500m, statement.TotalWithdrawals);
        Assert.Equal(1_000m, statement.ClosingBalance);
        Assert.Equal(3, statement.Movements.Count);
    }

    [Fact]
    public async Task Un_mes_sin_movimientos_cierra_con_el_saldo_inicial()
    {
        var response = await Service(new StubAccountDao(750m)).GetMonthlyStatement("AH-1", 2026, 8, default);

        Assert.Equal(750m, response.Data!.ClosingBalance);
        Assert.Empty(response.Data.Movements);
    }

    [Fact]
    public async Task Un_mes_futuro_se_rechaza()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            Service(new StubAccountDao(0m)).GetMonthlyStatement("AH-1", 2026, 11, default));
    }
}
