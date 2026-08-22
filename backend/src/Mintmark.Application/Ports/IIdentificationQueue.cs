using Mintmark.Domain;
using Mintmark.Domain.Entities;

namespace Mintmark.Application.Ports;

/// <summary>
/// Port to the identification job queue. Submitting a run enqueues its id;
/// workers pick ids up, persist inputs and drive the vision port.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The name IIdentificationQueue is part of the Application port contract.")]
public interface IIdentificationQueue
{
    /// <summary>Enqueues an identification run for processing.</summary>
    Task EnqueueAsync(IdentificationRunId runId, CancellationToken cancellationToken = default);
}

/// <summary>Port to the background job runner tick (Quartz-backed in Infrastructure).</summary>
public interface IJobRunner
{
    /// <summary>Executes all currently due jobs, returning how many ran.</summary>
    Task<int> RunPendingAsync(CancellationToken cancellationToken = default);
}

/// <summary>Port to perceptual image hashing (pHash) for dedupe.</summary>
public interface IPerceptualHasher
{
    /// <summary>Computes the 64-bit perceptual hash of an image.</summary>
    Task<ulong> HashAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Persistence port for <see cref="IdentificationRun"/> (the audit
/// backbone). Implemented by Infrastructure; the use case layer never touches
/// the database directly.
/// </summary>
public interface IIdentificationRunStore
{
    /// <summary>Persists a newly started run.</summary>
    Task AddAsync(IdentificationRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a run by id, or null.</summary>
    Task<IdentificationRun?> FindAsync(IdentificationRunId runId, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a run (e.g. a recorded confirmation).</summary>
    Task SaveAsync(IdentificationRun run, CancellationToken cancellationToken = default);

    /// <summary>Finds a prior run with the same obverse perceptual hash, or null (dedupe).</summary>
    Task<IdentificationRun?> FindByObversePerceptualHashAsync(
        ulong perceptualHash,
        CancellationToken cancellationToken = default);
}
