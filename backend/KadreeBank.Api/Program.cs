using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using KadreeBank.Api.Data;
using KadreeBank.Api.Helpers;
using KadreeBank.Api.Interfaces;
using KadreeBank.Api.Middleware;
using KadreeBank.Api.Models.DAO;
using KadreeBank.Api.Models.DTOs.Responses;
using KadreeBank.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configuración de la base de datos (EF Core + PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'DefaultConnection'.");
builder.Services.AddDbContext<KadreeBankDbContext>(options => options.UseNpgsql(connectionString));

// Configuración del banco (zona horaria para extractos y reportes)
var bankOptions = builder.Configuration.GetSection(BankOptions.Section).Get<BankOptions>() ?? new BankOptions();
builder.Services.AddSingleton(bankOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BankCalendar>();

// Configuración de seguridad: secretos fuera del repositorio (user-secrets o variables de entorno)
var jwtSettings = builder.Configuration.GetSection(JwtSettings.Section).Get<JwtSettings>() ?? new JwtSettings();
var authSettings = builder.Configuration.GetSection(AuthSettings.Section).Get<AuthSettings>() ?? new AuthSettings();
SecuritySettingsValidator.Validate(jwtSettings, authSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(authSettings);
builder.Services.AddSingleton<PinHasher>();
builder.Services.AddSingleton<JwtTokenFactory>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// Registrar servicios
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Registrar DAOs
builder.Services.AddScoped<IAccountDao, AccountDao>();
builder.Services.AddScoped<ICityDao, CityDao>();
builder.Services.AddScoped<IReportDao, ReportDao>();
builder.Services.AddScoped<IUserDao, UserDao>();

// Autenticación JWT: firma, emisor, audiencia y vigencia obligatorios
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = JwtTokenFactory.CreateValidationParameters(jwtSettings);
        options.Events = new JwtBearerEvents
        {
            // 401 y 403 también responden con ApiResponse.
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                    expired ? "Tu sesión expiró. Ingresa de nuevo." : "Debes ingresar para usar esta función.",
                    expired ? "sesion_expirada" : "no_autenticado"));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(
                    "No tienes permiso para usar esta función.", "sin_permiso"));
            },
        };
    });

// Autorización: todo exige sesión salvo lo marcado con [AllowAnonymous]; reportes, solo rol Banco
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(AuthPolicies.Banco, policy => policy.RequireRole(nameof(KadreeBank.Api.Models.Entities.UserRole.Banco)));
});

// Límite de solicitudes por IP en los endpoints de autenticación
var authPermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Fail(
            "Hiciste demasiados intentos seguidos. Espera un minuto e inténtalo de nuevo.", "demasiados_intentos"), ct);
    };
});

// Detrás de Nginx: la IP real del cliente llega en X-Forwarded-For (para el límite de solicitudes)
var behindProxy = builder.Configuration.GetValue("ReverseProxy:Enabled", false);
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Configuración de controladores
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Los errores de validación (DataAnnotations) también responden con ApiResponse.
        options.InvalidModelStateResponseFactory = context =>
        {
            var fieldErrors = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(kvp => NormalizeFieldName(kvp.Key), kvp => kvp.Value!.Errors);

            // Con JSON mal formado, ASP.NET agrega además un error genérico sobre el parámetro "request".
            if (fieldErrors.Count > 1)
                fieldErrors.Remove("request");

            var validationErrors = fieldErrors.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(e => FriendlyMessage(kvp.Key, e)).Distinct().ToArray());

            return new BadRequestObjectResult(new ApiResponse
            {
                Success = false,
                Message = "La solicitud tiene datos inválidos.",
                ErrorCode = "validacion",
                ValidationErrors = validationErrors,
                Errors = validationErrors.Values.SelectMany(v => v).ToList(),
            });
        };
    });

// Manejo centralizado de errores
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks().AddDbContextCheck<KadreeBankDbContext>("database");

// Configuración de Swagger (con el botón Authorize para probar con el access token)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Kadree Bank API",
        Version = "v1",
        Description = "Sesión, saldos, consignaciones, retiros, movimientos, extractos mensuales y reportes.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Access token de POST /api/auth/login. Pegue solo el token, sin la palabra Bearer.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

// CORS: solo hace falta si el frontend se sirve desde otro origen (en Docker, Nginx hace de proxy de /api).
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("AllowSpecificOrigin",
    policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Pipeline HTTP
if (behindProxy)
    app.UseForwardedHeaders();

app.UseExceptionHandler();

if (app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Kadree Bank API");
}

app.UseCors("AllowSpecificOrigin");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Contraseña de prueba para los usuarios semilla (solo si Auth:DemoPin está configurado)
await DemoUserSeeder.SeedAsync(app.Services);

app.Run();

// "$.amount" o "Amount" -> "amount"
static string NormalizeFieldName(string key)
{
    var name = key.StartsWith("$.") ? key[2..] : key;
    return string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

// Los errores de conversión de JSON traen mensajes en inglés con tipos internos (System.Decimal...).
static string FriendlyMessage(string field, Microsoft.AspNetCore.Mvc.ModelBinding.ModelError error)
{
    if (field == "request")
        return "El cuerpo de la solicitud es obligatorio y debe ser un JSON válido.";
    if (error.Exception is not null || error.ErrorMessage.StartsWith("The JSON value", StringComparison.Ordinal))
        return $"El valor de '{field}' no tiene un formato válido.";
    return error.ErrorMessage;
}

/// <summary>Expuesto para pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
