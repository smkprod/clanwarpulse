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
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var chatId = message.Chat.Id.ToString();
        using var scope = _scopeFactory.CreateScope();
        var messenger = scope.ServiceProvider.GetRequiredService<IPlatformMessenger>();

        if (!text.StartsWith('/'))
        {
            await SendTextAsync(messenger, chatId, "Use /start or /help to see available commands.", cancellationToken);
            return;
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].Split('@', 2)[0].ToLowerInvariant();
        var args = parts.Skip(1).ToArray();

        switch (command)
        {
            case "/start":
                await HandleStartAsync(messenger, message, chatId, args, cancellationToken);
                return;

            case "/help":
                await SendTextAsync(messenger, chatId, BuildHelpText(), cancellationToken);
                return;

            case "/ping":
                await SendTextAsync(messenger, chatId, "Bot is online.", cancellationToken);
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
                await SendTextAsync(messenger, chatId, "Unknown command. Use /help.", cancellationToken);
                return;
        }
    }

    private static async Task HandleStartAsync(
        IPlatformMessenger messenger,
        TelegramInboundMessage message,
        string chatId,
        string[] args,
        CancellationToken cancellationToken)
    {
        var lines = new List<string>();

        if (message.From is not null)
        {
            lines.Add($"Hello, {BuildUserDisplayName(message.From)}.");
        }

        lines.Add("ClanWarReminder bot is ready.");

        if (args.Length > 0)
        {
            var payload = args[0].Trim();

            if (payload.StartsWith("player_", StringComparison.OrdinalIgnoreCase))
            {
                var playerTag = NormalizeTag(payload["player_".Length..]);
                lines.Add($"Deep link received for player {playerTag}.");
                lines.Add($"Use /link {playerTag} to link this Telegram account to the player.");
            }
            else if (payload.StartsWith("clan_", StringComparison.OrdinalIgnoreCase))
            {
                var clanTag = NormalizeTag(payload["clan_".Length..]);
                lines.Add($"Deep link received for clan {clanTag}.");
                lines.Add($"Use /setup {clanTag} in the target group to bind the clan.");
            }
        }

        lines.Add(string.Empty);
        lines.Add(BuildHelpText());

        await SendTextAsync(messenger, chatId, string.Join('\n', lines), cancellationToken);
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
            await SendTextAsync(messenger, chatId, "Usage: /setup #CLANTAG", cancellationToken);
            return;
        }

        var setupService = services.GetRequiredService<ClanSetupService>();
        var group = await setupService.SetupAsync(PlatformType.Telegram, chatId, args[0], cancellationToken);

        await SendTextAsync(
            messenger,
            chatId,
            $"Setup completed. Chat {chatId} is linked to clan {group.ClanTag}.",
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
                .Select(x => $"{x.PlayerName} ({x.PlayerTag}) remaining {x.BattlesRemaining}");

            var lines = new List<string>
            {
                $"Clan {status.ClanTag}",
                $"War {status.WarKey}",
                $"Played: {status.Played.Count}",
                $"Not played: {status.NotPlayed.Count}"
            };

            if (status.NotPlayed.Count > 0)
            {
                lines.Add("Players who still have battles left:");
                lines.AddRange(top.Select(x => $"- {x}"));
            }

            await SendTextAsync(messenger, chatId, string.Join('\n', lines), cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Failed to load status: {ex.Message}", cancellationToken);
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
            await SendTextAsync(messenger, chatId, "Usage: /link #PLAYERTAG", cancellationToken);
            return;
        }

        if (message.From is null || message.From.Id <= 0)
        {
            await SendTextAsync(messenger, chatId, "Failed to determine the Telegram user for this message.", cancellationToken);
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

            await SendTextAsync(
                messenger,
                chatId,
                $"User {BuildUserDisplayName(message.From)} is linked to {link.PlayerTag}.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Failed to link player: {ex.Message}", cancellationToken);
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
            await SendTextAsync(messenger, chatId, "This chat is not configured yet. Use /setup #CLANTAG first.", cancellationToken);
            return;
        }

        var dashboard = await statusService.GetDashboardByClanTagAsync(group.ClanTag, cancellationToken);
        var notPlayedByTag = dashboard.NotPlayed.ToDictionary(
            x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        if (notPlayedByTag.Count == 0)
        {
            await SendTextAsync(messenger, chatId, "All clan members have already finished their clan war battles.", cancellationToken);
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
            $"Clan {group.ClanTag}: players with remaining battles ({notPlayedByTag.Count})"
        };

        if (linkedTargets.Count > 0)
        {
            lines.Add("Linked players:");
            foreach (var link in linkedTargets)
            {
                var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(link.PlayerTag);
                var status = notPlayedByTag[normalizedTag];
                var displayName = string.IsNullOrWhiteSpace(link.User.DisplayName) ? normalizedTag : link.User.DisplayName;
                lines.Add($"- {BuildTelegramMention(link.User.PlatformUserId, displayName)} ({normalizedTag}) remaining {status.BattlesRemaining}");
            }
        }

        if (unlinkedTargets.Count > 0)
        {
            lines.Add("Not linked yet:");
            foreach (var member in unlinkedTargets)
            {
                lines.Add($"- {WebUtility.HtmlEncode(member.PlayerName)} ({WebUtility.HtmlEncode(member.PlayerTag)}) remaining {member.BattlesRemaining}");
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

    private static string BuildHelpText()
    {
        return string.Join('\n', new[]
        {
            "Available commands:",
            "/start - start the bot",
            "/help - show this help",
            "/ping - bot health check",
            "/setup #CLANTAG - bind this chat to a clan",
            "/link #PLAYERTAG - bind your Telegram user to a player",
            "/status - current clan war status",
            "/tagnotplayed - mention players who still have battles left"
        });
    }

    private static string NormalizeTag(string value)
    {
        var normalized = value.Trim().TrimStart('#').ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? value : $"#{normalized}";
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
