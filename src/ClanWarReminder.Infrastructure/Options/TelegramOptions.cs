namespace ClanWarReminder.Infrastructure.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";
    public string BotToken { get; set; } = string.Empty;
    public string BotUsername { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public int MaxAuthAgeMinutes { get; set; } = 60;
}
