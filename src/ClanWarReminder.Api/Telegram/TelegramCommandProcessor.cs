using System.Net;
using System.Text.Json.Serialization;
using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Application.Services;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Api.Telegram;

public sealed class TelegramCommandProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TelegramCommandProcessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task HandleMessageAsync(TelegramInboundMessage message, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].Split('@', 2)[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();
        var chatId = message.Chat.Id.ToString();

        using var scope = _scopeFactory.CreateScope();
        var messenger = scope.ServiceProvider.GetRequiredService<IPlatformMessenger>();

        switch (command)
        {
            case "/start":
            case "/help":
                await SendTextAsync(messenger, chatId,
                    "Команды:\n/setup #CLANTAG\n/link #PLAYERTAG\n/status\n/tagnotplayed\n/help",
                    cancellationToken);
                return;

            case "/setup":
                await HandleSetupAsync(scope.ServiceProvider, messenger, chatId, args, cancellationToken);
                return;

            case "/status":
                await HandleStatusAsync(scope.ServiceProvider, messenger, chatId, cancellationToken);
                return;

            case "/link":
                await HandleLinkAsync(scope.ServiceProvider, messenger, message, chatId, args, cancellationToken);
                return;

            case "/tagnotplayed":
            case "/tag":
                await HandleTagNotPlayedAsync(scope.ServiceProvider, messenger, chatId, cancellationToken);
                return;

            default:
                await SendTextAsync(messenger, chatId, "Неизвестная команда. Используйте /help", cancellationToken);
                return;
        }
    }

    private static async Task HandleSetupAsync(
        IServiceProvider services,
        IPlatformMessenger messenger,
        string chatId,
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            await SendTextAsync(messenger, chatId, "Использование: /setup #CLANTAG", cancellationToken);
            return;
        }

        var setupService = services.GetRequiredService<ClanSetupService>();
        var group = await setupService.SetupAsync(PlatformType.Telegram, chatId, args[0], cancellationToken);

        await SendTextAsync(
            messenger,
            chatId,
            $"Настройка завершена. Чат {chatId} привязан к клану {group.ClanTag}.",
            cancellationToken);
    }

    private static async Task HandleStatusAsync(
        IServiceProvider services,
        IPlatformMessenger messenger,
        string chatId,
        CancellationToken cancellationToken)
    {
        try
        {
            var statusService = services.GetRequiredService<ClanStatusService>();
            var status = await statusService.GetStatusAsync(PlatformType.Telegram, chatId, cancellationToken);
            var top = status.NotPlayed
                .OrderByDescending(x => x.BattlesRemaining)
                .Take(10)
                .Select(x => $"{x.PlayerName} ({x.PlayerTag}) осталось {x.BattlesRemaining}");

            var lines = new List<string>
            {
                $"Клан {status.ClanTag}",
                $"Война {status.WarKey}",
                $"Сыграли: {status.Played.Count}",
                $"Не сыграли: {status.NotPlayed.Count}"
            };

            if (status.NotPlayed.Count > 0)
            {
                lines.Add("Кто ещё не сыграл:");
                lines.AddRange(top.Select(x => $"- {x}"));
            }

            await SendTextAsync(messenger, chatId, string.Join('\n', lines), cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Не удалось получить статус: {ex.Message}", cancellationToken);
        }
    }

    private static async Task HandleLinkAsync(
        IServiceProvider services,
        IPlatformMessenger messenger,
        TelegramInboundMessage message,
        string chatId,
        string[] args,
        CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            await SendTextAsync(messenger, chatId, "Использование: /link #PLAYERTAG", cancellationToken);
            return;
        }

        if (message.From is null || message.From.Id <= 0)
        {
            await SendTextAsync(messenger, chatId, "Не удалось определить Telegram-пользователя для этого сообщения.", cancellationToken);
            return;
        }

        try
        {
            var playerLinkService = services.GetRequiredService<PlayerLinkService>();
            var link = await playerLinkService.LinkAsync(
                PlatformType.Telegram,
                chatId,
                message.From.Id.ToString(),
                BuildUserDisplayName(message.From),
                args[0],
                cancellationToken);

            await SendTextAsync(messenger, chatId, $"Пользователь {BuildUserDisplayName(message.From)} привязан к тегу {link.PlayerTag}.", cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Не удалось выполнить привязку: {ex.Message}", cancellationToken);
        }
    }

    private static async Task HandleTagNotPlayedAsync(
        IServiceProvider services,
        IPlatformMessenger messenger,
        string chatId,
        CancellationToken cancellationToken)
    {
        var groups = services.GetRequiredService<IGroupRepository>();
        var links = services.GetRequiredService<IPlayerLinkRepository>();
        var statusService = services.GetRequiredService<ClanStatusService>();
        var group = await groups.GetByPlatformGroupAsync(PlatformType.Telegram, chatId, cancellationToken);

        if (group is null)
        {
            await SendTextAsync(messenger, chatId, "Группа не настроена. Сначала используйте /setup #CLANTAG.", cancellationToken);
            return;
        }

        var dashboard = await statusService.GetDashboardByClanTagAsync(group.ClanTag, cancellationToken);
        var notPlayedByTag = dashboard.NotPlayed.ToDictionary(
            x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        if (notPlayedByTag.Count == 0)
        {
            await SendTextAsync(messenger, chatId, "Все игроки уже завершили бои войны кланов.", cancellationToken);
            return;
        }

        var groupLinks = await links.GetByGroupAsync(group.Id, cancellationToken);
        var linkedTargets = groupLinks
            .Where(x => notPlayedByTag.ContainsKey(TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag)))
            .ToList();

        var linkedTags = linkedTargets
            .Select(x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unlinkedTargets = dashboard.NotPlayed
            .Where(x => !linkedTags.Contains(TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag)))
            .ToList();

        var lines = new List<string>
        {
            $"Клан {group.ClanTag}: игроки с оставшимися боями ({notPlayedByTag.Count})"
        };

        if (linkedTargets.Count > 0)
        {
            lines.Add("Привязанные игроки:");
            foreach (var link in linkedTargets)
            {
                var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(link.PlayerTag);
                var status = notPlayedByTag[normalizedTag];
                var displayName = string.IsNullOrWhiteSpace(link.User.DisplayName) ? normalizedTag : link.User.DisplayName;
                lines.Add($"- {BuildTelegramMention(link.User.PlatformUserId, displayName)} ({normalizedTag}) осталось {status.BattlesRemaining}");
            }
        }

        if (unlinkedTargets.Count > 0)
        {
            lines.Add("Пока не привязаны:");
            foreach (var member in unlinkedTargets)
            {
                lines.Add($"- {WebUtility.HtmlEncode(member.PlayerName)} ({WebUtility.HtmlEncode(member.PlayerTag)}) осталось {member.BattlesRemaining}");
            }
        }

        await messenger.SendReminderAsync(
            new ReminderMessage(
                PlatformType.Telegram,
                chatId,
                string.Empty,
                string.Join('\n', lines),
                IsHtml: true),
            cancellationToken);
    }

    private static async Task SendTextAsync(
        IPlatformMessenger messenger,
        string chatId,
        string text,
        CancellationToken cancellationToken)
    {
        await messenger.SendReminderAsync(
            new ReminderMessage(PlatformType.Telegram, chatId, string.Empty, text),
            cancellationToken);
    }

    private static string BuildTelegramMention(string platformUserId, string displayName)
    {
        var safeName = WebUtility.HtmlEncode(displayName);
        return long.TryParse(platformUserId, out _)
            ? $"<a href=\"tg://user?id={platformUserId}\">{safeName}</a>"
            : safeName;
    }

    private static string BuildUserDisplayName(TelegramUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return $"@{user.Username}";
        }

        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)))
            .Trim();

        return string.IsNullOrWhiteSpace(fullName) ? user.Id.ToString() : fullName;
    }
}

public sealed class TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramInboundMessage? Message { get; set; }
}

public sealed class TelegramInboundMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("from")]
    public TelegramUser? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; set; } = new();
}

public sealed class TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
}

public sealed class TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
}
