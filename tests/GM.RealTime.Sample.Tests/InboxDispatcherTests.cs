using System.Text.Json;
using GM.Messaging.Domain.Inbox;
using GM.RealTime;
using GM.RealTime.Sample.Domain.Events;
using GM.RealTime.Sample.Sender.Worker;
using Xunit;

namespace GM.RealTime.Sample.Tests;

public class InboxDispatcherTests
{
    private sealed class RecordingSender : IRealTimeSender
    {
        public List<(string UserId, string Event, object? Payload)> Sends { get; } = [];

        public Task SendToUserAsync(string userId, string @event, object? payload, CancellationToken cancellationToken = default)
        {
            Sends.Add((userId, @event, payload));
            return Task.CompletedTask;
        }

        public Task SendToConnectionAsync(string connectionId, string @event, object? payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToGroupAsync(string group, string @event, object? payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendToAllAsync(string @event, object? payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static InboxMessage InboxRowFor(Guid userId, string title, string body)
    {
        var evt = new RealTimeMessageQueuedIntegrationEvent(title, body) { UserId = userId };
        var payload = JsonSerializer.Serialize(evt);
        return InboxMessage.Create(evt.EventId, "test-consumer", evt.GetType().FullName!, payload, userId);
    }

    [Fact]
    public async Task Dispatch_SendsTheMessageToTheTargetUser()
    {
        var sender = new RecordingSender();
        var dispatcher = new InboxDispatcher(sender);
        var userId = Guid.NewGuid();

        await dispatcher.DispatchAsync(InboxRowFor(userId, "Hi", "Welcome aboard"));

        var send = Assert.Single(sender.Sends);
        Assert.Equal(userId.ToString(), send.UserId);
        Assert.Equal("notification", send.Event);
    }

    [Fact]
    public async Task Dispatch_IsANoop_WhenTheInboxRowHasNoTargetUser()
    {
        var sender = new RecordingSender();
        var dispatcher = new InboxDispatcher(sender);

        // An inbox row with no UserId (userId: null) can't be targeted.
        var evt = new RealTimeMessageQueuedIntegrationEvent("Hi", "Body");
        var row = InboxMessage.Create(evt.EventId, "test-consumer", evt.GetType().FullName!, JsonSerializer.Serialize(evt), userId: null);

        await dispatcher.DispatchAsync(row);

        Assert.Empty(sender.Sends);
    }
}
