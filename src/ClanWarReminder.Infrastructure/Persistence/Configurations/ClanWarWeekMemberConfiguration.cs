using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanWarReminder.Infrastructure.Persistence.Configurations;

public class ClanWarWeekMemberConfiguration : IEntityTypeConfiguration<ClanWarWeekMember>
{
    public void Configure(EntityTypeBuilder<ClanWarWeekMember> builder)
    {
        builder.ToTable("clan_war_week_members");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlayerTag).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PlayerName).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ClanWarWeekId, x.PlayerTag }).IsUnique();
    }
}
