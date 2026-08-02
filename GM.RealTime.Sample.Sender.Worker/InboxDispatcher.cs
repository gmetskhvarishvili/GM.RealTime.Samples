using System.Text.Json;
using GM.Messaging.Domain.Inbox;
using GM.RealTime;
using GM.RealTime.Sample.Domain.Events;

namespace GM.RealTime.Sample.Sender.Worker;

/// <summary>
/// Turns one inbox row into a real-time push. Kept separate from the polling loop so the delivery
/// logic is unit-testable without a database or SignalR host.
/// </summary>
public sealed class InboxDispatcher(IRealTimeSender sender)
{
    public async Task DispatchAsync(InboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message.UserId is not { } userId)
            return; // nothing to target

        var payload = JsonSerializer.Deserialize<RealTimeMessageQueuedIntegrationEvent>(message.Payload)
                      ?? throw new InvalidOperationException($"Could not deserialize inbox payload for event {message.EventId}.");

        // SendToUserAsync resolves the user's live connections from the shared registry and (with the
        // Redis backplane) delivers to them even though this worker hosts no connections itself.
        await sender.SendToUserAsync(
            userId.ToString(),
            "notification",
            new { payload.Title, payload.Body },
            cancellationToken);
    }
}
