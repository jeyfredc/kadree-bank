using System.Reflection;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Models.Entities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KadreeBank.UnitTests;

internal static class TestSecrets
{
    public static AuthSettings Auth(string pepper = "pepper-de-pruebas-con-mas-de-32-caracteres") => new()
    {
        PinPepper = pepper,
        MaxFailedAttempts = 3,
        LockoutMinutes = 15,
    };

    public static JwtSettings Jwt() => new()
    {
        Key = "clave-jwt-de-pruebas-con-mas-de-32-bytes!!",
        Issuer = "kadree-test",
        Audience = "kadree-test-web",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7,
    };

    /// <summary>Crea un usuario con id y cliente asignados (EF los pondría al guardar).</summary>
    public static User User(long id, string username, Customer customer, string pinHash)
    {
        var user = new User(username, customer, pinHash, DateTime.UtcNow);
        typeof(User).GetProperty(nameof(Api.Models.Entities.User.Id))!.SetValue(user, id);
        typeof(User).GetProperty(nameof(Api.Models.Entities.User.CustomerId))!.SetValue(user, customer.Id);
        return user;
    }
}

public class PinHasherTests
{
    [Fact]
    public void Un_hash_verifica_su_contraseña_y_rechaza_las_demas()
    {
        var hasher = new PinHasher(TestSecrets.Auth());
        var hash = hasher.Hash("2580");

        Assert.StartsWith("pbkdf2-sha256$600000$", hash);
        Assert.True(hasher.Verify("2580", hash));
        Assert.False(hasher.Verify("2581", hash));
    }

    [Fact]
    public void La_misma_contraseña_produce_hashes_distintos_por_la_sal()
    {
        var hasher = new PinHasher(TestSecrets.Auth());

        Assert.NotEqual(hasher.Hash("2580"), hasher.Hash("2580"));
    }

    [Fact]
    public void Sin_el_pepper_correcto_el_hash_no_sirve()
    {
        var hash = new PinHasher(TestSecrets.Auth()).Hash("2580");
        var otroEntorno = new PinHasher(TestSecrets.Auth("otro-pepper-distinto-de-mas-de-32-caracteres"));

        Assert.False(otroEntorno.Verify("2580", hash));
    }

    [Theory]
    [InlineData(User.PendingPinHash)]
    [InlineData("")]
    [InlineData("pbkdf2-sha256$abc$no-es-base64$tampoco")]
    public void Un_hash_invalido_o_pendiente_nunca_coincide(string stored)
    {
        Assert.False(new PinHasher(TestSecrets.Auth()).Verify("2580", stored));
    }
}

public class PinRulesTests
{
    [Theory]
    [InlineData("2580")]
    [InlineData("1357")]
    [InlineData("0912")]
    public void Contraseñas_validas(string pin)
    {
        Assert.Empty(PinRules.Validate(pin, "1012345678"));
    }

    [Theory]
    [InlineData("1111", "repetir")]
    [InlineData("1234", "secuencia")]
    [InlineData("4321", "secuencia")]
    [InlineData("5678", "últimos 4 dígitos")]
    [InlineData("12a4", "exactamente 4 dígitos")]
    [InlineData("123", "exactamente 4 dígitos")]
    public void Contraseñas_invalidas_explican_la_regla(string pin, string fragmento)
    {
        var errors = PinRules.Validate(pin, "1012345678");

        Assert.Contains(errors, e => e.Contains(fragmento));
    }
}

public class UserLockoutTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Al_tercer_intento_fallido_se_bloquea_por_el_tiempo_configurado()
    {
        var user = TestSecrets.User(1, "ana.gomez", new Customer { Id = 1, FullName = "Ana" }, "x");

        Assert.False(user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15)));
        Assert.False(user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15)));
        Assert.True(user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15)));

        Assert.True(user.IsLocked(Now.AddMinutes(14)));
        Assert.False(user.IsLocked(Now.AddMinutes(15)));
    }

    [Fact]
    public void Un_ingreso_correcto_reinicia_el_contador()
    {
        var user = TestSecrets.User(1, "ana.gomez", new Customer { Id = 1, FullName = "Ana" }, "x");
        user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15));
        user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15));

        user.RegisterSuccessfulLogin();

        Assert.Equal(0, user.FailedAttempts);
        Assert.False(user.RegisterFailedAttempt(Now, 3, TimeSpan.FromMinutes(15)));
    }
}

public class JwtTokenFactoryTests
{
    [Fact]
    public async Task El_access_token_lleva_los_claims_y_valida_con_la_configuracion()
    {
        var settings = TestSecrets.Jwt();
        var factory = new JwtTokenFactory(settings);
        var user = TestSecrets.User(7, "ana.gomez", new Customer { Id = 3, FullName = "Ana María Gómez" }, "x");
        var now = DateTime.UtcNow;

        var (token, expires) = factory.CreateAccessToken(user, "Ana María Gómez", now);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, JwtTokenFactory.CreateValidationParameters(settings));

        Assert.True(result.IsValid);
        Assert.Equal(now.AddMinutes(15), expires, TimeSpan.FromSeconds(1));
        Assert.Equal("7", result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("3", result.Claims[JwtTokenFactory.CustomerIdClaim]);
        Assert.Equal("Cliente", result.Claims[JwtTokenFactory.RoleClaim]);
    }

    [Fact]
    public async Task Un_token_firmado_con_otra_clave_no_valida()
    {
        var user = TestSecrets.User(7, "ana.gomez", new Customer { Id = 3, FullName = "Ana" }, "x");
        var otra = TestSecrets.Jwt();
        otra.Key = "otra-clave-completamente-distinta-de-32-bytes";
        var (token, _) = new JwtTokenFactory(otra).CreateAccessToken(user, "Ana", DateTime.UtcNow);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, JwtTokenFactory.CreateValidationParameters(TestSecrets.Jwt()));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Los_refresh_tokens_son_aleatorios_y_se_guardan_solo_como_hash()
    {
        var (t1, h1) = JwtTokenFactory.CreateRefreshToken();
        var (t2, _) = JwtTokenFactory.CreateRefreshToken();

        Assert.NotEqual(t1, t2);
        Assert.Equal(64, h1.Length);
        Assert.NotEqual(t1, h1);
        Assert.Equal(h1, JwtTokenFactory.HashRefreshToken(t1));
    }
}
