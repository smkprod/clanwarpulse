using ClanWarReminder.Domain.Entities;

namespace ClanWarReminder.Application.Abstractions.Persistence;

public interface IPlayerLinkRepository
{
    Task<PlayerLink?> GetByGroupAndUserAsync(Guid groupId, Guid userId, CancellationToken cancellationToken);
    Task<PlayerLink?> GetLatestByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerLink>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerLink>> GetByGroupAsync(Guid groupId, CancellationToken cancellationToken);
    Task AddAsync(PlayerLink link, CancellationToken cancellationToken);
    Task UpdateAsync(PlayerLink link, CancellationToken cancellationToken);
    Task DeleteRangeAsync(IEnumerable<PlayerLink> links, CancellationToken cancellationToken);
}
