using GM.Caching;
using GM.DistributedLock;
using GM.RealTime;
using GM.RealTime.Domain;
using GM.RealTime.Persistence;
using GM.RealTime.Sample.API;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace GM.RealTime.Sample.Tests;

public class UserNotifierTests
{
    // Records what the notifier tried to send, standing in for the SignalR-backed sender.
    private sealed class RecordingSender : IRealTimeSender
    {
        public List<(string UserId, string Event)> UserSends { get; } = [];

        public Task SendToUserAsync(string userId, string @event, object? payload, CancellationToken ct = default)
        {
            UserSends.Add((userId, @event));
            return Task.CompletedTask;
        }

        public Task SendToConnectionAsync(string connectionId, string @event, object? payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToGroupAsync(string group, string @event, object? payload, CancellationToken ct = default) => Task.CompletedTask;
        public Task SendToAllAsync(string @event, object? payload, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (UserNotifier Notifier, IConnectionRegistry Registry, RecordingSender Sender) Build()
    {
        var cache = new MemoryCacheService(new MemoryCache(new MemoryCacheOptions()), Options.Create(new CacheServiceOptions()));
        IConnectionRegistry registry = new CacheConnectionRegistry(cache, new InMemoryDistributedLock(), Options.Create(new ConnectionRegistryOptions()));
        var sender = new RecordingSender();
        return (new UserNotifier(sender, registry), registry, sender);
    }

    [Fact]
    public async Task NotifyIfOnline_Sends_WhenTheUserHasAConnection()
    {
        var (notifier, registry, sender) = Build();
        await registry.AddConnectionAsync("user-1", "conn-a");

        var delivered = await notifier.NotifyIfOnlineAsync("user-1", "ping", new { hello = "world" });

        Assert.True(delivered);
        Assert.Single(sender.UserSends);
        Assert.Equal(("user-1", "ping"), sender.UserSends[0]);
    }

    [Fact]
    public async Task NotifyIfOnline_Skips_WhenTheUserIsOffline()
    {
        var (notifier, _, sender) = Build();

        var delivered = await notifier.NotifyIfOnlineAsync("ghost", "ping", null);

        Assert.False(delivered);
        Assert.Empty(sender.UserSends);
    }

    [Fact]
    public async Task NotifyIfOnline_Skips_AfterTheUsersLastConnectionGoesAway()
    {
        var (notifier, registry, sender) = Build();
        await registry.AddConnectionAsync("user-1", "conn-a");
        await registry.RemoveConnectionAsync("user-1", "conn-a");

        var delivered = await notifier.NotifyIfOnlineAsync("user-1", "ping", null);

        Assert.False(delivered);
        Assert.Empty(sender.UserSends);
    }
}
