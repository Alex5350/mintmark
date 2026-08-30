using Microsoft.EntityFrameworkCore;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// EF-backed persistence for <see cref="IdentificationRun"/> — the audit
/// backbone. Adds and saves are explicit so the enqueue-after-commit window
/// the job runner checks is real.
/// </summary>
public sealed class IdentificationRunStore(MintmarkDbContext dbContext) : IIdentificationRunStore
{
    /// <inheritdoc />
    public async Task AddAsync(IdentificationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        _ = dbContext.IdentificationRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IdentificationRun?> FindAsync(IdentificationRunId runId, CancellationToken cancellationToken = default) =>
        dbContext.IdentificationRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(IdentificationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<IdentificationRun?> FindByObversePerceptualHashAsync(
        UserId userId,
        ulong perceptualHash,
        CancellationToken cancellationToken = default) =>
        dbContext.IdentificationRuns
            .Where(r => r.UserId == userId && r.ObversePerceptualHash == perceptualHash)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
