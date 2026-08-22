using Mintmark.Domain.Entities;

namespace Mintmark.Domain.Tests;

public class IdentificationRunTests
{
    private static readonly UserId UserId = new(42);
    private static readonly ImageId ObverseImageId = new(101);
    private static readonly ImageId ReverseImageId = new(102);
    private static readonly CoinTypeId CandidateA = new(201);
    private static readonly CoinTypeId CandidateB = new(202);

    private const string RawResponse = """
        {"country":{"value":"United States","confidence":0.93,"evidence":"liberty legend"}}
        """;

    private static IdentificationRun Start() => IdentificationRun.Start(
        UserId,
        modelName: "test-vision",
        modelVersion: "2026-06",
        promptTemplateVersion: "identify-v1",
        rawResponse: RawResponse,
        fieldConfidences: new Dictionary<string, decimal>
        {
            ["country"] = 0.93m,
            ["year"] = 0.71m,
            ["series"] = 0.80m,
        },
        candidates: [new IdentificationCandidate(CandidateA, 0.91m), new IdentificationCandidate(CandidateB, 0.64m)],
        obverseImageId: ObverseImageId,
        reverseImageId: ReverseImageId,
        obversePerceptualHash: 0x1234567890ABCDEFUL);

    [Fact]
    public void Start_MissingRawResponse_Throws()
    {
        Assert.Throws<ArgumentException>(() => IdentificationRun.Start(
            UserId, "test-vision", "2026-06", "identify-v1", "  ", new Dictionary<string, decimal>(), []));
    }

    [Fact]
    public void Start_ConfidenceOutOfRange_Throws()
    {
        Assert.Throws<ArgumentException>(() => IdentificationRun.Start(
            UserId, "test-vision", "2026-06", "identify-v1", RawResponse,
            new Dictionary<string, decimal> { ["country"] = 1.5m }, []));
    }

    [Fact]
    public void Confirm_RecordsUserDecision()
    {
        var run = Start();

        run.Confirm(CandidateA, correctedBy: "user-42");

        Assert.True(run.IsConfirmed);
        Assert.Equal(CandidateA, run.ConfirmedCoinTypeId);
        Assert.Equal("user-42", run.ConfirmedBy);
        Assert.NotNull(run.ConfirmedAtUtc);
    }

    [Fact]
    public void Confirm_SecondCall_Throws()
    {
        var run = Start();
        run.Confirm(CandidateA, correctedBy: "user-42");

        Assert.Throws<InvalidOperationException>(() => run.Confirm(CandidateB, correctedBy: "user-42"));
    }

    [Fact]
    public void Confirm_DoesNotMutateRawData()
    {
        var run = Start();
        var rawBefore = run.RawResponse;
        var confidencesBefore = run.FieldConfidences.ToDictionary(p => p.Key, p => p.Value);
        var candidatesBefore = run.Candidates.ToList();

        run.Confirm(CandidateA, correctedBy: "user-42");

        Assert.Equal(rawBefore, run.RawResponse);
        Assert.Equal(confidencesBefore, run.FieldConfidences);
        Assert.Equal(candidatesBefore, run.Candidates);
    }

    [Fact]
    public void Confirm_AcceptsCorrectionBeyondCandidates()
    {
        // The user may correct to a coin type that was not among the proposals.
        var run = Start();
        var correction = new CoinTypeId(999);
        run.Confirm(correction, correctedBy: "user-42");
        Assert.Equal(correction, run.ConfirmedCoinTypeId);
    }
}
