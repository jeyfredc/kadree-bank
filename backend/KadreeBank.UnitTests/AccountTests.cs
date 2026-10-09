using KadreeBank.Api.Exceptions;
using KadreeBank.Api.Models.Entities;

namespace KadreeBank.UnitTests;

public class AccountTests
{
    private static readonly DateTime Now = new(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc);
    private const int Bogota = 1;
    private const int Medellin = 2;

    private static Account SavingsAccount(decimal balance) =>
        new(1, "AH-0001", AccountType.Ahorros, 10, CustomerType.Natural, Bogota, balance, Now);

    [Fact]
    public void Consignar_incrementa_el_saldo_y_registra_el_saldo_resultante()
    {
        var account = SavingsAccount(100_000m);

        var movement = account.Deposit(50_000.50m, Bogota, Now, "Nómina");

        Assert.Equal(150_000.50m, account.Balance);
        Assert.Equal(MovementType.Consignacion, movement.Type);
        Assert.Equal(150_000.50m, movement.BalanceAfter);
        Assert.Equal(Bogota, movement.CityId);
    }

    [Fact]
    public void Retirar_disminuye_el_saldo_y_guarda_la_ciudad_del_retiro()
    {
        var account = SavingsAccount(100_000m);

        var movement = account.Withdraw(30_000m, Medellin, Now);

        Assert.Equal(70_000m, account.Balance);
        Assert.Equal(MovementType.Retiro, movement.Type);
        Assert.Equal(Medellin, movement.CityId);
        Assert.Equal(70_000m, movement.BalanceAfter);
    }

    [Fact]
    public void Retirar_todo_el_saldo_deja_la_cuenta_en_cero()
    {
        var account = SavingsAccount(100_000m);

        account.Withdraw(100_000m, Bogota, Now);

        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void Retirar_mas_del_saldo_falla_y_no_modifica_la_cuenta()
    {
        var account = SavingsAccount(100_000m);

        var ex = Assert.Throws<InsufficientFundsException>(() => account.Withdraw(100_000.01m, Bogota, Now));

        Assert.Equal("saldo_insuficiente", ex.ErrorCode);
        Assert.Equal(100_000m, account.Balance);
    }

    [Fact]
    public void Retirar_de_una_cuenta_en_cero_falla()
    {
        var account = SavingsAccount(0m);

        Assert.Throws<InsufficientFundsException>(() => account.Withdraw(0.01m, Bogota, Now));
        Assert.Equal(0m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    [InlineData(1_000_000_000_000.01)]
    public void Montos_invalidos_se_rechazan_en_consignaciones_y_retiros(decimal amount)
    {
        var account = SavingsAccount(1_000m);

        Assert.Throws<InvalidAmountException>(() => account.Deposit(amount, Bogota, Now));
        Assert.Throws<InvalidAmountException>(() => account.Withdraw(amount, Bogota, Now));
        Assert.Equal(1_000m, account.Balance);
    }

    [Fact]
    public void Una_cuenta_no_puede_abrirse_con_saldo_negativo()
    {
        Assert.Throws<InvalidAmountException>(() =>
            new Account(1, "AH-1", AccountType.Ahorros, 1, CustomerType.Natural, Bogota, -1m, Now));
    }

    [Theory]
    [InlineData(CustomerType.Natural, AccountType.Corriente)]
    [InlineData(CustomerType.Juridica, AccountType.Ahorros)]
    public void El_tipo_de_cuenta_debe_corresponder_al_tipo_de_cliente(CustomerType customer, AccountType account)
    {
        Assert.Throws<AccountTypeNotAllowedException>(() =>
            new Account(1, "X-1", account, 1, customer, Bogota, 0m, Now));
    }

    [Theory]
    [InlineData(CustomerType.Natural, AccountType.Ahorros)]
    [InlineData(CustomerType.Juridica, AccountType.Corriente)]
    public void Combinaciones_validas_de_cliente_y_cuenta(CustomerType customer, AccountType account)
    {
        var created = new Account(1, "X-1", account, 1, customer, Bogota, 0m, Now);
        Assert.Equal(account, created.Type);
    }
}
