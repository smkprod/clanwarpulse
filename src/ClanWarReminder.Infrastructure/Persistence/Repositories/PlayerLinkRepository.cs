using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence.Repositories;

public class PlayerLinkRepository : IPlayerLinkRepository
{
    private readonly AppDbContext _db;

    public PlayerLinkRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<PlayerLink?> GetByGroupAndUserAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
        => _db.PlayerLinks.FirstOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<PlayerLink>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken)
        => await _db.PlayerLinks
            .Include(x => x.User)
            .Where(x => x.GroupId == groupId)
            .ToListAsync(cancellationToken);

    public Task AddAsync(PlayerLink link, CancellationToken cancellationToken)
        => _db.PlayerLinks.AddAsync(link, cancellationToken).AsTask();

    public Task UpdateAsync(PlayerLink link, CancellationToken cancellationToken)
    {
        _db.PlayerLinks.Update(link);
        return Task.CompletedTask;
    }
}
