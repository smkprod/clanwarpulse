using ClanWarReminder.Domain.Entities;

namespace ClanWarReminder.Application.Abstractions.Persistence;

public interface IReminderRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid groupId, string warKey, CancellationToken cancellationToken);
    Task AddAsync(Reminder reminder, CancellationToken cancellationToken);
}
