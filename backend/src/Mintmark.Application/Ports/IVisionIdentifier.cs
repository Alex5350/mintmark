namespace Mintmark.Application.Ports;

/// <summary>
/// A single observed field from the vision model, matching the identify-v1
/// prompt contract: every reported field is {"value", "confidence",
/// "evidence"}. Null beats guessing — a wrong year is worse than no year.
/// </summary>
/// <typeparam name="T">The field's CLR type (nullable annotations flow through the type argument).</typeparam>
/// <param name="Value">The observed value; <c>null</c> when no evidence was visible.</param>
/// <param name="Confidence">Model confidence in [0, 1].</param>
/// <param name="Evidence">A specific visual cue backing the value.</param>
public sealed record FieldObservation<T>(T Value, decimal Confidence, string? Evidence = null);

/// <summary>An advisory authenticity signal: an observation, never a verdict.</summary>
/// <param name="Signal">What was observed (e.g. irregular reeding spacing).</param>
/// <param name="Severity">Observational concern: low | medium | high, or null.</param>
public sealed record AuthenticityFlag(string? Signal, string? Severity);

/// <summary>The image bytes for one identification request.</summary>
/// <param name="ObverseBytes">Obverse (front) image — required.</param>
/// <param name="ReverseBytes">Reverse (back) image — required.</param>
/// <param name="EdgeBytes">Edge image, when supplied.</param>
public sealed record ImageInput(byte[] ObverseBytes, byte[] ReverseBytes, byte[]? EdgeBytes = null);

/// <summary>
/// The parsed identify-v1 contract response (see /prompts/identify-v1.md).
/// Field names mirror the JSON keys of the prompt's required shape; the
/// verbatim model output is retained in <see cref="RawResponse"/> for the
/// audit trail.
/// </summary>
public sealed record VisionIdentification
{
    /// <summary>Initializes the identification result.</summary>
    public VisionIdentification(
        string modelName,
        string modelVersion,
        string rawResponse,
        FieldObservation<string?> country,
        FieldObservation<string?> mint,
        FieldObservation<string?> series,
        FieldObservation<int?> year,
        FieldObservation<string?> denomination,
        FieldObservation<string?> metal,
        FieldObservation<decimal?> fineness,
        FieldObservation<decimal?> sizeEstimateTroyOz,
        FieldObservation<string?> finish,
        IReadOnlyList<string> finishAttributes,
        FieldObservation<string?> edge,
        IReadOnlyList<string> conditionNotes,
        IReadOnlyList<AuthenticityFlag> authenticityFlags,
        IReadOnlyList<string> imageQualityIssues)
    {
        ModelName = modelName;
        ModelVersion = modelVersion;
        RawResponse = rawResponse;
        Country = country;
        Mint = mint;
        Series = series;
        Year = year;
        Denomination = denomination;
        Metal = metal;
        Fineness = fineness;
        SizeEstimateTroyOz = sizeEstimateTroyOz;
        Finish = finish;
        FinishAttributes = finishAttributes;
        Edge = edge;
        ConditionNotes = conditionNotes;
        AuthenticityFlags = authenticityFlags;
        ImageQualityIssues = imageQualityIssues;
    }

    /// <summary>Gets the vision model name that produced the result.</summary>
    public string ModelName { get; }

    /// <summary>Gets the vision model version.</summary>
    public string ModelVersion { get; }

    /// <summary>Gets the verbatim raw model output (JSON), recorded on the identification run.</summary>
    public string RawResponse { get; }

    /// <summary>Gets the observed country.</summary>
    public FieldObservation<string?> Country { get; }

    /// <summary>Gets the observed mint (name or mark).</summary>
    public FieldObservation<string?> Mint { get; }

    /// <summary>Gets the observed series.</summary>
    public FieldObservation<string?> Series { get; }

    /// <summary>Gets the observed year, read from the date — never inferred from a design era.</summary>
    public FieldObservation<int?> Year { get; }

    /// <summary>Gets the observed denomination.</summary>
    public FieldObservation<string?> Denomination { get; }

    /// <summary>Gets the observed metal.</summary>
    public FieldObservation<string?> Metal { get; }

    /// <summary>Gets the observed fineness.</summary>
    public FieldObservation<decimal?> Fineness { get; }

    /// <summary>Gets the size estimate in troy ounces; only reported when a scale reference is visible.</summary>
    public FieldObservation<decimal?> SizeEstimateTroyOz { get; }

    /// <summary>Gets the observed primary finish.</summary>
    public FieldObservation<string?> Finish { get; }

    /// <summary>Gets the observed finish attribute flags.</summary>
    public IReadOnlyList<string> FinishAttributes { get; }

    /// <summary>Gets the observed edge type.</summary>
    public FieldObservation<string?> Edge { get; }

    /// <summary>Gets observable condition facts (marks, spotting, toning, luster).</summary>
    public IReadOnlyList<string> ConditionNotes { get; }

    /// <summary>Gets advisory authenticity signals (observations, never verdicts).</summary>
    public IReadOnlyList<AuthenticityFlag> AuthenticityFlags { get; }

    /// <summary>Gets image quality issues limiting confidence (glare, blur, obstruction).</summary>
    public IReadOnlyList<string> ImageQualityIssues { get; }
}

/// <summary>
/// Port to the vision identification model. Implemented by Infrastructure
/// against the model provider, using the versioned prompt from
/// <see cref="PromptCatalog"/>.
/// </summary>
public interface IVisionIdentifier
{
    /// <summary>Identifies one coin from its photos, returning the identify-v1 contract fields.</summary>
    Task<VisionIdentification> IdentifyAsync(ImageInput input, CancellationToken cancellationToken = default);
}
