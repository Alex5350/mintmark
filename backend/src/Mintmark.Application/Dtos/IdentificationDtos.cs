using Mintmark.Domain;

namespace Mintmark.Application.Dtos;

/// <summary>Submission for coin identification. Obverse and reverse are required.</summary>
public sealed record SubmitIdentificationRequest
{
    /// <summary>Initializes the request.</summary>
    public SubmitIdentificationRequest(byte[]? obverseImage, byte[]? reverseImage, byte[]? edgeImage = null)
    {
        ObverseImage = obverseImage;
        ReverseImage = reverseImage;
        EdgeImage = edgeImage;
    }

    /// <summary>Gets the obverse (front) image bytes; required, between 10 KB and 15 MB.</summary>
    public byte[]? ObverseImage { get; }

    /// <summary>Gets the reverse (back) image bytes; required, between 10 KB and 15 MB.</summary>
    public byte[]? ReverseImage { get; }

    /// <summary>Gets the optional edge image bytes; when present, at most 15 MB.</summary>
    public byte[]? EdgeImage { get; }
}

/// <summary>Lifecycle state of an identification job.</summary>
public enum IdentificationJobStatus
{
    /// <summary>Accepted, waiting to be processed.</summary>
    Queued,

    /// <summary>Vision analysis done; waiting for the user to confirm a candidate.</summary>
    AwaitingConfirmation,

    /// <summary>User decision recorded.</summary>
    Confirmed,

    /// <summary>Processing failed; the run may be resubmitted.</summary>
    Failed,
}

/// <summary>Response after submitting an identification.</summary>
/// <param name="JobId">The identification run id to poll.</param>
/// <param name="Deduplicated">True when an existing run with the same perceptual hash was returned instead.</param>
public sealed record SubmitIdentificationResponse(IdentificationRunId JobId, bool Deduplicated);

/// <summary>A candidate with its hybrid-search score.</summary>
/// <param name="CoinTypeId">The proposed catalog row.</param>
/// <param name="Score">The blended match score.</param>
public sealed record IdentificationCandidateDto(CoinTypeId CoinTypeId, decimal Score);

/// <summary>Status of an identification run, including per-field confidences and candidates.</summary>
public sealed record IdentificationStatusResponse
{
    /// <summary>Initializes the response.</summary>
    public IdentificationStatusResponse(
        IdentificationRunId jobId,
        IdentificationJobStatus status,
        string providerLabel,
        string promptTemplateVersion,
        DateTimeOffset createdAtUtc,
        IReadOnlyDictionary<string, decimal> perFieldConfidences,
        IReadOnlyList<IdentificationCandidateDto> candidates,
        CoinTypeId? confirmedCoinTypeId)
    {
        JobId = jobId;
        Status = status;
        ProviderLabel = providerLabel;
        PromptTemplateVersion = promptTemplateVersion;
        CreatedAtUtc = createdAtUtc;
        PerFieldConfidences = perFieldConfidences;
        Candidates = candidates;
        ConfirmedCoinTypeId = confirmedCoinTypeId;
    }

    /// <summary>Gets the run/job identifier.</summary>
    public IdentificationRunId JobId { get; }

    /// <summary>Gets the job lifecycle state.</summary>
    public IdentificationJobStatus Status { get; }

    /// <summary>
    /// Gets the provider label: the vision model name that served the run, or
    /// the literal <c>offline</c> when no provider was available.
    /// </summary>
    public string ProviderLabel { get; }

    /// <summary>Gets the prompt template version that served the run.</summary>
    public string PromptTemplateVersion { get; }

    /// <summary>Gets when the run was created (UTC).</summary>
    public DateTimeOffset CreatedAtUtc { get; }

    /// <summary>Gets per-field confidences keyed by contract field name.</summary>
    public IReadOnlyDictionary<string, decimal> PerFieldConfidences { get; }

    /// <summary>Gets the proposed candidates, best first.</summary>
    public IReadOnlyList<IdentificationCandidateDto> Candidates { get; }

    /// <summary>Gets the confirmed/corrected coin type, once decided.</summary>
    public CoinTypeId? ConfirmedCoinTypeId { get; }
}

/// <summary>User decision on an identification run: confirm a candidate or correct to any coin type.</summary>
/// <param name="CoinTypeId">The chosen catalog row.</param>
/// <param name="CorrectedBy">Who made the decision (user id or label), for the audit trail.</param>
public sealed record ConfirmIdentificationRequest(long CoinTypeId, string? CorrectedBy = null);
