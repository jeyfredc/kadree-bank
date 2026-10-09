using System.Reflection;
using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Models.DTOs.Auth;
using KadreeBank.Api.Models.Entities;
using KadreeBank.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace KadreeBank.UnitTests;

/// <summary>DAO de usuarios en memoria para probar la lógica de sesión sin base de datos.</summary>
internal sealed class FakeUserDao : IUserDao
{
    private static readonly PropertyInfo TokenId = typeof(RefreshToken).GetProperty(nameof(RefreshToken.Id))!;
    private static readonly PropertyInfo TokenUser = typeof(RefreshToken).GetProperty(nameof(RefreshToken.User))!;

    public List<User> Users { get; } = new();
    public List<RefreshToken> Tokens { get; } = new();
    private readonly List<RefreshToken> _pending = new();

    public Task<User?> FindByIdentifierAsync(string identifier, CancellationToken ct) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Username == identifier || u.Customer?.DocumentNumber == identifier));

    public Task<User?> FindByIdAsync(long userId, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == userId));
    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct) => Task.FromResult(Users.Any(u => u.Username == username));
    public Task<bool> DocumentExistsAsync(string documentNumber, CancellationToken ct) => Task.FromResult(Users.Any(u => u.Customer?.DocumentNumber == documentNumber));
    public Task<Account> RegisterAsync(Customer customer, int originCityId, string username, string pinHash, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult(new Account(0, "AH-5001", Customer.AllowedAccountType(customer.Type), 1, customer.Type, originCityId, 0, nowUtc));
    public Task<List<User>> GetUsersWithPendingPinAsync(CancellationToken ct) => Task.FromResult(Users.Where(u => u.PinHash == User.PendingPinHash).ToList());

    public Task<RefreshToken?> FindRefreshTokenAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public void AddRefreshToken(RefreshToken token) => _pending.Add(token);

    public Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken ct)
    {
        foreach (var t in Tokens.Where(t => t.FamilyId == familyId)) t.Revoke(nowUtc);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        foreach (var t in _pending)
        {
            TokenId.SetValue(t, (long)Tokens.Count + 1);
            TokenUser.SetValue(t, Users.First(u => u.Id == t.UserId));
            Tokens.Add(t);
        }
        _pending.Clear();
        return Task.CompletedTask;
    }
}

