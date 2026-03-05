using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly AppDbContext _db;

    public ReminderRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(Guid userId, Guid groupId, string warKey, CancellationToken cancellationToken)
        => _db.Reminders.AnyAsync(x => x.UserId == userId && x.GroupId == groupId && x.WarKey == warKey, cancellationToken);

    public Task AddAsync(Reminder reminder, CancellationToken cancellationToken)
        => _db.Reminders.AddAsync(reminder, cancellationToken).AsTask();
}
