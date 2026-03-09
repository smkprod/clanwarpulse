using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanWarReminder.Infrastructure.Persistence.Configurations;

public class ClanWarWeekConfiguration : IEntityTypeConfiguration<ClanWarWeek>
{
    public void Configure(EntityTypeBuilder<ClanWarWeek> builder)
    {
        builder.ToTable("clan_war_weeks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ClanTag).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ClanName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.WarKey).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ClanTag, x.WarKey }).IsUnique();
        builder.HasMany(x => x.Members)
            .WithOne(x => x.ClanWarWeek)
            .HasForeignKey(x => x.ClanWarWeekId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
