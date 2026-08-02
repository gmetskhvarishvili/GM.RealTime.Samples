using GM.Messaging.Domain.Events;
using Wolverine.Attributes;

namespace GM.RealTime.Sample.Domain.Events;

/// <summary>
/// Published by any service that wants to push something to a user in real time. The consumer
/// worker ingests it into the inbox; the sender worker later delivers it over SignalR.
/// Set <see cref="IntegrationEvent.UserId"/> to the recipient.
/// </summary>
// The alias keeps the RabbitMQ message-type header stable across services (Wolverine resolves the
// local type from it), so publishers and consumers don't need to share the .NET type name.
[MessageIdentity("realtime.message.queued")]
public sealed record RealTimeMessageQueuedIntegrationEvent(string Title, string Body) : IntegrationEvent;