public class AuthServiceTests
{
    private const string Pin = "2580";
    private readonly FakeUserDao _users = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 8, 20, 0, 0, TimeSpan.Zero));
    private readonly AuthSettings _settings = TestSecrets.Auth();

    public AuthServiceTests()
    {
        var hasher = new PinHasher(_settings);
        var ana = new Customer { Id = 1, DocumentNumber = "1012345678", FullName = "Ana María Gómez", Type = CustomerType.Natural };
        _users.Users.Add(TestSecrets.User(10, "ana.gomez", ana, hasher.Hash(Pin)));
    }

    private AuthService Service() => new(_users, new InMemoryBank(), new FakeCurrentUser(), new PinHasher(_settings),
        new JwtTokenFactory(TestSecrets.Jwt()), _settings, _time, NullLogger<AuthService>.Instance);

    private static LoginRequestDto Login(string identifier, string pin) => new() { Identifier = identifier, Pin = pin };

    [Theory]
    [InlineData("ana.gomez")]
    [InlineData("ANA.GOMEZ ")]
    [InlineData("1012345678")]
    [InlineData("1.012.345.678")]
    public async Task Se_ingresa_con_usuario_o_con_documento(string identifier)
    {
        var session = await Service().Login(Login(identifier, Pin), default);

        Assert.True(session.Response.Success);
        Assert.False(string.IsNullOrEmpty(session.Response.Data!.AccessToken));
        Assert.Equal("Ana María Gómez", session.Response.Data.User.FullName);
        Assert.False(string.IsNullOrEmpty(session.RefreshToken));
        Assert.Single(_users.Tokens);
    }

    [Fact]
    public async Task Usuario_inexistente_y_contraseña_incorrecta_dan_el_mismo_error()
    {
        var unknown = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service().Login(Login("nadie.aqui", Pin), default));
        var wrong = await Assert.ThrowsAsync<AuthenticationFailedException>(() => Service().Login(Login("ana.gomez", "1357"), default));

        Assert.Equal(unknown.Message, wrong.Message);
    }

    [Fact]
    public async Task Tres_intentos_fallidos_bloquean_y_ni_la_contraseña_correcta_entra_hasta_que_pase_el_tiempo()
    {
        var service = Service();
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.Login(Login("ana.gomez", "1111"), default));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.Login(Login("ana.gomez", "2222"), default));
        await Assert.ThrowsAsync<AccountLockedException>(() => service.Login(Login("ana.gomez", "3333"), default));

        await Assert.ThrowsAsync<AccountLockedException>(() => service.Login(Login("ana.gomez", Pin), default));

        _time.Advance(TimeSpan.FromMinutes(15));
        var session = await service.Login(Login("ana.gomez", Pin), default);
        Assert.True(session.Response.Success);
    }

    [Fact]
    public async Task Renovar_rota_el_refresh_token_y_reutilizar_el_viejo_cierra_la_sesion()
    {
        var service = Service();
        var login = await service.Login(Login("ana.gomez", Pin), default);

        var refreshed = await service.Refresh(login.RefreshToken, default);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        // Alguien reutiliza el token viejo (posible robo): falla y se revoca también el nuevo.
        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh(login.RefreshToken, default));
        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh(refreshed.RefreshToken, default));
    }

    [Fact]
    public async Task Un_refresh_token_vencido_o_desconocido_no_renueva()
    {
        var service = Service();
        var login = await service.Login(Login("ana.gomez", Pin), default);

        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh("token-inventado", default));
        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh(null, default));

        _time.Advance(TimeSpan.FromDays(8));
        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh(login.RefreshToken, default));
    }

    [Fact]
    public async Task Cerrar_sesion_revoca_el_refresh_token()
    {
        var service = Service();
        var login = await service.Login(Login("ana.gomez", Pin), default);

        await service.Logout(login.RefreshToken, default);

        await Assert.ThrowsAsync<InvalidSessionException>(() => service.Refresh(login.RefreshToken, default));
    }

    private static RegisterRequestDto Registro() => new()
    {
        CustomerType = CustomerType.Natural,
        DocumentType = DocumentType.CC,
        DocumentNumber = "52123456",
        FullName = "  Marta   Lucía Ríos ",
        Email = "marta@correo.com",
        Phone = "3001234567",
        CityId = 1,
        Username = "Marta.Rios",
        Pin = "2580",
        PinConfirmation = "2580",
        AcceptTerms = true,
    };

    [Fact]
    public async Task Un_registro_valido_abre_cuenta_de_ahorros_para_persona_natural()
    {
        var result = await Service().Register(Registro(), default);

        Assert.Equal(AccountType.Ahorros, result.Data!.AccountType);
        Assert.Equal("marta.rios", result.Data.Username);
        Assert.Equal("Marta Lucía Ríos", result.Data.FullName);
        Assert.Equal("Bogotá", result.Data.OriginCity);
    }

    [Fact]
    public async Task El_registro_reporta_todos_los_errores_por_campo()
    {
        var request = Registro();
        request.DocumentType = DocumentType.NIT;
        request.Username = "ab";
        request.Pin = "1234";
        request.PinConfirmation = "9999";
        request.AcceptTerms = false;

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => Service().Register(request, default));

        Assert.True(ex.ValidationErrors.ContainsKey("documentType"));
        Assert.True(ex.ValidationErrors.ContainsKey("username"));
        Assert.True(ex.ValidationErrors.ContainsKey("pin"));
        Assert.True(ex.ValidationErrors.ContainsKey("pinConfirmation"));
        Assert.True(ex.ValidationErrors.ContainsKey("acceptTerms"));
    }

    [Fact]
    public async Task No_se_puede_registrar_un_usuario_ya_existente()
    {
        var request = Registro();
        request.Username = "ana.gomez";

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Service().Register(request, default));
        Assert.Equal("usuario_no_disponible", ex.ErrorCode);
    }
}

public class AccountAuthorizationTests
{
    private static OperationRequestDto Request(decimal amount) => new() { Amount = amount, CityId = 1 };

    [Fact]
    public async Task Un_cliente_no_puede_operar_la_cuenta_de_otro_cliente()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-AJENA", 1_000m, customerId: 2);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            AccountServiceTests.CreateService(bank).Withdraw("AH-AJENA", Request(100m), default));
        Assert.Equal(1_000m, bank.BalanceOf("AH-AJENA"));
    }

    [Fact]
    public async Task El_personal_del_banco_no_mueve_dinero_de_los_clientes()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 1_000m);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            AccountServiceTests.CreateService(bank, user: FakeCurrentUser.Banco()).Deposit("AH-1", Request(100m), default));
    }

    [Fact]
    public async Task La_operacion_queda_registrada_con_el_usuario_que_la_hizo()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 1_000m);

        await AccountServiceTests.CreateService(bank).Deposit("AH-1", Request(100m), default);

        Assert.Equal(10, bank.CommittedMovements.Single().CreatedByUserId);
    }
}
