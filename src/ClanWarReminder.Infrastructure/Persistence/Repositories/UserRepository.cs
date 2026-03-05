using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Domain.Entities;
using ClanWarReminder.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByPlatformUserAsync(PlatformType platform, string platformUserId, CancellationToken cancellationToken)
        => _db.Users.FirstOrDefaultAsync(x => x.Platform == platform && x.PlatformUserId == platformUserId, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(User user, CancellationToken cancellationToken)
        => _db.Users.AddAsync(user, cancellationToken).AsTask();
}
