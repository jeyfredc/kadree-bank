using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KadreeBank.IntegrationTests;

/// <summary>Pruebas de punta a punta por HTTP: sesión JWT, autorización por dueño y por rol.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ApiTests(PostgresFixture db)
{
    private async Task<(HttpClient Client, string Token)> LoginAsync(string identifier, string pin = PostgresFixture.DemoPin)
    {
        var client = db.Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { identifier, pin });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    [SkippableFact]
    public async Task Sin_token_responde_401()
    {
        db.SkipIfUnavailable();
        var response = await db.Factory.CreateClient().GetAsync("/api/accounts");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Un_cliente_solo_ve_sus_cuentas_y_no_las_ajenas()
    {
        db.SkipIfUnavailable();
        var me = await db.CreateIsolatedClientAsync(100m);
        var (client, _) = await LoginAsync(me.Username);

        var list = (await client.GetFromJsonAsync<JsonElement>("/api/accounts")).GetProperty("data");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/accounts/AH-1001")).StatusCode);
    }

    [SkippableFact]
    public async Task Retiro_sin_fondos_422_y_consignacion_201()
    {
        db.SkipIfUnavailable();
        var me = await db.CreateIsolatedClientAsync(100m);
        var (client, _) = await LoginAsync(me.Document);

        var tooMuch = await client.PostAsJsonAsync($"/api/accounts/{me.AccountNumber}/withdrawals", new { amount = 150, cityId = 1 });
        var deposit = await client.PostAsJsonAsync($"/api/accounts/{me.AccountNumber}/deposits", new { amount = 50, cityId = 1 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooMuch.StatusCode);
        Assert.Equal(HttpStatusCode.Created, deposit.StatusCode);
    }

    [SkippableFact]
    public async Task Reportes_solo_para_el_rol_Banco()
    {
        db.SkipIfUnavailable();
        var me = await db.CreateIsolatedClientAsync(0m);
        var (cliente, _) = await LoginAsync(me.Username);
        var (banco, _) = await LoginAsync("admin.banco");

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/reports/out-of-city-withdrawals")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await banco.GetAsync("/api/reports/out-of-city-withdrawals")).StatusCode);

        const string branch = "/api/reports/transactions-by-branch?year=2026&month=9";
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(branch)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await banco.GetAsync($"{branch}&cityId=1&type=Retiro")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await banco.GetAsync($"{branch}&type=Otro")).StatusCode);

        // Paginación por HTTP: la página trae items y totales; un tamaño fuera de rango es 400.
        var paged = (await banco.GetFromJsonAsync<JsonElement>($"{branch}&page=1&pageSize=2")).GetProperty("data");
        Assert.Equal(1, paged.GetProperty("page").GetInt32());
        Assert.Equal(2, paged.GetProperty("pageSize").GetInt32());
        Assert.True(paged.GetProperty("totalPages").GetInt32() >= 1);
        Assert.True(paged.GetProperty("totalTransactions").GetInt32() >= paged.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await banco.GetAsync($"{branch}&pageSize=500")).StatusCode);
    }

    [SkippableFact]
    public async Task El_registro_crea_cliente_cuenta_y_usuario_y_permite_ingresar()
    {
        db.SkipIfUnavailable();
        var suffix = Random.Shared.Next(10_000_000, 99_999_999).ToString();
        var request = new
        {
            customerType = "Natural",
            documentType = "CC",
            documentNumber = "10" + suffix,
            // Mismo nombre que los clientes aislados: los reportes contra la semilla los excluyen.
            fullName = "Cliente de prueba",
            email = $"reg{suffix}@example.com",
            phone = "3001234567",
            cityId = 3,
            username = $"reg.{suffix}",
            pin = "2580",
            pinConfirmation = "2580",
            acceptTerms = true,
        };
        var client = db.Factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/auth/register", request);
        var duplicated = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var data = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.StartsWith("AH-", data.GetProperty("accountNumber").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicated.StatusCode);

        var (session, _) = await LoginAsync(request.username, "2580");
        var list = (await session.GetFromJsonAsync<JsonElement>("/api/accounts")).GetProperty("data");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(0m, list[0].GetProperty("balance").GetDecimal());
    }

    [SkippableFact]
    public async Task Tres_intentos_fallidos_bloquean_el_acceso()
    {
        db.SkipIfUnavailable();
        var me = await db.CreateIsolatedClientAsync(0m);
        var client = db.Factory.CreateClient();
        HttpResponseMessage last = null!;
        foreach (var pin in new[] { "1111", "2222", "3333" })
            last = await client.PostAsJsonAsync("/api/auth/login", new { identifier = me.Username, pin });

        Assert.Equal((HttpStatusCode)423, last.StatusCode);
    }

    [SkippableFact]
    public async Task Refresh_con_cookie_renueva_y_logout_la_invalida()
    {
        db.SkipIfUnavailable();
        var me = await db.CreateIsolatedClientAsync(0m);
        var (client, _) = await LoginAsync(me.Username);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
        await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }
}
