using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Application.Abstractions.Integrations;

public interface IPlatformMessenger
{
    Task SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken);
}
