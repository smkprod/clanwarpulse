using ClanWarReminder.Application.Abstractions.Persistence;

namespace ClanWarReminder.Infrastructure.Persistence;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public EfUnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public void ClearChanges()
        => _dbContext.ChangeTracker.Clear();
}
