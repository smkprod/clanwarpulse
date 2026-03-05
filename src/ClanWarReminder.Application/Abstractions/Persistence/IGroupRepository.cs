using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Application.Abstractions.Persistence;

public interface IGroupRepository
{
    Task<Group?> GetByPlatformGroupAsync(PlatformType platform, string platformGroupId, CancellationToken cancellationToken);
    Task<Group?> GetActiveByClanTagAsync(PlatformType platform, string clanTag, CancellationToken cancellationToken);
    Task<Group?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Group>> GetActiveAsync(CancellationToken cancellationToken);
    Task AddAsync(Group group, CancellationToken cancellationToken);
}
