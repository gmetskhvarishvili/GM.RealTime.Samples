using GM.Messaging.Persistence.Inbox;
using GM.RealTime.Sample.Domain.Events;

namespace GM.RealTime.Sample.Consumer.Worker.Handlers;

/// <summary>
/// Wolverine handler: receives the integration event from RabbitMQ and <b>ingests</b> it into the
/// inbox (idempotently, keyed on EventId + consumer). It does not deliver — the sender worker polls
/// the inbox and pushes to the client. This decouples "we received it" from "we delivered it".
/// </summary>
public sealed class RealTimeMessageQueuedHandler(IInboxProcessor inbox)
{
    public Task Handle(RealTimeMessageQueuedIntegrationEvent message, CancellationToken cancellationToken) =>
        inbox.IngestAsync(message, "GM.RealTime.Sample.Consumer.Worker", cancellationToken);
}
