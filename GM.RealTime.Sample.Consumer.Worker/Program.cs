using GM.Messaging;
using GM.RealTime.Sample.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// GM.Messaging (Wolverine + RabbitMQ) discovers the Handle method in this assembly and delivers
// each consumed RealTimeMessageQueuedIntegrationEvent to it.
builder.Services.AddGMMessaging(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Create the inbox schema on first run (a sample shortcut; a real app would use migrations).
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InboxDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
