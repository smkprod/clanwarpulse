using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Domain.Entities;

public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public PlatformType Platform { get; set; }
    public string PlatformGroupId { get; set; } = string.Empty;
    public string ClanTag { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlayerLink> PlayerLinks { get; set; } = new List<PlayerLink>();
}
