using GM.Messaging.Domain.Inbox;
using GM.RealTime.Sample.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GM.RealTime.Sample.Sender.Worker;

/// <summary>
/// Polls the inbox for undelivered messages and pushes each to its user via <see cref="InboxDispatcher"/>,
/// marking it processed on success or failed on error. Runs in its own process — the SignalR Redis
/// backplane carries the message to whichever API instance holds the user's connection.
/// </summary>
public sealed class InboxSenderWorker(
    IServiceScopeFactory scopeFactory,
    InboxDispatcher dispatcher,
    ILogger<InboxSenderWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan _pollInterval =
        TimeSpan.FromSeconds(configuration.GetValue("InboxSender:PollIntervalSeconds", 3));
    private readonly int _batchSize =
        configuration.GetValue("InboxSender:BatchSize", 50);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inbox send batch failed.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<InboxDbContext>();

        var pending = await context.InboxMessages
            .Where(x => x.ProcessedAtUtc == null && x.Error == null)
            .OrderBy(x => x.ReceivedAtUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
            return;

        foreach (var message in pending)
        {
            try
            {
                await dispatcher.DispatchAsync(message, cancellationToken);
                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deliver inbox message {EventId}.", message.EventId);
                message.MarkFailed(ex.Message);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
