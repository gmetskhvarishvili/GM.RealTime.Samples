using System.Security.Claims;
using System.Text;
using GM.Caching.Redis;
using GM.DistributedLock.Redis;
using GM.Messaging;
using GM.RealTime;
using GM.RealTime.Domain;
using GM.RealTime.Sample.API;
using GM.RealTime.Sample.API.Contracts;
using GM.RealTime.Sample.Domain.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
var redis = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";

// Redis-backed presence registry, shared with the sender worker.
builder.Services.AddGMRedisCaching(o => o.ConnectionString = redis);
builder.Services.AddGMRedisDistributedLock(o => o.ConnectionString = redis);

// GM.RealTime: SignalR + presence + the JWT handshake, with the Redis backplane so a send from any
// instance (or the sender worker) reaches this instance's connected clients.
builder.Services.AddGMRealTime(o =>
{
    o.HubPath = "/hubs/realtime";
    o.RedisBackplaneConnectionString = redis;
});

// GM.Messaging producer — the /queue endpoint publishes the "queued" event to RabbitMQ.
builder.Services.AddGMMessaging(builder.Configuration);

// JWT bearer as GM.Identity would set it up; GM.RealTime plugs the access_token query string in.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<UserNotifier>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Liveness must not depend on downstream dependencies, so it runs no checks; readiness runs
// every registered health check (none here yet). See engineering baseline §11.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

var api = app.MapGroup("/api/v1");

// Dev-only helper: mint a token for a userId so a SignalR client can connect as that user.
api.MapPost("/dev/token/{userId}", (string userId) =>
{
    var descriptor = new SecurityTokenDescriptor
    {
        Issuer = jwt["Issuer"],
        Audience = jwt["Audience"],
        Subject = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim("name", userId)]),
        Expires = DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
    };
    var token = new JsonWebTokenHandler().CreateToken(descriptor);
    return Results.Ok(new { token });
});

// Enqueue a real-time message: publishes to RabbitMQ. The consumer worker ingests it into the inbox
// and the sender worker delivers it — demonstrating the full GM.Messaging -> inbox -> SignalR flow.
api.MapPost("/queue/{userId:guid}", async (Guid userId, QueueRequest request, IMessageBus bus) =>
{
    await bus.PublishAsync(new RealTimeMessageQueuedIntegrationEvent(request.Title, request.Body) { UserId = userId });
    return Results.Accepted();
});

// Direct push (bypasses the queue) to a user if online — presence-aware.
api.MapPost("/notify/{userId}", async (string userId, NotifyRequest request, UserNotifier notifier) =>
{
    var delivered = await notifier.NotifyIfOnlineAsync(userId, request.Event, request.Payload);
    return Results.Ok(new { userId, delivered });
}).RequireAuthorization();

// Presence lookup backed by the shared connection registry.
api.MapGet("/presence/{userId}", async (string userId, IConnectionRegistry registry) =>
{
    var presence = await registry.GetPresenceAsync(userId);
    return Results.Ok(new { userId, presence.IsOnline, connections = presence.ConnectionIds.Count });
});

// The real-time hub, mapped at RealTimeOptions.HubPath and requiring a valid JWT.
app.MapGMRealTimeHub().RequireAuthorization();

await app.RunAsync();
