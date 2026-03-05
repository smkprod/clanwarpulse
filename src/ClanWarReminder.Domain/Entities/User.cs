using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PlatformType Platform { get; set; }
    public string PlatformUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlayerLink> PlayerLinks { get; set; } = new List<PlayerLink>();
}
