using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PayLite.WalletApi;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Serilog is configured here, not from the "Logging" section of appsettings.
// Framework noise is turned down so a transfer's own lines are easy to follow in the demo:
// UseSerilogRequestLogging below still writes one line per request, and the outbox poll's
// SELECT no longer shows up every second. Failed SQL commands are still logged as errors.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/wallet-log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<PasswordHashOptions>()
    .Bind(builder.Configuration.GetSection(PasswordHashOptions.SectionName));

builder.Services.AddOptions<WalletOptions>()
    .Bind(builder.Configuration.GetSection(WalletOptions.SectionName));

builder.Services.AddOptions<OutboxOptions>()
    .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName));

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName));

// The Wallet API ISSUES the tokens. The Activity Service validates the same tokens,
// so Jwt:Issuer, Jwt:Audience and Jwt:Key must be identical in both services.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration[$"{JwtOptions.SectionName}:Key"];
        ArgumentException.ThrowIfNullOrWhiteSpace(jwtKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration[$"{JwtOptions.SectionName}:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration[$"{JwtOptions.SectionName}:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, Argon2PasswordHasher>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ITopUpService, TopUpService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddHostedService<IdempotencyKeyCleanupService>();
builder.Services.AddHostedService<OutboxPublisher>();
// TODO (Owner A): outbox publisher (BackgroundService)

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WalletDb")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<WalletDbContext>("sqlserver")
    .AddCheck<KafkaHealthCheck>("kafka", failureStatus: HealthStatus.Degraded);

// The OpenAPI document is the API contract: the frontend generates its typed client from
// /swagger/v1/swagger.json (see frontend/package.json "gen:api").
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Honour C# nullability, so `string` (not `string?`) becomes a required, non-null
    // property in the generated TypeScript types.
    options.SupportNonNullableReferenceTypes();
    options.NonNullableReferenceTypesAsRequired();

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Enter a valid JWT token."
    });

    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

// Migrations are NOT applied here: the one-shot "wallet-migrator" service does that (see docker-compose.yml).
// Only the InMemory provider used by unit-style tests needs the schema created on the fly.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database;
    if (!database.IsRelational())
    {
        database.EnsureCreated();
    }
}

await AdminSeeder.SeedAdminAsync(app.Services);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler(_ => { });
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PayLite Wallet API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

app.Run();

public partial class Program { }
