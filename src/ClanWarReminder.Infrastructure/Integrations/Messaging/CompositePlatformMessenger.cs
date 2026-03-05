using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Infrastructure.Integrations.Messaging;

public class CompositePlatformMessenger : IPlatformMessenger
{
    private readonly IEnumerable<IPlatformMessenger> _messengers;

    public CompositePlatformMessenger(IEnumerable<IPlatformMessenger> messengers)
    {
        _messengers = messengers;
    }

    public async Task SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
    {
        foreach (var messenger in _messengers)
        {
            if (messenger is CompositePlatformMessenger)
            {
                continue;
            }

            await messenger.SendReminderAsync(message, cancellationToken);
        }
    }
}
