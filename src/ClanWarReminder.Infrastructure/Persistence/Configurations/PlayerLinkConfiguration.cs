using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClanWarReminder.Infrastructure.Persistence.Configurations;

public class PlayerLinkConfiguration : IEntityTypeConfiguration<PlayerLink>
{
    public void Configure(EntityTypeBuilder<PlayerLink> builder)
    {
        builder.ToTable("player_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PlayerTag).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.GroupId, x.PlayerTag }).IsUnique();

        builder.HasOne(x => x.Group)
            .WithMany(x => x.PlayerLinks)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.PlayerLinks)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
