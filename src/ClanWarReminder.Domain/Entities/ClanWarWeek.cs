namespace ClanWarReminder.Domain.Entities;

public class ClanWarWeek
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClanTag { get; set; } = string.Empty;
    public string ClanName { get; set; } = string.Empty;
    public string WarKey { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ClanWarWeekMember> Members { get; set; } = new List<ClanWarWeekMember>();
}
