namespace Mintmark.Domain.Entities;

/// <summary>A candidate catalog match proposed during an identification run, with its hybrid search score.</summary>
public sealed record IdentificationCandidate(CoinTypeId CoinTypeId, decimal Score);

/// <summary>
/// The audit backbone of the identification pipeline. Initialized with the
/// raw model response, per-field confidences and candidate matches; the
/// user's confirmation is then recorded exactly once. Raw data is never
/// mutated after <see cref="Start"/> — append-only, never skipped.
/// </summary>
public sealed class IdentificationRun
{
    /// <summary>Parameterless constructor for EF Core materialization only.</summary>
    private IdentificationRun()
    {
    }

    private IdentificationRun(string modelName, string modelVersion, string promptTemplateVersion, string rawResponse)
    {
        ModelName = modelName;
        ModelVersion = modelVersion;
        PromptTemplateVersion = promptTemplateVersion;
        RawResponse = rawResponse;
    }

    /// <summary>Gets the persistence-assigned identifier (the job id surfaced to clients).</summary>
    public IdentificationRunId Id { get; private set; }

    /// <summary>Gets the owning user.</summary>
    public UserId UserId { get; private set; }

    /// <summary>Gets the obverse input image, when persisted.</summary>
    public ImageId? ObverseImageId { get; private set; }

    /// <summary>Gets the reverse input image, when persisted.</summary>
    public ImageId? ReverseImageId { get; private set; }

    /// <summary>Gets the edge input image, when supplied.</summary>
    public ImageId? EdgeImageId { get; private set; }

    /// <summary>Gets the perceptual hash of the obverse image (dedupe key).</summary>
    public ulong? ObversePerceptualHash { get; private set; }

    /// <summary>Gets the vision model name (or the literal <c>offline</c> provider label).</summary>
    public string ModelName { get; private set; } = string.Empty;

    /// <summary>Gets the vision model version.</summary>
    public string ModelVersion { get; private set; } = string.Empty;

    /// <summary>Gets the versioned prompt template that served the run (files in /prompts).</summary>
    public string PromptTemplateVersion { get; private set; } = string.Empty;

    /// <summary>Gets the raw structured model response. Immutable after <see cref="Start"/>.</summary>
    public string RawResponse { get; private set; } = string.Empty;

    /// <summary>Gets the per-field confidences keyed by field name. Immutable after <see cref="Start"/>.</summary>
    public IReadOnlyDictionary<string, decimal> FieldConfidences { get; private set; }
        = new Dictionary<string, decimal>();

    /// <summary>Gets the proposed catalog candidates with scores. Immutable after <see cref="Start"/>.</summary>
    public IReadOnlyList<IdentificationCandidate> Candidates { get; private set; } = [];

    /// <summary>Gets the confirmed/corrected coin type, once the user has decided.</summary>
    public CoinTypeId? ConfirmedCoinTypeId { get; private set; }

    /// <summary>Gets who recorded the confirmation (user or provider label), when confirmed.</summary>
    public string? ConfirmedBy { get; private set; }

    /// <summary>Gets when the user decision was recorded (UTC).</summary>
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    /// <summary>Gets when the run was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Gets a value indicating whether a user decision has been recorded.</summary>
    public bool IsConfirmed => ConfirmedCoinTypeId is not null;

    /// <summary>Starts a run, freezing the raw response and its confidences.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the raw response or model metadata is missing, or a
    /// confidence falls outside 0..1.
    /// </exception>
    public static IdentificationRun Start(
        UserId userId,
        string modelName,
        string modelVersion,
        string promptTemplateVersion,
        string rawResponse,
        IReadOnlyDictionary<string, decimal> fieldConfidences,
        IReadOnlyList<IdentificationCandidate> candidates,
        ImageId? obverseImageId = null,
        ImageId? reverseImageId = null,
        ImageId? edgeImageId = null,
        ulong? obversePerceptualHash = null,
        DateTimeOffset? createdAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new ArgumentException("Model name is required.", nameof(modelName));
        }

        if (string.IsNullOrWhiteSpace(modelVersion))
        {
            throw new ArgumentException("Model version is required.", nameof(modelVersion));
        }

        if (string.IsNullOrWhiteSpace(promptTemplateVersion))
        {
            throw new ArgumentException("Prompt template version is required.", nameof(promptTemplateVersion));
        }

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            throw new ArgumentException("The raw model response is required.", nameof(rawResponse));
        }

        foreach (var pair in fieldConfidences)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Field confidence keys must be non-empty.", nameof(fieldConfidences));
            }

            if (pair.Value is < 0m or > 1m)
            {
                throw new ArgumentException(
                    $"Confidence for '{pair.Key}' must be within 0..1; got {pair.Value}.", nameof(fieldConfidences));
            }
        }

        return new IdentificationRun(modelName.Trim(), modelVersion.Trim(), promptTemplateVersion.Trim(), rawResponse)
        {
            UserId = userId,
            ObverseImageId = obverseImageId,
            ReverseImageId = reverseImageId,
            EdgeImageId = edgeImageId,
            ObversePerceptualHash = obversePerceptualHash,
            FieldConfidences = new Dictionary<string, decimal>(fieldConfidences, StringComparer.Ordinal),
            Candidates = candidates.ToList(),
            CreatedAtUtc = createdAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Records the user's decision (confirmation of a candidate, or a
    /// correction to any coin type). May be called exactly once; raw data is
    /// never touched.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a decision was already recorded.</exception>
    public void Confirm(CoinTypeId candidateCoinTypeId, string? correctedBy = null)
    {
        if (IsConfirmed)
        {
            throw new InvalidOperationException(
                $"Identification run {Id} already recorded a user decision ({ConfirmedCoinTypeId}); runs are append-only.");
        }

        ConfirmedCoinTypeId = candidateCoinTypeId;
        ConfirmedBy = correctedBy;
        ConfirmedAtUtc = DateTimeOffset.UtcNow;
    }
}
