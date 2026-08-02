using GM.RealTime;
using GM.RealTime.Domain;

namespace GM.RealTime.Sample.API;

/// <summary>
/// A tiny sample service showing presence-aware delivery: it only pushes to a user who currently
/// has a live connection, and reports whether the message was delivered or skipped.
/// </summary>
public sealed class UserNotifier(IRealTimeSender sender, IConnectionRegistry registry)
{
    public async Task<bool> NotifyIfOnlineAsync(
        string userId, string @event, object? payload, CancellationToken cancellationToken = default)
    {
        if (!await registry.IsOnlineAsync(userId, cancellationToken))
            return false; // offline — a real app might fall back to email/push here (see Herald)

        await sender.SendToUserAsync(userId, @event, payload, cancellationToken);
        return true;
    }
}
