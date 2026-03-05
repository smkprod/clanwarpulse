using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanWarReminder.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.PlatformUserId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.Platform, x.PlatformUserId }).IsUnique();
    }
}
