using Mintmark.Application.Dtos;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Domain.Entities;

namespace Mintmark.Application.UseCases;

/// <summary>
/// Identification orchestration: submit (hash, dedupe, identify, search
/// candidates, persist the run, enqueue) and confirm (record the user
/// decision exactly once). Runs are the audit backbone — append-only, never
/// skipped.
/// </summary>
public sealed class IdentificationService
{
    /// <summary>The provider label used when no vision provider was available.</summary>
    public const string OfflineProviderLabel = "offline";

    private readonly IVisionIdentifier _identifier;
    private readonly ICoinSearch _coinSearch;
    private readonly IPerceptualHasher _hasher;
    private readonly IIdentificationRunStore _store;
    private readonly IIdentificationQueue _queue;

    /// <summary>Initializes the service with its ports.</summary>
    public IdentificationService(
        IVisionIdentifier identifier,
        ICoinSearch coinSearch,
        IPerceptualHasher hasher,
        IIdentificationRunStore store,
        IIdentificationQueue queue)
    {
        _identifier = identifier;
        _coinSearch = coinSearch;
        _hasher = hasher;
        _store = store;
        _queue = queue;
    }

    /// <summary>
    /// Submits an identification. The obverse image is perceptually hashed
    /// first: an existing run with the same hash is returned as-is (dedupe).
    /// Otherwise the vision port runs, hybrid search proposes candidates, the
    /// run is persisted with its raw response and confidences, and the job is
    /// enqueued.
    /// </summary>
    public async Task<SubmitIdentificationResponse> SubmitAsync(
        UserId userId,
        SubmitIdentificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var obverse = request.ObverseImage ?? throw new ArgumentException("An obverse image is required.");
        var reverse = request.ReverseImage ?? throw new ArgumentException("A reverse image is required.");

        // Dedupe by perceptual hash: identical-looking submissions return the
        // original run instead of starting a new one.
        var perceptualHash = await _hasher.HashAsync(obverse, cancellationToken);
        var existing = await _store.FindByObversePerceptualHashAsync(userId, perceptualHash, cancellationToken);
        if (existing is not null)
        {
            return new SubmitIdentificationResponse(existing.Id, Deduplicated: true);
        }

        var vision = await _identifier.IdentifyAsync(
            new ImageInput(obverse, reverse, request.EdgeImage), cancellationToken);

        var candidates = await _coinSearch.SearchAsync(BuildQuery(vision, perceptualHash), cancellationToken);

        var run = IdentificationRun.Start(
            userId,
            vision.ModelName,
            vision.ModelVersion,
            PromptCatalog.IdentifyPromptTemplateVersion,
            vision.RawResponse,
            ExtractConfidences(vision),
            candidates.Select(c => new IdentificationCandidate(c.CoinTypeId, c.Score)).ToList(),
            obversePerceptualHash: perceptualHash);

        await _store.AddAsync(run, cancellationToken);
        await _queue.EnqueueAsync(run.Id, cancellationToken);

        return new SubmitIdentificationResponse(run.Id, Deduplicated: false);
    }

    /// <summary>Records the user's confirmation/correction on a run (append-only; exactly once).</summary>
    /// <exception cref="InvalidOperationException">Thrown when the run does not exist or was already confirmed.</exception>
    public async Task ConfirmAsync(
        IdentificationRunId runId,
        ConfirmIdentificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var run = await _store.FindAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Identification run {runId} was not found.");

        run.Confirm(new CoinTypeId(request.CoinTypeId), request.CorrectedBy);
        await _store.SaveAsync(run, cancellationToken);
    }

    /// <summary>Builds the status response for a run, deriving the lifecycle state from its data.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the run does not exist.</exception>
    public async Task<IdentificationStatusResponse> GetStatusAsync(
        IdentificationRunId runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _store.FindAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Identification run {runId} was not found.");

        var status = run.IsConfirmed ? IdentificationJobStatus.Confirmed : IdentificationJobStatus.AwaitingConfirmation;

        return new IdentificationStatusResponse(
            run.Id,
            status,
            string.IsNullOrWhiteSpace(run.ModelName) ? OfflineProviderLabel : run.ModelName,
            run.PromptTemplateVersion,
            run.CreatedAtUtc,
            run.FieldConfidences,
            run.Candidates.Select(c => new IdentificationCandidateDto(c.CoinTypeId, c.Score)).ToList(),
            run.ConfirmedCoinTypeId);
    }

    private static CoinSearchQuery BuildQuery(VisionIdentification vision, ulong perceptualHash) => new()
    {
        FreeText = JoinNonEmpty(
            vision.Country.Value,
            vision.Series.Value,
            vision.Year.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Country = vision.Country.Value,
        Series = vision.Series.Value,
        Year = vision.Year.Value,
        Fineness = vision.Fineness.Value,
        PerceptualHash = perceptualHash,
        Limit = 5,
    };

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var joined = string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static Dictionary<string, decimal> ExtractConfidences(VisionIdentification vision)
        => new Dictionary<string, decimal>
        {
            ["country"] = vision.Country.Confidence,
            ["mint"] = vision.Mint.Confidence,
            ["series"] = vision.Series.Confidence,
            ["year"] = vision.Year.Confidence,
            ["denomination"] = vision.Denomination.Confidence,
            ["metal"] = vision.Metal.Confidence,
            ["fineness"] = vision.Fineness.Confidence,
            ["sizeEstimateTroyOz"] = vision.SizeEstimateTroyOz.Confidence,
            ["finish"] = vision.Finish.Confidence,
            ["edge"] = vision.Edge.Confidence,
        };
}
