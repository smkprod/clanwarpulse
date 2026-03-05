namespace ClanWarReminder.Infrastructure.Options;

public class WorkerOptions
{
    public const string SectionName = "Worker";
    public int PollMinutes { get; set; } = 20;
}
