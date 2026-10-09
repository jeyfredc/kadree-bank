using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace KadreeBank.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class ConcurrencyAgainstPostgresTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task Retiros_concurrentes_reales_nunca_sobregiran_ni_pierden_actualizaciones()
    {
        db.SkipIfUnavailable();
        const decimal initial = 1_000m;
        const decimal amount = 30m;
        var client = await db.CreateIsolatedClientAsync(initial);
        var number = client.AccountNumber;
        await using var services = db.CreateServices(TestUser.Cliente(client.CustomerId));

        // 60 retiros de $30 a la vez, cada uno con su propio DbContext/conexión (como peticiones HTTP distintas).
        var attempts = Enumerable.Range(0, 60).Select(_ => Task.Run(async () =>
        {
            await using var scope = services.CreateAsyncScope();
            var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
            try
            {
                await accounts.Withdraw(number, new OperationRequestDto { Amount = amount, CityId = 1 }, default);
                return true;
            }
            catch (InsufficientFundsException)
            {
                return false;
            }
        }));
        var results = await Task.WhenAll(attempts);

        var expectedSuccesses = (int)Math.Floor(initial / amount); // 33
        Assert.Equal(expectedSuccesses, results.Count(ok => ok));

        await using var context = db.CreateDbContext();
        var account = await context.Accounts.AsNoTracking().SingleAsync(a => a.Number == number);
        var movements = await context.Movements.AsNoTracking().Where(m => m.AccountId == account.Id).ToListAsync();

        Assert.Equal(initial - expectedSuccesses * amount, account.Balance);
        Assert.Equal(initial - account.Balance, movements.Sum(m => m.Amount));
        Assert.All(movements, m => Assert.True(m.BalanceAfter >= 0));
    }

    [SkippableFact]
    public async Task La_restriccion_CHECK_impide_saldo_negativo_aunque_se_salte_la_aplicacion()
    {
        db.SkipIfUnavailable();
        var number = (await db.CreateIsolatedClientAsync(10m)).AccountNumber;
        await using var context = db.CreateDbContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE accounts SET balance = balance - 11 WHERE number = {number}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_accounts_balance_non_negative", ex.ConstraintName);
    }

    [SkippableFact]
    public async Task Los_movimientos_son_inmutables()
    {
        db.SkipIfUnavailable();
        await using var context = db.CreateDbContext();

        await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE movements SET amount = 1 WHERE id = (SELECT MIN(id) FROM movements)"));
    }

    [SkippableFact]
    public async Task Solo_puede_existir_un_administrador()
    {
        db.SkipIfUnavailable();
        await using var context = db.CreateDbContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                "INSERT INTO users (username, customer_id, role, pin_hash) VALUES ('otro.admin', NULL, 'BANCO', 'PENDIENTE')"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class ReportsAgainstSeedTests(PostgresFixture db)
{
    [SkippableFact]
    public async Task Reporte_1_ordena_clientes_por_transacciones_de_septiembre()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var rows = (await reports.GetTransactionsByCustomer(2026, 9, 1, 100, default)).Data!.Items;
        var seeded = rows.Where(r => r.FullName != "Cliente de prueba").ToList();

        Assert.Equal(
            ["Ana María Gómez", "Comercializadora Andina S.A.S.", "Laura Restrepo", "Carlos Andrés Pérez", "Jorge Martínez", "Tecnologías del Caribe S.A."],
            seeded.Select(r => r.FullName));
        Assert.Equal([5, 5, 3, 2, 1, 1], seeded.Select(r => r.TransactionCount));
        Assert.True(rows.Zip(rows.Skip(1)).All(p => p.First.TransactionCount >= p.Second.TransactionCount));
    }

    [SkippableFact]
    public async Task Reporte_por_sede_filtra_por_ciudad_y_ordena_de_mayor_a_menor()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        // Sede Bogotá (id 1), septiembre: transacciones hechas en Bogotá.
        var bogota = (await reports.GetTransactionsByBranch(2026, 9, 1, null, 1, 100, default)).Data!.Items;

        // Una fila por transacción. Clientes por su total en la sede (3, 3, 1); cada uno en orden cronológico.
        static DateTime Utc(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);
        Assert.All(bogota, r => Assert.Equal("Bogotá", r.City));
        Assert.Equal(
            [
                ("Ana María Gómez", 3, "AH-1001", MovementType.Consignacion, 3_000_000m, Utc(9, 1, 13, 0)),
                ("Ana María Gómez", 3, "AH-1001", MovementType.Retiro, 200_000m, Utc(9, 20, 15, 0)),
                ("Ana María Gómez", 3, "AH-1006", MovementType.Retiro, 250_000m, Utc(9, 22, 14, 40)),
                ("Comercializadora Andina S.A.S.", 3, "CC-2001", MovementType.Consignacion, 5_000_000m, Utc(9, 2, 12, 30)),
                ("Comercializadora Andina S.A.S.", 3, "CC-2001", MovementType.Retiro, 800_000m, Utc(9, 23, 15, 0)),
                // Recaudo del 30-sep 22:30 en Bogotá (1-oct 03:30 UTC): cuenta en septiembre.
                ("Comercializadora Andina S.A.S.", 3, "CC-2001", MovementType.Consignacion, 4_200_000m, Utc(10, 1, 3, 30)),
                ("Laura Restrepo", 1, "AH-1003", MovementType.Retiro, 400_000m, Utc(9, 18, 20, 20)),
            ],
            bogota.Select(r => (r.FullName, r.CustomerTransactionCount, r.AccountNumber, r.Type, r.Amount, r.CreatedAtUtc)));
        Assert.Equal("Cajero", bogota[1].Description);
        Assert.Equal(bogota.Count, bogota.Select(r => r.MovementId).Distinct().Count());
    }

    [SkippableFact]
    public async Task Reporte_1_incluye_el_rango_de_fechas_y_null_si_no_hubo_transacciones()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var september = (await reports.GetTransactionsByCustomer(2026, 9, 1, 100, default)).Data!.Items;
        var october = (await reports.GetTransactionsByCustomer(2026, 10, 1, 100, default)).Data!.Items;

        var ana = september.Single(r => r.FullName == "Ana María Gómez");
        Assert.Equal(new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc), ana.FirstTransactionAtUtc);
        Assert.True(ana.LastTransactionAtUtc > ana.FirstTransactionAtUtc);

        // Laura no tiene movimientos en octubre en la semilla: sin fechas.
        var laura = october.Single(r => r.FullName == "Laura Restrepo");
        Assert.Equal(0, laura.TransactionCount);
        Assert.Null(laura.FirstTransactionAtUtc);
        Assert.Null(laura.LastTransactionAtUtc);
    }

    [SkippableFact]
    public async Task Reporte_por_sede_de_todas_las_ciudades_cuadra_con_el_reporte_1_y_filtra_por_tipo()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var all = (await reports.GetTransactionsByBranch(2026, 9, null, null, 1, 100, default)).Data!.Items;
        var withdrawals = (await reports.GetTransactionsByBranch(2026, 9, null, MovementType.Retiro, 1, 100, default)).Data!.Items;
        var byCustomer = (await reports.GetTransactionsByCustomer(2026, 9, 1, 100, default)).Data!.Items;

        // Todas las sedes: una fila por transacción, así que hay tantas filas como transacciones del reporte 1.
        Assert.Equal(byCustomer.Sum(r => r.TransactionCount), all.Count);
        Assert.True(all.Zip(all.Skip(1)).All(p => p.First.CustomerTransactionCount >= p.Second.CustomerTransactionCount));
        Assert.All(all.GroupBy(r => (r.CityId, r.CustomerId)),
            g => Assert.All(g, r => Assert.Equal(g.Count(), r.CustomerTransactionCount)));

        // Solo retiros: todas las filas son retiros; empates de 2 ordenados por sede (Barranquilla, Bogotá).
        Assert.All(withdrawals, r => Assert.Equal(MovementType.Retiro, r.Type));
        Assert.Equal(
            [
                ("Barranquilla", "Comercializadora Andina S.A.S.", 2),
                ("Barranquilla", "Comercializadora Andina S.A.S.", 2),
                ("Bogotá", "Ana María Gómez", 2),
                ("Bogotá", "Ana María Gómez", 2),
            ],
            withdrawals.Take(4).Select(r => (r.City, r.FullName, r.CustomerTransactionCount)));
    }

    [SkippableFact]
    public async Task Reporte_por_sede_rechaza_una_sede_inexistente()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        await Assert.ThrowsAsync<RequestValidationException>(() => reports.GetTransactionsByBranch(2026, 9, 999, null, 1, 20, default));
    }

    [SkippableFact]
    public async Task Reporte_por_sede_pagina_por_cliente_sin_partir_bloques()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var full = (await reports.GetTransactionsByBranch(2026, 9, null, null, 1, 100, default)).Data!;
        var first = (await reports.GetTransactionsByBranch(2026, 9, null, null, 1, 2, default)).Data!;

        // Totales del filtro completo, iguales en cualquier página.
        Assert.Equal(full.TotalItems, first.TotalItems);
        Assert.Equal(full.Items.Count, first.TotalTransactions);
        Assert.Equal((int)Math.Ceiling(full.TotalItems / 2.0), first.TotalPages);

        // La página 1 trae los 2 primeros clientes con TODAS sus transacciones.
        Assert.Equal(2, first.Items.Select(r => (r.CityId, r.CustomerId)).Distinct().Count());
        Assert.All(first.Items.GroupBy(r => (r.CityId, r.CustomerId)), g => Assert.Equal(g.First().CustomerTransactionCount, g.Count()));

        // Recorrer todas las páginas reproduce exactamente el reporte completo, en el mismo orden.
        var concatenated = new List<long>();
        for (var page = 1; page <= first.TotalPages; page++)
            concatenated.AddRange((await reports.GetTransactionsByBranch(2026, 9, null, null, page, 2, default)).Data!.Items.Select(r => r.MovementId));
        Assert.Equal(full.Items.Select(r => r.MovementId), concatenated);

        // Una página fuera de rango llega vacía, con los totales.
        var beyond = (await reports.GetTransactionsByBranch(2026, 9, null, null, first.TotalPages + 1, 2, default)).Data!;
        Assert.Empty(beyond.Items);
        Assert.Equal(full.TotalItems, beyond.TotalItems);
    }

    [SkippableFact]
    public async Task Reportes_1_y_2_paginan_y_validan_los_parametros()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var customers = (await reports.GetTransactionsByCustomer(2026, 9, 1, 2, default)).Data!;
        Assert.Equal(["Ana María Gómez", "Comercializadora Andina S.A.S."], customers.Items.Select(r => r.FullName));
        Assert.True(customers.TotalItems >= 6);

        var withdrawals = (await reports.GetOutOfCityWithdrawals(null, null, null, 2, 2, default)).Data!;
        Assert.Equal(3, withdrawals.TotalItems);
        Assert.Equal(2, withdrawals.TotalPages);
        Assert.Equal("Ana María Gómez", Assert.Single(withdrawals.Items).FullName);

        await Assert.ThrowsAsync<RequestValidationException>(() => reports.GetTransactionsByCustomer(2026, 9, 0, 20, default));
        await Assert.ThrowsAsync<RequestValidationException>(() => reports.GetTransactionsByCustomer(2026, 9, 1, 101, default));
        await Assert.ThrowsAsync<RequestValidationException>(() => reports.GetOutOfCityWithdrawals(null, null, null, 1, 0, default));
    }

    [SkippableFact]
    public async Task Reporte_2_lista_solo_clientes_que_superan_un_millon_fuera_de_su_ciudad()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        var rows = (await reports.GetOutOfCityWithdrawals(null, null, null, 1, 100, default)).Data!.Items;

        Assert.Equal(["Comercializadora Andina S.A.S.", "Carlos Andrés Pérez", "Ana María Gómez"], rows.Select(r => r.FullName));
        Assert.Equal([3_500_000m, 1_650_000m, 1_150_000m], rows.Select(r => r.TotalWithdrawn));
        Assert.Equal([2, 2, 3], rows.Select(r => r.WithdrawalCount));
        Assert.All(rows, r => Assert.True(r.FirstTransactionAtUtc <= r.LastTransactionAtUtc));
        // Jorge retiró exactamente $1.000.000 fuera de su ciudad: no "supera" el umbral.
        Assert.DoesNotContain(rows, r => r.FullName == "Jorge Martínez");
    }

    [SkippableFact]
    public async Task Reporte_2_respeta_el_rango_de_fechas()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var reports = scope.ServiceProvider.GetRequiredService<IReportService>();

        // Solo septiembre: Carlos baja a $450.000 (su retiro de $1.200.000 fue en agosto).
        var rows = (await reports.GetOutOfCityWithdrawals(0m, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), 1, 100, default)).Data!.Items;

        Assert.Equal(450_000m, rows.Single(r => r.FullName == "Carlos Andrés Pérez").TotalWithdrawn);
    }

    [SkippableFact]
    public async Task El_extracto_usa_la_hora_de_Bogota_para_delimitar_el_mes()
    {
        db.SkipIfUnavailable();
        await using var services = db.CreateServices(TestUser.Banco());
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var statement = (await accounts.GetMonthlyStatement("CC-2001", 2026, 9, default)).Data!;

        // El recaudo del 30-sep a las 22:30 (03:30 UTC del 1-oct) pertenece a septiembre.
        Assert.Equal(20_000_000m, statement.OpeningBalance);
        Assert.Equal(9_200_000m, statement.TotalDeposits);
        Assert.Equal(4_300_000m, statement.TotalWithdrawals);
        Assert.Equal(24_900_000m, statement.ClosingBalance);
        Assert.Equal(5, statement.Movements.Count);
        Assert.Equal(MovementType.Consignacion, statement.Movements[^1].Type);
    }
}
