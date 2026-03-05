using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Models;
using Microsoft.Extensions.Logging;

namespace ClanWarReminder.Infrastructure.Integrations.Messaging;

public class DiscordMessenger : IPlatformMessenger
{
    private readonly ILogger<DiscordMessenger> _logger;

    public DiscordMessenger(ILogger<DiscordMessenger> logger)
    {
        _logger = logger;
    }

    public Task SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
    {
        if (message.Platform != Domain.Enums.PlatformType.Discord)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Discord reminder to user {UserId} in group {GroupId}: {Text}", message.PlatformUserId, message.PlatformGroupId, message.Text);
        return Task.CompletedTask;
    }
}
