using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Mintmark.Api;
using Mintmark.Application;
using Mintmark.Api.EndpointModules;
using Mintmark.Application.Dtos;
using Mintmark.Application.Validators;
using Mintmark.Infrastructure;
using Mintmark.Infrastructure.Identity;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Seed;
using Scalar.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// ---------------------------------------------------------------------------
// Composition root ONLY: options, DI, middleware wiring. Behavior lives in
// Application/Infrastructure; routes live in EndpointModules.
// Modes: `--seed` (seed then exit), `--export-openapi` (write docs/openapi.json then exit).
// ---------------------------------------------------------------------------

var seedMode = args.Contains("--seed");
var exportOpenApi = args.Contains("--export-openapi");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });

// MINTMARK_ENVIRONMENT wins over ASPNETCORE_ENVIRONMENT (.env.example contract).
var environmentOverride = Environment.GetEnvironmentVariable("MINTMARK_ENVIRONMENT");
if (!string.IsNullOrWhiteSpace(environmentOverride))
{
    builder.Environment.EnvironmentName = environmentOverride;
}

// MINTMARK_* environment variables map onto the typed options groups.
// Absent variables are skipped (a null in-memory value would override the
// appsettings defaults).
var envOverrides = new Dictionary<string, string?>
{
    ["Database:ConnectionString"] = Env("MINTMARK_DATABASE"),
    ["Jwt:SigningKey"] = Env("MINTMARK_JWT_SIGNING_KEY"),
    ["Jwt:Issuer"] = Env("MINTMARK_JWT_ISSUER"),
    ["Jwt:Audience"] = Env("MINTMARK_JWT_AUDIENCE"),
    ["Jwt:AccessTokenMinutes"] = Env("MINTMARK_ACCESS_TOKEN_MINUTES"),
    ["Jwt:RefreshTokenDays"] = Env("MINTMARK_REFRESH_TOKEN_DAYS"),
    ["Storage:Endpoint"] = Env("MINTMARK_STORAGE_ENDPOINT"),
    ["Storage:AccessKey"] = Env("MINTMARK_STORAGE_ACCESS_KEY"),
    ["Storage:SecretKey"] = Env("MINTMARK_STORAGE_SECRET_KEY"),
    ["Storage:Bucket"] = Env("MINTMARK_STORAGE_BUCKET"),
    ["Storage:UseSsl"] = Env("MINTMARK_STORAGE_USE_SSL"),
    ["Price:Primary"] = Env("MINTMARK_PRICE_PRIMARY"),
    ["Price:PrimaryKey"] = Env("MINTMARK_PRICE_PRIMARY_KEY"),
    ["Price:Fallback"] = Env("MINTMARK_PRICE_FALLBACK"),
    ["Price:FallbackKey"] = Env("MINTMARK_PRICE_FALLBACK_KEY"),
    ["Price:MonthlyBudget"] = Env("MINTMARK_PRICE_MONTHLY_BUDGET"),
    ["Price:BaseCurrency"] = Env("MINTMARK_PRICE_BASE_CURRENCY"),
    ["Vision:Provider"] = Env("MINTMARK_VISION_PROVIDER"),
    ["Vision:Model"] = Env("MINTMARK_VISION_MODEL"),
    ["Vision:OpenAIKey"] = Env("MINTMARK_OPENAI_API_KEY"),
    ["Vision:GeminiKey"] = Env("MINTMARK_GEMINI_API_KEY"),
    ["Identification:DailyLimit"] = Env("MINTMARK_IDENTIFY_DAILY_LIMIT"),
};
builder.Configuration.AddInMemoryCollection(
    envOverrides.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value));

static string? Env(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;

// ---- Services -------------------------------------------------------------

builder.Services.AddMintmarkInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(OptionsGroups.Jwt).Get<JwtOptions>() ?? new JwtOptions();
_ = builder.Services.AddMintmarkJwtAuthentication(jwtOptions);
builder.Services.AddAuthorization();

// Validators are invoked manually in endpoints (the AspNetCore
// auto-validation package is deprecated — see docs/versions.md).
builder.Services.AddScoped<IValidator<RegisterRequest>, RegisterValidator>();
builder.Services.AddScoped<IValidator<CreateHoldingRequest>, CreateHoldingValidator>();
builder.Services.AddScoped<IValidator<SubmitIdentificationRequest>, SubmitIdentificationValidator>();

// RFC 9457 problem details everywhere; unhandled exceptions and bare status
// codes become problem documents.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new TypedIdJsonConverterFactory());
    options.SerializerOptions.Converters.Add(new CurrencyJsonConverter());
});

