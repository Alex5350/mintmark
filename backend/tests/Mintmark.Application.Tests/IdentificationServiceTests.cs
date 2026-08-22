using FluentAssertions;
using Mintmark.Application.Dtos;
using Mintmark.Application.Tests.TestInfrastructure;
using Mintmark.Application.UseCases;
using Mintmark.Domain;

namespace Mintmark.Application.Tests;

public class IdentificationServiceTests
{
    private readonly FakeVisionIdentifier _identifier = new();
    private readonly FakeCoinSearch _coinSearch = new();
    private readonly FakePerceptualHasher _hasher = new();
    private readonly InMemoryRunStore _store = new();
    private readonly RecordingIdentificationQueue _queue = new();

    private IdentificationService Service => new(_identifier, _coinSearch, _hasher, _store, _queue);

    private static SubmitIdentificationRequest RequestFor(byte[] obverse) =>
        new(obverse, obverse, edgeImage: null);

    [Fact]
    public async Task Submit_PersistsRun_And_Enqueues()
    {
        var obverse = new byte[20_000];

        var response = await Service.SubmitAsync(FixtureCoins.OwnerId, RequestFor(obverse));

        response.Deduplicated.Should().BeFalse();
        _queue.Enqueued.Should().ContainSingle().Which.Should().Be(response.JobId);

        var run = await _store.FindAsync(response.JobId);
        run.Should().NotBeNull();
        run!.RawResponse.Should().Be(FakeVisionIdentifier.RawResponse);
        run.ModelName.Should().Be("fixture-vision");
        run.ModelVersion.Should().Be("1.2.3");
        run.PromptTemplateVersion.Should().Be(PromptCatalog.IdentifyPromptTemplateVersion);
        run.ObversePerceptualHash.Should().NotBeNull();

        // Per-field confidences extracted from the vision result.
        run.FieldConfidences["series"].Should().Be(0.87m);
        run.FieldConfidences["year"].Should().Be(0.75m);

        // Candidates carried over from hybrid search.
        run.Candidates.Select(c => c.CoinTypeId.Value).Should().ContainInOrder(10201L, 10202L);
    }

    [Fact]
    public async Task Submit_Deduplicates_ByPerceptualHash()
    {
        var obverse = new byte[20_000];

        var first = await Service.SubmitAsync(FixtureCoins.OwnerId, RequestFor(obverse));
        var second = await Service.SubmitAsync(FixtureCoins.OwnerId, RequestFor(obverse));

        second.JobId.Should().Be(first.JobId);
        second.Deduplicated.Should().BeTrue();

        // The vision provider is not billed twice for the same photo.
        _identifier.CallCount.Should().Be(1);
        _store.Runs.Should().ContainSingle();
        _queue.Enqueued.Should().ContainSingle();
    }

    [Fact]
    public async Task Confirm_RecordsDecision_ExactlyOnce()
    {
        var response = await Service.SubmitAsync(FixtureCoins.OwnerId, RequestFor(new byte[20_000]));

        await Service.ConfirmAsync(
            response.JobId,
            new ConfirmIdentificationRequest(CoinTypeId: 10201, CorrectedBy: "user-501"));

        var run = await _store.FindAsync(response.JobId);
        run!.IsConfirmed.Should().BeTrue();
        run.ConfirmedCoinTypeId!.Value.Value.Should().Be(10201L);
        run.ConfirmedBy.Should().Be("user-501");

        var act = () => Service.ConfirmAsync(
            response.JobId,
            new ConfirmIdentificationRequest(CoinTypeId: 10202));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetStatus_MapsRun_ToResponse()
    {
        var response = await Service.SubmitAsync(FixtureCoins.OwnerId, RequestFor(new byte[20_000]));

        var status = await Service.GetStatusAsync(response.JobId);

        status.JobId.Should().Be(response.JobId);
        status.Status.Should().Be(IdentificationJobStatus.AwaitingConfirmation);
        status.ProviderLabel.Should().Be("fixture-vision");
        status.PromptTemplateVersion.Should().Be("identify-v1");
        status.PerFieldConfidences.Keys.Should().Contain("country").And.Contain("series").And.Contain("year").And.Contain("finish");
        status.Candidates.Should().HaveCount(2);
        status.ConfirmedCoinTypeId.Should().BeNull();

        await Service.ConfirmAsync(
            response.JobId,
            new ConfirmIdentificationRequest(CoinTypeId: 10201));
        var confirmed = await Service.GetStatusAsync(response.JobId);
        confirmed.Status.Should().Be(IdentificationJobStatus.Confirmed);
        confirmed.ConfirmedCoinTypeId!.Value.Value.Should().Be(10201L);
    }

    [Fact]
    public async Task GetStatus_UnknownRun_Throws()
    {
        var act = () => Service.GetStatusAsync(new IdentificationRunId(4242));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
