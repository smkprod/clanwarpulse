using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<User?> GetByPlatformUserAsync(PlatformType platform, string platformUserId, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}
