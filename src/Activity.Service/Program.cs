using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using PayLite.ActivityService;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Same setup as the Wallet API, so both services' logs look alike on Demo Day:
// every line starts with the transfer's correlation id, and framework noise is turned down
// (Serilog doesn't read the "Logging" section of appsettings). Failed SQL commands are still errors.
const string LogTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}";
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: LogTemplate)
    .WriteTo.File("logs/activity-log.txt", outputTemplate: LogTemplate, rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandlingMiddleware>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<KafkaOptions>()
    .Bind(builder.Configuration.GetSection(KafkaOptions.SectionName));

// Validates the tokens the Wallet API issues (same issuer, audience and key).
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

        // Browsers can't set headers on a WebSocket, so SignalR sends the token as ?access_token=.
        // Accept it ONLY on the hub path (ADR-0003): every other endpoint still needs the Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments(NotificationHub.Path))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

// Applies one Kafka event to ActivityDb (ADR-0004). A new scope (and DbContext) per message.
builder.Services.AddScoped<TransferEventProcessor>();
builder.Services.AddScoped<ITransferEventProcessor>(sp => sp.GetRequiredService<TransferEventProcessor>());
if (builder.Configuration.GetValue($"{KafkaOptions.SectionName}:ConsumerEnabled", true))
{
    builder.Services.AddHostedService<TransferEventsConsumer>();
}

builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IMerchantSummaryService, MerchantSummaryService>();
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationPusher, SignalRNotificationPusher>();

builder.Services.AddControllers();
builder.Services.AddDbContext<ActivityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("ActivityDb"))
        // A duplicate event is EXPECTED to fail with 2627 (that's how the consumer stays idempotent), and
        // the processor logs "already processed, skipping" for it. Without this, EF Core also logs the
        // failed save and the failed command as [ERR], which reads like a crash during the scenario 5 demo.
        // Real failures are still logged by our own code (retries, dead-letter, the API exception handler).
        .ConfigureWarnings(warnings => warnings.Log(
            (CoreEventId.SaveChangesFailed, LogLevel.Debug),
            (RelationalEventId.CommandError, LogLevel.Debug))));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ActivityDbContext>("sqlserver")
    .AddCheck<KafkaHealthCheck>("kafka", failureStatus: HealthStatus.Degraded);

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
        Description = "Paste an access token from the Wallet API's /api/auth/login."
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

// Migrations are NOT applied here: the one-shot "activity-migrator" service does that.
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<ActivityDbContext>().Database;
    if (!database.IsRelational())
    {
        database.EnsureCreated();
    }
}

app.UseExceptionHandler(_ => { });
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PayLite Activity Service v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Path);
app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = HealthResponseWriter.WriteAsync });

app.Run();

public partial class Program { }
