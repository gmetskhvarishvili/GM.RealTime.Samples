using GM.Messaging.Domain.Inbox;
using GM.Messaging.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;

namespace GM.RealTime.Sample.Persistence;

/// <summary>The inbox database: one table of received-but-maybe-not-yet-delivered messages.</summary>
public sealed class InboxDbContext(DbContextOptions<InboxDbContext> options) : DbContext(options)
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Reuse GM.Messaging's inbox mapping (keyed on EventId + ConsumerName).
        modelBuilder.ApplyConfiguration(new InboxMessageConfiguration<InboxMessage>("realtime"));
    }
}
