using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Application.Services;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Enums;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ClanWarReminder.Api.Background;

public sealed class ApiTelegramCommandHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramOptions _telegramOptions;
    private readonly ILogger<ApiTelegramCommandHostedService> _logger;
    private long _offset;

    public ApiTelegramCommandHostedService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramOptions> telegramOptions,
        ILogger<ApiTelegramCommandHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _telegramOptions = telegramOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var botToken = _telegramOptions.BotToken?.Trim();
        if (!HasUsableBotToken(botToken))
        {
            _logger.LogWarning(
                "Telegram command polling in API disabled because Telegram bot token is not configured or still has placeholder value.");
            return;
        }

        var http = _httpClientFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = BuildGetUpdatesUrl(botToken!, _offset);
                var response = await http.GetFromJsonAsync<GetUpdatesResponse>(url, cancellationToken: stoppingToken);

                if (response?.Ok != true || response.Result is null || response.Result.Count == 0)
                {
                    continue;
                }

                foreach (var update in response.Result)
                {
                    _offset = Math.Max(_offset, update.UpdateId + 1);
                    var message = update.Message;
                    if (message is null || string.IsNullOrWhiteSpace(message.Text))
                    {
                        continue;
                    }

                    await HandleMessageAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram command polling in API failed.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(TelegramMessageDto message, CancellationToken cancellationToken)
    {
        var text = message.Text!.Trim();
        if (!text.StartsWith('/'))
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
                    "Commands:\n/setup #CLANTAG\n/link #PLAYERTAG\n/status\n/tagnotplayed\n/help",
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
                await SendTextAsync(messenger, chatId, "Unknown command. Use /help", cancellationToken);
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
            await SendTextAsync(messenger, chatId, "Usage: /setup #CLANTAG", cancellationToken);
            return;
        }

        var setupService = services.GetRequiredService<ClanSetupService>();
        var group = await setupService.SetupAsync(PlatformType.Telegram, chatId, args[0], cancellationToken);

        await SendTextAsync(
            messenger,
            chatId,
            $"Setup complete. Chat {chatId} linked to clan {group.ClanTag}.",
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
                .Take(5)
                .Select(x => $"{x.PlayerName} ({x.PlayerTag}) left {x.BattlesRemaining}");

            var lines = new List<string>
            {
                $"Clan {status.ClanTag}",
                $"War {status.WarKey}",
                $"Played: {status.Played.Count}",
                $"Not played: {status.NotPlayed.Count}"
            };

            if (status.NotPlayed.Count > 0)
            {
                lines.Add("Top pending:");
                lines.AddRange(top.Select(x => $"- {x}"));
            }

            await SendTextAsync(messenger, chatId, string.Join('\n', lines), cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Status failed: {ex.Message}", cancellationToken);
        }
    }

    private static async Task HandleLinkAsync(
        IServiceProvider services,
        IPlatformMessenger messenger,
        TelegramMessageDto message,
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
            await SendTextAsync(messenger, chatId, "Cannot read Telegram user identity for this message.", cancellationToken);
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

            await SendTextAsync(messenger, chatId, $"Linked {BuildUserDisplayName(message.From)} to {link.PlayerTag}.", cancellationToken);
        }
        catch (Exception ex)
        {
            await SendTextAsync(messenger, chatId, $"Link failed: {ex.Message}", cancellationToken);
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
            await SendTextAsync(messenger, chatId, "Group is not configured. Use /setup #CLANTAG first.", cancellationToken);
            return;
        }

        var dashboard = await statusService.GetDashboardByClanTagAsync(group.ClanTag, cancellationToken);
        var notPlayedByTag = dashboard.NotPlayed.ToDictionary(
            x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag),
            x => x,
            StringComparer.OrdinalIgnoreCase);

        if (notPlayedByTag.Count == 0)
        {
            await SendTextAsync(messenger, chatId, "All players have completed clan war battles.", cancellationToken);
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
            $"Clan {group.ClanTag}: players with battles remaining ({notPlayedByTag.Count})"
        };

        if (linkedTargets.Count > 0)
        {
            lines.Add("Linked players:");
            foreach (var link in linkedTargets)
            {
                var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(link.PlayerTag);
                var status = notPlayedByTag[normalizedTag];
                var displayName = string.IsNullOrWhiteSpace(link.User.DisplayName) ? normalizedTag : link.User.DisplayName;
                lines.Add($"- {BuildTelegramMention(link.User.PlatformUserId, displayName)} ({normalizedTag}) left {status.BattlesRemaining}");
            }
        }

        if (unlinkedTargets.Count > 0)
        {
            lines.Add("Not linked in mini app yet:");
            foreach (var member in unlinkedTargets)
            {
                lines.Add($"- {WebUtility.HtmlEncode(member.PlayerName)} ({WebUtility.HtmlEncode(member.PlayerTag)}) left {member.BattlesRemaining}");
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

    private static string BuildGetUpdatesUrl(string token, long offset)
    {
        var offsetPart = offset > 0 ? $"&offset={offset}" : string.Empty;
        return $"https://api.telegram.org/bot{token}/getUpdates?timeout=25{offsetPart}";
    }

    private static bool HasUsableBotToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (token.Contains("YOUR_TELEGRAM_BOT_TOKEN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (token.StartsWith('<') && token.EndsWith('>'))
        {
            return false;
        }

        return true;
    }

    private static string BuildUserDisplayName(TelegramUserDto user)
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

    private sealed class GetUpdatesResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public List<TelegramUpdateDto> Result { get; set; } = new();
    }

    private sealed class TelegramUpdateDto
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessageDto? Message { get; set; }
    }

    private sealed class TelegramMessageDto
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("from")]
        public TelegramUserDto? From { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChatDto Chat { get; set; } = new();
    }

    private sealed class TelegramUserDto
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

    private sealed class TelegramChatDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}
