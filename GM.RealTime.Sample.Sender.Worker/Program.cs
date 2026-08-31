using GM.Caching.Redis;
using GM.DistributedLock.Redis;
using GM.RealTime;
using GM.RealTime.Sample.Persistence;
using GM.RealTime.Sample.Sender.Worker;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
var redis = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

// Same Redis-backed presence registry the API writes to, so this worker sees who is online.
builder.Services.AddGMRedisCaching(o => o.ConnectionString = redis);
builder.Services.AddGMRedisDistributedLock(o => o.ConnectionString = redis);

// IRealTimeSender + the SignalR Redis backplane. This process hosts no hub endpoint; the backplane
// carries each send to whichever API instance holds the target user's connection.
builder.Services.AddGMRealTime(o => o.RedisBackplaneConnectionString = redis);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddSingleton<InboxDispatcher>();
builder.Services.AddHostedService<InboxSenderWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
    await context.Database.EnsureCreatedAsync();
}

await app.RunAsync();
