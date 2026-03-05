using Microsoft.Extensions.DependencyInjection;

namespace ClanWarReminder.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Services.ClanSetupService>();
        services.AddScoped<Services.PlayerLinkService>();
        services.AddScoped<Services.ClanStatusService>();
        services.AddScoped<Services.WarReminderService>();
        return services;
    }
}
