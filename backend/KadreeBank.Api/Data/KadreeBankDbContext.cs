using KadreeBank.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KadreeBank.Api.Data
{
    /// <summary>
    /// Mapea las entidades sobre el esquema de db/01_schema.sql. El esquema SQL es la fuente
    /// de verdad (con sus CHECK, índices y trigger), por eso no se usan migraciones.
    /// </summary>
    public class KadreeBankDbContext : DbContext
    {
        /// <summary>Crea el contexto; la cadena de conexión llega por <paramref name="options"/> desde Program.cs.</summary>
        /// <param name="options">Opciones de EF Core (proveedor Npgsql y cadena de conexión).</param>
        public KadreeBankDbContext(DbContextOptions<KadreeBankDbContext> options) : base(options) { }

        /// <summary>Tabla <c>cities</c>.</summary>
        public DbSet<City> Cities => Set<City>();

        /// <summary>Tabla <c>customers</c>.</summary>
        public DbSet<Customer> Customers => Set<Customer>();

        /// <summary>Tabla <c>accounts</c>.</summary>
        public DbSet<Account> Accounts => Set<Account>();

        /// <summary>Tabla <c>movements</c> (solo inserción: un trigger impide modificar o borrar).</summary>
        public DbSet<Movement> Movements => Set<Movement>();

        /// <summary>Tabla <c>users</c>: usuarios de la banca en línea.</summary>
        public DbSet<User> Users => Set<User>();

        /// <summary>Tabla <c>refresh_tokens</c>: sesiones (solo hashes).</summary>
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        /// <summary>
        /// Mapea cada entidad a su tabla y columna en snake_case, convierte los enums a texto
        /// y declara las relaciones de navegación que usan las consultas LINQ.
        /// </summary>
        /// <param name="model">Constructor del modelo de EF Core.</param>
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<City>(e =>
            {
                e.ToTable("cities");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
                e.Property(x => x.Name).HasColumnName("name");
            });

            model.Entity<Customer>(e =>
            {
                e.ToTable("customers");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.DocumentNumber).HasColumnName("document_number");
                e.Property(x => x.FullName).HasColumnName("full_name");
                e.Property(x => x.Type).HasColumnName("customer_type").HasConversion(UpperCaseEnum<CustomerType>());
                e.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion(UpperCaseEnum<DocumentType>());
                e.Property(x => x.Email).HasColumnName("email");
                e.Property(x => x.Phone).HasColumnName("phone");
            });

            model.Entity<User>(e =>
            {
                e.ToTable("users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
                e.Property(x => x.Username).HasColumnName("username");
                e.Property(x => x.CustomerId).HasColumnName("customer_id");
                e.Property(x => x.Role).HasColumnName("role").HasConversion(UpperCaseEnum<UserRole>());
                e.Property(x => x.PinHash).HasColumnName("pin_hash");
                e.Property(x => x.FailedAttempts).HasColumnName("failed_attempts");
                e.Property(x => x.LockedUntilUtc).HasColumnName("locked_until");
                e.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
                e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            });

            model.Entity<RefreshToken>(e =>
            {
                e.ToTable("refresh_tokens");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
                e.Property(x => x.UserId).HasColumnName("user_id");
                e.Property(x => x.TokenHash).HasColumnName("token_hash");
                e.Property(x => x.FamilyId).HasColumnName("family_id");
                e.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at");
                e.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
                e.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at");
                e.Property(x => x.ReplacedByHash).HasColumnName("replaced_by_hash");
                e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            });

            model.Entity<Account>(e =>
            {
                e.ToTable("accounts");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id");
                e.Property(x => x.Number).HasColumnName("number");
                e.Property(x => x.Type).HasColumnName("account_type").HasConversion(UpperCaseEnum<AccountType>());
                e.Property(x => x.CustomerId).HasColumnName("customer_id");
                e.Property(x => x.CustomerType).HasColumnName("customer_type").HasConversion(UpperCaseEnum<CustomerType>());
                e.Property(x => x.OriginCityId).HasColumnName("origin_city_id");
                e.Property(x => x.Balance).HasColumnName("balance").HasPrecision(18, 2);
                e.Property(x => x.OpenedAtUtc).HasColumnName("opened_at");

                e.HasOne(x => x.Customer).WithMany(c => c.Accounts).HasForeignKey(x => x.CustomerId);
                e.HasOne(x => x.OriginCity).WithMany().HasForeignKey(x => x.OriginCityId);
            });

            model.Entity<Movement>(e =>
            {
                e.ToTable("movements");
                e.HasKey(x => x.Id);
                e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
                e.Property(x => x.AccountId).HasColumnName("account_id");
                e.Property(x => x.Type).HasColumnName("movement_type").HasConversion(UpperCaseEnum<MovementType>());
                e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
                e.Property(x => x.CityId).HasColumnName("city_id");
                e.Property(x => x.BalanceAfter).HasColumnName("balance_after").HasPrecision(18, 2);
                e.Property(x => x.Description).HasColumnName("description");
                e.Property(x => x.CreatedAtUtc).HasColumnName("created_at");
                e.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");

                e.HasOne(x => x.Account).WithMany(a => a.Movements).HasForeignKey(x => x.AccountId);
                e.HasOne(x => x.City).WithMany().HasForeignKey(x => x.CityId);
            });
        }

        /// <summary>Guarda los enums como texto en mayúsculas ('AHORROS', 'RETIRO'...), igual que los CHECK del esquema.</summary>
        private static ValueConverter<TEnum, string> UpperCaseEnum<TEnum>() where TEnum : struct, Enum =>
            new(v => v.ToString().ToUpperInvariant(), v => Enum.Parse<TEnum>(v, true));
    }
}
