namespace ClanWarReminder.Domain.Entities;

public class PlayerLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public string PlayerTag { get; set; } = string.Empty;
    public DateTimeOffset LinkedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
