namespace ClanWarReminder.Domain.Entities;

public class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public string WarKey { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
    public Group Group { get; set; } = null!;
}
