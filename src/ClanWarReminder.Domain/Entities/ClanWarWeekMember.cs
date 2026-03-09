namespace ClanWarReminder.Domain.Entities;

public class ClanWarWeekMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClanWarWeekId { get; set; }
    public string PlayerTag { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int BattlesPlayed { get; set; }
    public int MaxBattles { get; set; }
    public int TotalContribution { get; set; }
    public double AverageContributionPerBattle { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ClanWarWeek ClanWarWeek { get; set; } = null!;
}
