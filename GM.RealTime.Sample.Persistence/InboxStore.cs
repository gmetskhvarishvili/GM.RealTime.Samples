using GM.Messaging.Domain.Inbox;
using GM.Messaging.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;

namespace GM.RealTime.Sample.Persistence;

/// <summary>
/// The <see cref="IInboxStore{TInboxMessage}"/> GM.Messaging's inbox processor writes through when
/// the consumer worker ingests a message — a thin wrapper over <see cref="InboxDbContext"/>.
/// </summary>
public sealed class InboxStore(InboxDbContext context) : IInboxStore<InboxMessage>
{
    public Task<bool> ExistsAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default) =>
        context.InboxMessages.AnyAsync(x => x.EventId == eventId && x.ConsumerName == consumerName, cancellationToken);

    public async Task<InboxMessage> CreateAndAddAsync(
        Guid eventId, string consumerName, string eventType, string payload, Guid? userId, CancellationToken cancellationToken = default)
    {
        var message = InboxMessage.Create(eventId, consumerName, eventType, payload, userId);
        await context.InboxMessages.AddAsync(message, cancellationToken);
        return message;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
