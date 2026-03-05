using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Infrastructure.Integrations.ClashRoyale;
using ClanWarReminder.Infrastructure.Integrations.Messaging;
using ClanWarReminder.Infrastructure.Integrations.Telegram;
using ClanWarReminder.Infrastructure.Options;
using ClanWarReminder.Infrastructure.Persistence;
using ClanWarReminder.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClanWarReminder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ClashRoyaleOptions>(configuration.GetSection(ClashRoyaleOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerLinkRepository, PlayerLinkRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();

        services.AddHttpClient<ClashRoyaleClient>();
        services.AddHttpClient<TelegramBotProfileResolver>();
        services.AddHttpClient<TelegramMessenger>();
        services.AddScoped<IClashRoyaleClient, ClashRoyaleClient>();
        services.AddSingleton<TelegramInitDataValidator>();

        services.AddScoped<DiscordMessenger>();
        services.AddScoped<IPlatformMessenger>(sp =>
        {
            var messengers = new IPlatformMessenger[]
            {
                sp.GetRequiredService<TelegramMessenger>(),
                sp.GetRequiredService<DiscordMessenger>()
            };

            return new CompositePlatformMessenger(messengers);
        });

        return services;
    }
}
