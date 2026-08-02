using GM.Messaging.Domain.Inbox;
using GM.Messaging.Persistence;
using GM.Messaging.Persistence.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GM.RealTime.Sample.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InboxDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("ApplicationDatabase")));

        services.AddScoped<IInboxStore<InboxMessage>, InboxStore>();
        services.AddGMInboxProcessor<InboxMessage>();

        return services;
    }
}
