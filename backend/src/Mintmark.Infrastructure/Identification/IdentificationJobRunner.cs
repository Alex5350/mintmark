using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using Mintmark.Application.Ports;
using Mintmark.Domain;
using Mintmark.Infrastructure.Persistence;

namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// Channel-backed identification queue. The bound Application
/// IdentificationService completes a run inline (hash → vision → retrieval →
/// persist) and then enqueues its id; this queue hands the id to the
/// <see cref="IdentificationJobRunner"/> for post-processing.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The name mirrors the binding Application port IIdentificationQueue.")]
public sealed class IdentificationQueue : IIdentificationQueue
{
    private readonly Channel<IdentificationRunId> _channel =
        Channel.CreateBounded<IdentificationRunId>(new BoundedChannelOptions(1_000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });

    /// <inheritdoc />
    public async Task EnqueueAsync(IdentificationRunId runId, CancellationToken cancellationToken = default) =>
        await _channel.Writer.WriteAsync(runId, cancellationToken);

    /// <summary>Attempts to dequeue a pending run without waiting.</summary>
    public bool TryDequeue(out IdentificationRunId runId) => _channel.Reader.TryRead(out runId);

    /// <summary>Waits for the next queued run id.</summary>
    public async Task<IdentificationRunId> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}

/// <summary>
/// Drains the identification queue in the background and implements the
/// <see cref="IJobRunner"/> port. Post-persist steps performed per run:
/// verify the audit row actually committed (a lost write between
/// SaveChanges and enqueue is surfaced, not swallowed) and log completion.
/// The vision call itself is synchronous by design of the bound Application
/// service; hosted-model deployments that want it off the request path will
/// move it behind this runner — the queue boundary already exists. A plain
/// hosted service was chosen over a Quartz job because the queue is
/// in-process; there is nothing durable for a job store to add.
/// </summary>
public sealed class IdentificationJobRunner(
    IdentificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<IdentificationJobRunner> logger) : BackgroundService, IJobRunner
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IdentificationRunId runId;
            try
            {
                runId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessAsync(runId, stoppingToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        var ran = 0;
        while (queue.TryDequeue(out var runId))
        {
            await ProcessAsync(runId, cancellationToken);
            ran++;
        }

        return ran;
    }

    private async Task ProcessAsync(IdentificationRunId runId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MintmarkDbContext>();
            var committed = await dbContext.IdentificationRuns
                .AnyAsync(r => r.Id == runId, cancellationToken);

            if (!committed)
            {
                logger.LogError(
                    "Identification run {RunId} was enqueued but never committed — the audit trail is incomplete; resubmission required.",
                    runId.Value);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Post-processing identification run {RunId} failed", runId.Value);
        }
    }
}