builder.Services.AddCors(options => options.AddPolicy("clients", policy => policy
    .WithOrigins("http://localhost:3100", "http://localhost:19006", "exp://localhost:19006")
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Rate limits: 10 auth attempts per IP per minute; identification 25/day per user.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });

    options.AddPolicy("identification", context =>
    {
        var user = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(user, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.User.Identity?.IsAuthenticated == true ? 25 : 5,
            Window = TimeSpan.FromHours(24),
            QueueLimit = 0,
        });
    });
});

// The document service registers unconditionally (cheap; needed by the
// export mode and CI diff); SERVING it stays non-production-only below.
var docsEnabled = !builder.Environment.IsProduction() || exportOpenApi;
_ = builder.Services.AddOpenApi();

// OpenTelemetry: traces/metrics with OTLP export only when an endpoint is
// configured (no vendor, no noisy local default).
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
var telemetry = builder.Services.AddOpenTelemetry();
_ = telemetry
    .WithTracing(tracing => _ = tracing.AddAspNetCoreInstrumentation())
    .WithMetrics(metrics => _ = metrics.AddAspNetCoreInstrumentation());
if (!string.IsNullOrWhiteSpace(otlpEndpoint))
{
    _ = telemetry.WithTracing(tracing => _ = tracing.AddOtlpExporter());
    _ = telemetry.WithMetrics(metrics => _ = metrics.AddOtlpExporter());
}

var app = builder.Build();

// ---- Pipeline ---------------------------------------------------------------

app.UseExceptionHandler();
app.UseStatusCodePages();

// The document route maps unconditionally: MapOpenApi also registers the
// IOpenApiDocumentProvider the export mode resolves. Scalar UI (human
// surface) stays non-production-only.
_ = app.MapOpenApi("/openapi/v1.json");
if (docsEnabled)
{
    _ = app.MapScalarApiReference("/docs");
}

app.UseCors("clients");
_ = app.UseAuthentication();
app.UseMiddleware<UserContextMiddleware>();
_ = app.UseAuthorization();

app.UseRateLimiter();

_ = app.MapMintmarkModules();

_ = app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Meta")
    .AllowAnonymous();

// ---- Modes -------------------------------------------------------------------

if (exportOpenApi)
{
    // CI diff + client generation: self-host briefly and capture the
    // document from the mapped endpoint — the only surface guaranteed to
    // match what clients actually see.
    app.Urls.Add("http://127.0.0.1:5198");
    await app.StartAsync();
    try
    {
        using var http = new HttpClient();
        await using var document = await http.GetStreamAsync("http://127.0.0.1:5198/openapi/v1.json");
        var target = FindDocsDirectory();
        await using var output = File.Create(Path.Combine(target, "openapi.json"));
        await document.CopyToAsync(output);
        Console.WriteLine($"OpenAPI document written to {Path.Combine(target, "openapi.json")}");
    }
    finally
    {
        await app.StopAsync();
    }
    return;
}

if (app.Environment.IsDevelopment())
{
    // Development convenience only: production applies migrations
    // deliberately (docs/runbook.md). Disable with Database:AutoMigrate=false.
    using var scope = app.Services.CreateScope();
    var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (databaseOptions.AutoMigrate || seedMode)
    {
        await using var context = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();
        await context.Database.MigrateAsync();
    }
}

if (seedMode)
{
    await MintmarkSeeder.RunAsync(app.Services);
    Console.WriteLine("Seeding complete.");
    return;
}

app.Run();

static string FindDocsDirectory()
{
    var overridePath = Environment.GetEnvironmentVariable("MINTMARK_OPENAPI_OUTPUT");
    if (!string.IsNullOrWhiteSpace(overridePath))
    {
        return overridePath;
    }

    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && directory is not null; i++)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "docs"))
            && File.Exists(Path.Combine(directory.FullName, "justfile")))
        {
            return Path.Combine(directory.FullName, "docs");
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException(
        "Could not locate the repository docs/ directory for openapi.json export. Set MINTMARK_OPENAPI_OUTPUT.");
}

/// <summary>Program entry (top-level statements host); exposed for WebApplicationFactory.</summary>
public sealed partial class Program;
