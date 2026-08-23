using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Domain.ValueObjects;
using Mintmark.Infrastructure.Persistence;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Mintmark.Infrastructure.Seed;

/// <summary>
/// The <c>--seed</c> entry point: catalog (verbatim from
/// backend/seed/catalog.json), a demo account, a handful of demo holdings
/// and fixture spot rows so local charts and valuations work before the
/// poller ever runs. Idempotent: re-running never duplicates.
/// </summary>
public static class MintmarkSeeder
{
    /// <summary>The demo account email.</summary>
    public const string DemoEmail = "demo@mintmark.local";

    /// <summary>The demo account password (development only, committed deliberately).</summary>
    public const string DemoPassword = "mintmark-demo-2026";

    /// <summary>Locates backend/seed/catalog.json by walking up from the binary.</summary>
    public static string LocateCatalog()
    {
        var overridePath = Environment.GetEnvironmentVariable("MINTMARK_SEED_CATALOG");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && directory is not null; i++)
        {
            var candidate = Path.Combine(directory.FullName, "seed", "catalog.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate seed/catalog.json (walked up from the application binary). Set MINTMARK_SEED_CATALOG to point at it.");
    }

    /// <summary>Runs the full seed (catalog, demo user, holdings, fixture prices).</summary>
    public static async Task RunAsync(IServiceProvider services, string? catalogPath = null, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MintmarkSeeder");
        var dbContext = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();

        var catalog = new CatalogSeeder(
            dbContext,
            scope.ServiceProvider.GetRequiredService<Application.Ports.IImageStore>(),
            scope.ServiceProvider.GetRequiredService<ILogger<CatalogSeeder>>());
        await catalog.SeedAsync(catalogPath ?? LocateCatalog(), cancellationToken);
        logger.LogInformation("Catalog seeded.");

        await SeedFixturePricesAsync(dbContext, cancellationToken);
        var userId = await SeedDemoUserAsync(scope.ServiceProvider, logger, cancellationToken);
        await SeedDemoHoldingsAsync(dbContext, userId, cancellationToken);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Demo data seeded: {Email}", DemoEmail);
        }
    }

    private static async Task SeedFixturePricesAsync(MintmarkDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.SpotPrices.AnyAsync(cancellationToken))
        {
            return;
        }

        // Fixture rows (labeled provider "seed") so prices/charts/valuations
        // work locally before the poller runs; they are replaced by real
        // provider rows on the first successful poll.
        var currency = new Currency("USD");
        var now = DateTimeOffset.UtcNow;
        foreach (var (metal, price) in new[]
                 {
                     (MetalKind.Gold, 2650.44m),
                     (MetalKind.Silver, 30.78m),
                     (MetalKind.Platinum, 961.20m),
                     (MetalKind.Palladium, 1022.10m),
                 })
        {
            var money = new Money(price, currency);
            dbContext.SpotPrices.Add(SpotPrice.Create(
                metal, money, money, money, "seed", now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (await dbContext.SpotPriceDaily.AnyAsync(cancellationToken))
        {
            return;
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        for (var i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var wave = (decimal)(Math.Sin(i / 4.0) * 0.02);
            dbContext.SpotPriceDaily.Add(SpotPriceDaily.Create(
                MetalKind.Silver, currency, date, new Money(30.78m * (1m + wave), currency), "seed"));
            dbContext.SpotPriceDaily.Add(SpotPriceDaily.Create(
                MetalKind.Gold, currency, date, new Money(2650.44m * (1m + (wave / 2)), currency), "seed"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<long> SeedDemoUserAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
    {
        var userManager = services.GetRequiredService<UserManager<MintmarkUser>>();
        var existing = await userManager.FindByEmailAsync(DemoEmail);
        if (existing is not null)
        {
            return existing.Id;
        }

        var user = new MintmarkUser
        {
            UserName = DemoEmail,
            Email = DemoEmail,
            EmailConfirmed = true,
            DisplayName = "Demo Collector",
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Demo user creation failed: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Demo user created with Argon2-hashed password.");
        }
        return user.Id;
    }

    private static async Task SeedDemoHoldingsAsync(MintmarkDbContext dbContext, long userId, CancellationToken cancellationToken)
    {
        if (await dbContext.Holdings.IgnoreQueryFilters().AnyAsync(h => h.UserId == new UserId(userId), cancellationToken))
        {
            return;
        }

        var junkQuarter = await dbContext.CoinTypes
            .FirstOrDefaultAsync(c => c.Name.StartsWith("1964 Washington Quarter"), cancellationToken);
        var silverEagle = await dbContext.CoinTypes
            .FirstOrDefaultAsync(c => c.Name.StartsWith("2023 American Silver Eagle BullionUncirculated"), cancellationToken);

        var usd = new Currency("USD");
        var purchased = new DateTimeOffset(2025, 11, 3, 14, 0, 0, TimeSpan.Zero);

        if (junkQuarter is not null)
        {
            dbContext.Holdings.Add(Holding.Create(
                new UserId(userId),
                ItemForm.JunkSilver,
                quantity: 40, // one roll of 90% quarters
                purchasedAtUtc: purchased,
                purchasePricePerUnit: new Money(8.00m, usd),
                coinTypeId: junkQuarter.Id,
                dealer: "Local coin shop",
                notes: "Seeded demo holding: junk-silver quarter roll"));
        }

        if (silverEagle is not null)
        {
            dbContext.Holdings.Add(Holding.Create(
                new UserId(userId),
                ItemForm.Coin,
                quantity: 10,
                purchasedAtUtc: purchased.AddDays(7),
                purchasePricePerUnit: new Money(34.50m, usd),
                coinTypeId: silverEagle.Id,
                dealer: "Online dealer",
                notes: "Seeded demo holding: 2023 ASE tube"));
        }

        // A generic silver bar: legal without a CoinType (Bar form).
        dbContext.Holdings.Add(Holding.Create(
            new UserId(userId),
            ItemForm.Bar,
            quantity: 2,
            purchasedAtUtc: purchased.AddDays(-30),
            purchasePricePerUnit: new Money(33.10m, usd),
            serialNumber: "SB-000123",
            notes: "Seeded demo holding: generic 1 oz bars (no catalog row — contributes cost, not melt)"));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
