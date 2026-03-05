using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _db;

    public GroupRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Group?> GetByPlatformGroupAsync(PlatformType platform, string platformGroupId, CancellationToken cancellationToken)
        => _db.Groups.FirstOrDefaultAsync(x => x.Platform == platform && x.PlatformGroupId == platformGroupId, cancellationToken);

    public Task<Group?> GetActiveByClanTagAsync(PlatformType platform, string clanTag, CancellationToken cancellationToken)
        => _db.Groups.FirstOrDefaultAsync(
            x => x.Platform == platform && x.IsActive && x.ClanTag == clanTag,
            cancellationToken);

    public Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _db.Groups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Group>> GetActiveAsync(CancellationToken cancellationToken)
        => await _db.Groups
            .Where(x => x.IsActive)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task AddAsync(Group group, CancellationToken cancellationToken)
        => _db.Groups.AddAsync(group, cancellationToken).AsTask();
}
