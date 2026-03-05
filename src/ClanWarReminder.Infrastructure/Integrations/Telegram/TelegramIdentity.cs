namespace ClanWarReminder.Infrastructure.Integrations.Telegram;

public sealed record TelegramIdentity(
    string UserId,
    string DisplayName,
    string? Username,
    DateTimeOffset AuthDateUtc);
