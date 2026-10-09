using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.UnitTests;

/// <summary>
/// Pruebas de concurrencia del servicio. Verifican que AccountService aplica el patrón
/// "bloquear fila -> leer -> validar -> escribir" dentro de una sola transacción.
/// La misma garantía se prueba contra PostgreSQL real en KadreeBank.IntegrationTests.
/// </summary>
public class ConcurrencyTests
{
    private static OperationRequestDto Request(decimal amount) => new() { Amount = amount, CityId = 1 };

    [Fact]
    public async Task Sin_bloqueo_dos_retiros_simultaneos_sobregiran_la_cuenta()
    {
        // Control: demuestra que el escenario de carrera es real cuando no hay bloqueo.
        var bothRead = new TaskCompletionSource();
        var readers = 0;
        var bank = new InMemoryBank
        {
            UseRowLocks = false,
            AfterRead = async () =>
            {
                if (Interlocked.Increment(ref readers) == 2) bothRead.SetResult();
                await bothRead.Task;
            },
        };
        bank.SeedAccount("AH-1", 100m);
        var service = AccountServiceTests.CreateService(bank, TimeProvider.System);

        await Task.WhenAll(
            service.Withdraw("AH-1", Request(80m), default),
            service.Withdraw("AH-1", Request(80m), default));

        // Ambos retiros "pasaron" la validación con el saldo viejo: se entregaron $160 de $100.
        Assert.Equal(2, bank.CommittedMovements.Count);
        Assert.Equal(160m, bank.CommittedMovements.Sum(m => m.Amount));
    }

    [Fact]
    public async Task Con_bloqueo_de_fila_solo_uno_de_dos_retiros_simultaneos_se_aplica()
    {
        var bank = new InMemoryBank { AfterRead = () => Task.Delay(20) };
        bank.SeedAccount("AH-1", 100m);
        var service = AccountServiceTests.CreateService(bank, TimeProvider.System);

        var first = service.Withdraw("AH-1", Request(80m), default);
        var second = service.Withdraw("AH-1", Request(80m), default);
        var outcomes = await Task.WhenAll(Capture(first), Capture(second));

        Assert.Single(outcomes, o => o is null);
        Assert.Single(outcomes, o => o is InsufficientFundsException);
        Assert.Equal(20m, bank.BalanceOf("AH-1"));
    }

    [Fact]
    public async Task Cientos_de_operaciones_concurrentes_mantienen_el_saldo_consistente_y_nunca_negativo()
    {
        const decimal initial = 500m;
        const decimal amount = 10m;
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", initial);
        var service = AccountServiceTests.CreateService(bank, TimeProvider.System);

        // 150 retiros y 50 consignaciones de $10 lanzados a la vez sobre la misma cuenta.
        var operations = Enumerable.Range(0, 200)
            .Select(i => Task.Run(async () =>
            {
                var isDeposit = i % 4 == 0;
                try
                {
                    await (isDeposit
                        ? service.Deposit("AH-1", Request(amount), default)
                        : service.Withdraw("AH-1", Request(amount), default));
                    return (isDeposit, ok: true);
                }
                catch (InsufficientFundsException)
                {
                    return (isDeposit, ok: false);
                }
            }))
            .ToArray();
        var results = await Task.WhenAll(operations);

        var deposits = results.Count(r => r.isDeposit && r.ok);
        var withdrawals = results.Count(r => !r.isDeposit && r.ok);
        var finalBalance = bank.BalanceOf("AH-1");

        Assert.Equal(50, deposits);
        Assert.Equal(initial + deposits * amount - withdrawals * amount, finalBalance);
        Assert.True(finalBalance >= 0);
        Assert.Equal(deposits + withdrawals, bank.CommittedMovements.Count);

        // La cadena de saldos debe ser coherente movimiento a movimiento (sin actualizaciones perdidas).
        var running = initial;
        foreach (var movement in bank.CommittedMovements)
        {
            running += movement.Type == MovementType.Consignacion ? movement.Amount : -movement.Amount;
            Assert.Equal(running, movement.BalanceAfter);
            Assert.True(movement.BalanceAfter >= 0);
        }
        Assert.Equal(finalBalance, running);
    }

    private static async Task<Exception?> Capture(Task task)
    {
        try { await task; return null; }
        catch (Exception ex) { return ex; }
    }
}
