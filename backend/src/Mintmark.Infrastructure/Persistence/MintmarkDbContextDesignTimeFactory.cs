using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Mintmark.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can create the context without
/// booting the whole API (the startup project still supplies the connection
/// string at runtime). Connection string comes from MINTMARK_DATABASE or the
/// documented local default.
/// </summary>
public sealed class MintmarkDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MintmarkDbContext>
{
    /// <inheritdoc />
    public MintmarkDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MINTMARK_DATABASE")
            ?? "Host=localhost;Port=5434;Database=mintmark;Username=mintmark;Password=mintmark";

        var options = new DbContextOptionsBuilder<MintmarkDbContext>()
            .UseNpgsql(connectionString, npgsql => _ = npgsql.UseVector())
            .Options;

        return new MintmarkDbContext(options);
    }
}
