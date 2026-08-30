using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;

namespace Mintmark.Application.Tests.TestInfrastructure;

/// <summary>A vision port returning a fixed, contract-shaped response. Counts calls for dedupe assertions.</summary>
internal sealed class FakeVisionIdentifier : IVisionIdentifier
{
    public const string RawResponse = """
        {"series":{"value":"Fictional Libertad","confidence":0.87,"evidence":"winged victory on obverse"}}
        """;

    public int CallCount { get; private set; }

    public Task<VisionIdentification> IdentifyAsync(ImageInput input, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new VisionIdentification(
            "fixture-vision",
            "1.2.3",
            RawResponse,
            country: new FieldObservation<string?>("Mexico", 0.90m, "ESTADOS UNIDOS MEXICANOS legend"),
            mint: new FieldObservation<string?>(null, 0.0m),
            series: new FieldObservation<string?>("Fictional Libertad", 0.87m, "winged victory on obverse"),
            year: new FieldObservation<int?>(2023, 0.75m, "date below the victory device"),
            denomination: new FieldObservation<string?>("1 Onza", 0.70m, "denomination legend"),
            metal: new FieldObservation<string?>("Silver", 0.90m, "white metal, cartwheel luster"),
            fineness: new FieldObservation<decimal?>(0.999m, 0.80m, ".999 fine incuse on reverse"),
            sizeEstimateTroyOz: new FieldObservation<decimal?>(2m, 0.55m, "visible scale reference"),
            finish: new FieldObservation<string?>("ReverseProof", 0.65m, "frosted fields, mirrored devices"),
            finishAttributes: ["HighRelief"],
            edge: new FieldObservation<string?>("Reeded", 0.60m, "regular reeding"),
            conditionNotes: ["light toning on reverse"],
            authenticityFlags: [new AuthenticityFlag("luster pattern unusual for claimed finish", "low")],
            imageQualityIssues: ["glare top-left of obverse"]));
    }
}

/// <summary>A hybrid-search port returning fixed candidates.</summary>
internal sealed class FakeCoinSearch : ICoinSearch
{
    public IReadOnlyList<CoinSearchQuery> ReceivedQueries { get; private set; } = [];

    public Task<IReadOnlyList<CoinCandidate>> SearchAsync(
        CoinSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ReceivedQueries = [.. ReceivedQueries, query];
        IReadOnlyList<CoinCandidate> candidates =
        [
            new CoinCandidate(new CoinTypeId(10201), 0.94m, "Fictional Libertad-style 2 oz Reverse Proof 2023"),
            new CoinCandidate(new CoinTypeId(10202), 0.61m, "Fictional Libertad-style 1 oz BU 2023"),
        ];
        return Task.FromResult(candidates);
    }
}

/// <summary>A perceptual hasher deterministic in the bytes (length-mixed), so identical input dedupes.</summary>
internal sealed class FakePerceptualHasher : IPerceptualHasher
{
    public Task<ulong> HashAsync(byte[] imageBytes, CancellationToken cancellationToken = default) =>
        Task.FromResult<ulong>(0x9E3779B97F4A7C15UL ^ (ulong)imageBytes.Length * 0x100000001B3UL);
}

/// <summary>An in-memory identification run store.</summary>
internal sealed class InMemoryRunStore : IIdentificationRunStore
{
    private readonly List<IdentificationRun> _runs = [];

    public Task AddAsync(IdentificationRun run, CancellationToken cancellationToken = default)
    {
        _runs.Add(run);
        return Task.CompletedTask;
    }

    public Task<IdentificationRun?> FindAsync(IdentificationRunId runId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.FirstOrDefault(r => r.Id == runId));

    public Task SaveAsync(IdentificationRun run, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IdentificationRun?> FindByObversePerceptualHashAsync(
        UserId userId,
        ulong perceptualHash,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_runs.FirstOrDefault(r => r.UserId == userId && r.ObversePerceptualHash == perceptualHash));

    public IReadOnlyList<IdentificationRun> Runs => _runs;
}

/// <summary>A queue that records enqueued run ids without processing them.</summary>
internal sealed class RecordingIdentificationQueue : IIdentificationQueue
{
    private readonly List<IdentificationRunId> _enqueued = [];

    public Task EnqueueAsync(IdentificationRunId runId, CancellationToken cancellationToken = default)
    {
        _enqueued.Add(runId);
        return Task.CompletedTask;
    }

    public IReadOnlyList<IdentificationRunId> Enqueued => _enqueued;
}
