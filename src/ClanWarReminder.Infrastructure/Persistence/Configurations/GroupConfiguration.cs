using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanWarReminder.Infrastructure.Persistence.Configurations;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.PlatformGroupId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ClanTag).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.Platform, x.PlatformGroupId }).IsUnique();
    }
}
