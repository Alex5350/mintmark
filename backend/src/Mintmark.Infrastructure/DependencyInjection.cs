using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Mintmark.Application.Ports;
using Mintmark.Application.UseCases;
using Mintmark.Infrastructure.Identification;
using Mintmark.Infrastructure.Identity;
using Mintmark.Infrastructure.Jobs;
using Mintmark.Infrastructure.Persistence;
using Mintmark.Infrastructure.Prices;
using Mintmark.Infrastructure.Storage;
using Pgvector.EntityFrameworkCore;

namespace Mintmark.Infrastructure;

/// <summary>
/// Everything Infrastructure registers, in one place. The API composition
/// root calls this once; nothing here depends on the API.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers persistence, identity, storage, prices, jobs and identification.</summary>
    public static IServiceCollection AddMintmarkInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        BindOptions(services, configuration);

        // ---- Persistence -------------------------------------------------
        services.AddDbContext<MintmarkDbContext>((provider, options) =>
        {
            var database = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            _ = options.UseNpgsql(database.ConnectionString, npgsql => _ = npgsql.UseVector());
        });

        // ---- Identity (Argon2 per ADR 0005; no PBKDF2) --------------------
        services
            .AddIdentityCore<MintmarkUser>(options =>
            {
                options.Password.RequiredLength = 12; // the RegisterValidator adds the denylist screen
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<MintmarkDbContext>()
            .AddDefaultTokenProviders();
        _ = services.UpgradePasswordSecurity().UseArgon2<MintmarkUser>();

        services.AddScoped<AccessTokenIssuer>();
        services.AddScoped<RefreshTokenService>();

        // ---- Storage ------------------------------------------------------
        services.AddSingleton<MinioImageStore>();
        services.AddSingleton<IImageStore>(provider => provider.GetRequiredService<MinioImageStore>());
        services.AddSingleton<IUploadUrlFactory>(provider => provider.GetRequiredService<MinioImageStore>());
        services.AddSingleton<IObjectStorage>(provider => provider.GetRequiredService<MinioImageStore>());
        services.AddSingleton<IPerceptualHasher, PerceptualHasher>();

        // ---- Prices -------------------------------------------------------
        _ = services.AddMemoryCache();
        _ = services.AddHttpClient("metalsdev");
        _ = services.AddHttpClient("goldapicom");
        services.AddScoped<MetalsDevProvider>(provider => new MetalsDevProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("metalsdev"),
            provider.GetRequiredService<IOptions<PriceOptions>>().Value.PrimaryKey));
        services.AddScoped<GoldApiComProvider>(provider => new GoldApiComProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("goldapicom"),
            provider.GetRequiredService<IOptions<PriceOptions>>().Value.FallbackKey));
        services.AddScoped<CompositeMetalPriceProvider>(provider =>
        {
            var priceOptions = provider.GetRequiredService<IOptions<PriceOptions>>().Value;
            var chain = new List<IMetalPriceProvider>();
            foreach (var providerId in new[] { priceOptions.Primary, priceOptions.Fallback })
            {
                IMetalPriceProvider impl = providerId.Trim().ToLowerInvariant() switch
                {
                    "metalsdev" => provider.GetRequiredService<MetalsDevProvider>(),
                    "goldapicom" => provider.GetRequiredService<GoldApiComProvider>(),
                    _ => throw new InvalidOperationException(
                        $"Unknown price provider '{providerId}' (MINTMARK_PRICE_PRIMARY/FALLBACK); use metalsdev or goldapicom."),
                };
                if (!chain.Contains(impl))
                {
                    chain.Add(impl);
                }
            }

            return new CompositeMetalPriceProvider(chain, provider.GetRequiredService<ILogger<CompositeMetalPriceProvider>>());
        });

        services.AddScoped<PriceCache>();
        // Request paths read the cache, never the network.
        services.AddScoped<IMetalPriceProvider>(provider => provider.GetRequiredService<PriceCache>());
        services.AddScoped<PriceChartService>();

        // ---- Identification -----------------------------------------------
        services.AddScoped<ReferenceImageMatcher>();
        services.AddScoped<Application.Ports.IIdentificationRunStore, IdentificationRunStore>();
        services.AddSingleton<IdentificationQueue>();
        services.AddSingleton<IIdentificationQueue>(provider => provider.GetRequiredService<IdentificationQueue>());
        services.AddSingleton<IdentificationJobRunner>();
        services.AddSingleton<IJobRunner>(provider => provider.GetRequiredService<IdentificationJobRunner>());
        _ = services.AddHostedService(provider => provider.GetRequiredService<IdentificationJobRunner>());

        services.AddScoped<IVisionIdentifier>(provider =>
        {
            var vision = provider.GetRequiredService<IOptions<VisionOptions>>().Value;
            var httpFactory = provider.GetRequiredService<IHttpClientFactory>();
            return vision.Provider.Trim().ToLowerInvariant() switch
            {
                "offline" => new OfflineVisionEvaluator(
                    provider.GetRequiredService<IPerceptualHasher>(),
                    provider.GetRequiredService<ReferenceImageMatcher>()),
                "openai" => new OpenAIVisionIdentifier(
                    httpFactory.CreateClient("openai-vision"),
                    vision.OpenAIKey ?? throw new InvalidOperationException("MINTMARK_OPENAI_API_KEY is required for the openai vision provider."),
                    vision.Model ?? throw new InvalidOperationException("MINTMARK_VISION_MODEL is required for the openai vision provider.")),
                "gemini" => new GeminiVisionIdentifier(
                    httpFactory.CreateClient("gemini-vision"),
                    vision.GeminiKey ?? throw new InvalidOperationException("MINTMARK_GEMINI_API_KEY is required for the gemini vision provider."),
                    vision.Model ?? throw new InvalidOperationException("MINTMARK_VISION_MODEL is required for the gemini vision provider.")),
                _ => throw new InvalidOperationException(
                    $"Unknown vision provider '{vision.Provider}' (MINTMARK_VISION_PROVIDER); use offline, openai or gemini."),
            };
        });
        _ = services.AddHttpClient("openai-vision");
        _ = services.AddHttpClient("gemini-vision");

        services.AddScoped<ICoinSearch, HybridCoinSearch>();
        _ = services.AddScoped<IdentificationService>();
        _ = services.AddScoped<CatalogSearchService>();
        _ = services.AddScoped<ValuationService>();

        // ---- Jobs ----------------------------------------------------------
        _ = services.AddMintmarkQuartz(
            configuration.GetSection(OptionsGroups.Price).Get<PriceOptions>()
                ?? new PriceOptions());

        return services;
    }

    /// <summary>Configures JwtBearer authentication from the bound options (ADR 0005).</summary>
    public static IServiceCollection AddMintmarkJwtAuthentication(
        this IServiceCollection services,
        JwtOptions jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(jwtOptions);

        var issuer = new AccessTokenIssuer(jwtOptions);
        _ = services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = issuer.SigningKey,
                    ValidateLifetime = true,
                    // ADR 0005: 30 s of slack for client clock drift.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        return services;
    }

    private static void BindOptions(IServiceCollection services, IConfiguration configuration)
    {
        // Plain options instances are resolvable directly (services inject
        // the group type, not IOptions<T>), backed by the validated options.
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<StorageOptions>>().Value);
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<PriceOptions>>().Value);
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<VisionOptions>>().Value);
        _ = services.AddSingleton(provider => provider.GetRequiredService<IOptions<IdentificationOptions>>().Value);

        _ = services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Database))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Database:ConnectionString is required (MINTMARK_DATABASE).")
            .ValidateOnStart();

        _ = services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Jwt))
            .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters (MINTMARK_JWT_SIGNING_KEY; openssl rand -base64 48).")
            .Validate(o => o.AccessTokenMinutes is >= 1 and <= 120, "Jwt:AccessTokenMinutes must be between 1 and 120.")
            .Validate(o => o.RefreshTokenDays is >= 1 and <= 365, "Jwt:RefreshTokenDays must be between 1 and 365.")
            .ValidateOnStart();

        _ = services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Storage))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint), "Storage:Endpoint is required (MINTMARK_STORAGE_ENDPOINT).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.AccessKey), "Storage:AccessKey is required (MINTMARK_STORAGE_ACCESS_KEY).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "Storage:SecretKey is required (MINTMARK_STORAGE_SECRET_KEY).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Bucket), "Storage:Bucket is required (MINTMARK_STORAGE_BUCKET).")
            .ValidateOnStart();

        _ = services.AddOptions<PriceOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Price))
            .Validate(o => o.MonthlyBudget is >= 1 and <= 100_000, "Price:MonthlyBudget must be positive.")
            .Validate(o => o.Primary is "metalsdev" or "goldapicom", "Price:Primary must be metalsdev or goldapicom.")
            .Validate(o => o.Fallback is "metalsdev" or "goldapicom", "Price:Fallback must be metalsdev or goldapicom.")
            .Validate(o => o.BaseCurrency.Length == 3 && o.BaseCurrency.All(char.IsUpper), "Price:BaseCurrency must be three uppercase letters (e.g. USD).")
            .ValidateOnStart();

        _ = services.AddOptions<VisionOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Vision))
            .Validate(o => o.Provider is "offline" or "openai" or "gemini", "Vision:Provider must be offline, openai or gemini.")
            .Validate(o => o.Provider != "openai" || (!string.IsNullOrWhiteSpace(o.OpenAIKey) && !string.IsNullOrWhiteSpace(o.Model)), "The openai vision provider requires MINTMARK_OPENAI_API_KEY and MINTMARK_VISION_MODEL.")
            .Validate(o => o.Provider != "gemini" || (!string.IsNullOrWhiteSpace(o.GeminiKey) && !string.IsNullOrWhiteSpace(o.Model)), "The gemini vision provider requires MINTMARK_GEMINI_API_KEY and MINTMARK_VISION_MODEL.")
            .ValidateOnStart();

        _ = services.AddOptions<IdentificationOptions>()
            .Bind(configuration.GetSection(OptionsGroups.Identification))
            .Validate(o => o.DailyLimit is >= 1 and <= 1_000, "Identification:DailyLimit must be between 1 and 1000.")
            .ValidateOnStart();
    }
}
