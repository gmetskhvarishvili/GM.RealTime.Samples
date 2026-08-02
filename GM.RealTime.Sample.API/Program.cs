using System.Security.Claims;
using System.Text;
using GM.RealTime;
using GM.RealTime.Domain;
using GM.RealTime.Sample.API;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));

// GM.RealTime: SignalR + presence registry (cache + distributed lock) + the access_token handshake.
// In-memory backends by default; register AddGMRedisCaching/AddGMRedisDistributedLock first for
// cross-node presence (see the README).
builder.Services.AddGMRealTime(o => o.HubPath = "/hubs/realtime");

// JWT bearer as GM.Identity would set it up. GM.RealTime plugs the access_token query string into
// this same validation for the WebSocket handshake.
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

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Dev-only helper: mint a token for a userId so a SignalR client can connect as that user.
app.MapPost("/dev/token/{userId}", (string userId) =>
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

// Push a message to a user — delivered only if they're online (presence-aware).
app.MapPost("/notify/{userId}", async (string userId, NotifyRequest request, UserNotifier notifier) =>
{
    var delivered = await notifier.NotifyIfOnlineAsync(userId, request.Event, request.Payload);
    return Results.Ok(new { userId, delivered });
}).RequireAuthorization();

// Presence lookup backed by the shared connection registry.
app.MapGet("/presence/{userId}", async (string userId, IConnectionRegistry registry) =>
{
    var presence = await registry.GetPresenceAsync(userId);
    return Results.Ok(new { userId, presence.IsOnline, connections = presence.ConnectionIds.Count });
});

// The real-time hub, mapped at RealTimeOptions.HubPath and requiring a valid JWT.
app.MapGMRealTimeHub().RequireAuthorization();

app.Run();

internal sealed record NotifyRequest(string Event, object? Payload);
