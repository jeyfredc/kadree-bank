using System.ComponentModel.DataAnnotations;
using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Models.DTOs.Accounts;
using KadreeBank.Api.Services;

namespace KadreeBank.UnitTests;

public class AccountServiceTests
{
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero));

    internal static AccountService CreateService(InMemoryBank bank, TimeProvider? time = null, ICurrentUser? user = null)
    {
        var clock = time ?? Time;
        return new AccountService(bank, bank, new BankCalendar(new BankOptions(), clock), clock, user ?? new FakeCurrentUser());
    }

    private static OperationRequestDto Request(decimal amount, int cityId = 1, string? description = null) =>
        new() { Amount = amount, CityId = cityId, Description = description };

    [Fact]
    public async Task Un_retiro_valido_persiste_el_movimiento_y_el_nuevo_saldo()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 100_000m);

        var response = await CreateService(bank).Withdraw("AH-1", Request(40_000m, 2, "Cajero"), default);

        Assert.True(response.Success);
        Assert.Equal(60_000m, response.Data!.Balance);
        Assert.Equal("Medellín", response.Data.Movement.City);
        Assert.True(response.Data.Movement.Id > 0);
        Assert.Equal(60_000m, bank.BalanceOf("AH-1"));
        Assert.Single(bank.CommittedMovements);
    }

    [Fact]
    public async Task Un_retiro_sin_fondos_no_persiste_nada()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 100_000m);

        await Assert.ThrowsAsync<InsufficientFundsException>(() =>
            CreateService(bank).Withdraw("AH-1", Request(100_001m), default));

        Assert.Equal(100_000m, bank.BalanceOf("AH-1"));
        Assert.Empty(bank.CommittedMovements);
    }

    [Fact]
    public async Task Operar_sobre_una_cuenta_inexistente_responde_no_encontrado()
    {
        var bank = new InMemoryBank();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(bank).Deposit("NO-EXISTE", Request(10m), default));
    }

    [Fact]
    public async Task Una_ciudad_inexistente_es_un_error_de_validacion()
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 0m);

        var ex = await Assert.ThrowsAsync<RequestValidationException>(() =>
            CreateService(bank).Deposit("AH-1", Request(10m, 99), default));
        Assert.True(ex.ValidationErrors.ContainsKey("cityId"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.234)]
    public async Task Si_un_monto_invalido_llega_al_servicio_la_entidad_lo_rechaza(decimal amount)
    {
        var bank = new InMemoryBank();
        bank.SeedAccount("AH-1", 1_000m);

        await Assert.ThrowsAsync<InvalidAmountException>(() =>
            CreateService(bank).Deposit("AH-1", Request(amount), default));
        Assert.Equal(1_000m, bank.BalanceOf("AH-1"));
    }
}

/// <summary>Validación de entrada por DataAnnotations (la que aplica [ApiController] antes del servicio).</summary>
public class OperationRequestDtoValidationTests
{
    private static List<ValidationResult> Validate(OperationRequestDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Una_solicitud_valida_no_tiene_errores()
    {
        Assert.Empty(Validate(new OperationRequestDto { Amount = 1500.50m, CityId = 1, Description = "Nómina" }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    [InlineData(1_000_000_000_000.01)]
    public void Montos_invalidos_se_rechazan(decimal amount)
    {
        var errors = Validate(new OperationRequestDto { Amount = amount, CityId = 1 });
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(OperationRequestDto.Amount)));
    }

    [Fact]
    public void La_ciudad_es_obligatoria_y_la_descripcion_tiene_tope()
    {
        var errors = Validate(new OperationRequestDto { Amount = 10m, CityId = 0, Description = new string('x', 201) });

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(OperationRequestDto.CityId)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(OperationRequestDto.Description)));
    }
}
