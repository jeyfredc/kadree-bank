using KadreeBank.Api.Data;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DAO;
using KadreeBank.Api.Models.Entities;
using KadreeBank.Api.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace KadreeBank.IntegrationTests;

/// <summary>
/// Levanta un PostgreSQL desechable con Testcontainers y le aplica los mismos scripts
/// de db/ que usa docker-compose. Si Docker no está disponible, las pruebas se omiten.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string DemoPin = "2580";
    private const string JwtKey = "clave-jwt-de-integracion-con-mas-de-32-bytes!";
    private const string PinPepper = "pepper-de-integracion-con-mas-de-32-caracteres";

    private PostgreSqlContainer? _container;
    private WebApplicationFactory<Program>? _factory;

    public string ConnectionString { get; private set; } = string.Empty;
    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            SkipReason = $"Docker no está disponible para las pruebas de integración: {ex.Message}";
            return;
        }

        ConnectionString = _container.GetConnectionString();
        var dbDir = FindDbDirectory();
        await using (var db = CreateDbContext())
        {
            foreach (var script in new[] { "01_schema.sql", "02_seed.sql" })
            {
                // ExecuteSqlRaw interpreta { } como marcadores de formato; el esquema tiene regex como {6,20}.
                var sql = (await File.ReadAllTextAsync(Path.Combine(dbDir, script))).Replace("{", "{{").Replace("}", "}}");
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // Configuración que lee la API al arrancar en WebApplicationFactory.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Auth__PinPepper", PinPepper);
        Environment.SetEnvironmentVariable("Auth__DemoPin", DemoPin);
        Environment.SetEnvironmentVariable("Auth__RefreshCookieSecure", "false"); // TestServer usa http://localhost
        Environment.SetEnvironmentVariable("RateLimit__AuthPermitLimit", "10000");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    public void SkipIfUnavailable() => Skip.If(SkipReason is not null, SkipReason);

    /// <summary>La API en memoria (una sola instancia por ejecución). Al arrancar asigna DemoPin a los usuarios semilla.</summary>
    public WebApplicationFactory<Program> Factory => _factory ??= new WebApplicationFactory<Program>();

    public AuthSettings AuthSettings => new() { PinPepper = PinPepper };

    public KadreeBankDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<KadreeBankDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Mismo registro de dependencias que Program.cs, sin la capa web, con el usuario indicado.</summary>
    public ServiceProvider CreateServices(ICurrentUser currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<KadreeBankDbContext>(o => o.UseNpgsql(ConnectionString));
        services.AddSingleton(new BankOptions());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BankCalendar>();
        services.AddSingleton(currentUser);
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IAccountDao, AccountDao>();
        services.AddScoped<ICityDao, CityDao>();
        services.AddScoped<IReportDao, ReportDao>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Crea un cliente, su cuenta de ahorros y su usuario (contraseña <see cref="DemoPin"/>), aislados
    /// para que la prueba no altere los datos semilla.
    /// </summary>
    public async Task<IsolatedClient> CreateIsolatedClientAsync(decimal balance)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var number = $"IT-{suffix}";
        var document = string.Concat(suffix.Select(c => (char)('0' + c % 10)));
        var username = "it" + new string(suffix.Where(char.IsLetterOrDigit).ToArray())[..10].ToLowerInvariant() + "x";
        var pinHash = new PinHasher(AuthSettings).Hash(DemoPin);

        await using var db = CreateDbContext();
        var customerId = await db.Database.SqlQuery<long>($"""
            INSERT INTO customers (document_number, full_name, customer_type, document_type)
            VALUES ({document}, 'Cliente de prueba', 'NATURAL', 'CC')
            RETURNING id AS "Value"
            """).ToListAsync();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO accounts (number, account_type, customer_id, customer_type, origin_city_id, balance)
            VALUES ({number}, 'AHORROS', {customerId[0]}, 'NATURAL', 1, {balance})
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (username, customer_id, role, pin_hash) VALUES ({username}, {customerId[0]}, 'CLIENTE', {pinHash})
            """);
        return new IsolatedClient(number, customerId[0], username, document);
    }

    private static string FindDbDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "db");
            if (File.Exists(Path.Combine(candidate, "01_schema.sql"))) return candidate;
        }
        throw new DirectoryNotFoundException("No se encontró la carpeta db/ con los scripts SQL.");
    }
}

public sealed record IsolatedClient(string AccountNumber, long CustomerId, string Username, string Document);

/// <summary>Usuario fijo para las pruebas que llaman a los servicios sin pasar por HTTP.</summary>
public sealed class TestUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public long? UserId { get; init; }
    public long? CustomerId { get; init; }
    public UserRole? Role { get; init; } = UserRole.Cliente;
    public bool IsBanco => Role == UserRole.Banco;

    public static TestUser Cliente(long customerId) => new() { UserId = null, CustomerId = customerId };
    public static TestUser Banco() => new() { Role = UserRole.Banco };
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
