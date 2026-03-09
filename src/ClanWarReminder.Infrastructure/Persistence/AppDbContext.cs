using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Group> Groups => Set<Group>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PlayerLink> PlayerLinks => Set<PlayerLink>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ClanWarWeek> ClanWarWeeks => Set<ClanWarWeek>();
    public DbSet<ClanWarWeekMember> ClanWarWeekMembers => Set<ClanWarWeekMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
