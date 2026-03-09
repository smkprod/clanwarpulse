using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using ClanWarReminder.Api.Telegram;
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
            _logger.LogWarning("Telegram commands in API disabled because bot token is not configured.");
            return;
        }

        var http = _httpClientFactory.CreateClient();

        if (TryBuildWebhookUrl(botToken!, out var webhookUrl))
        {
            await EnsureWebhookReadyAsync(http, botToken!, webhookUrl, stoppingToken);
            _logger.LogInformation("Telegram webhook mode enabled: {WebhookUrl}", webhookUrl);
            return;
        }

        await EnsurePollingReadyAsync(http, botToken!, stoppingToken);

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
                    if (update.Message is null || string.IsNullOrWhiteSpace(update.Message.Text))
                    {
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var processor = scope.ServiceProvider.GetRequiredService<TelegramCommandProcessor>();
                    await processor.HandleMessageAsync(update.Message, stoppingToken);
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

    private bool TryBuildWebhookUrl(string token, out string webhookUrl)
    {
        var configured = _telegramOptions.WebhookUrl?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            webhookUrl = string.Empty;
            return false;
        }

        if (!configured.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !configured.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            configured = $"https://{configured.TrimStart('/')}";
        }

        var secret = string.IsNullOrWhiteSpace(_telegramOptions.WebhookSecret)
            ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..24].ToLowerInvariant()
            : _telegramOptions.WebhookSecret.Trim();

        webhookUrl = configured.Contains("/telegram/webhook/", StringComparison.OrdinalIgnoreCase)
            ? configured
            : $"{configured.TrimEnd('/')}/telegram/webhook/{secret}";

        return true;
    }

    private async Task EnsureWebhookReadyAsync(HttpClient http, string token, string webhookUrl, CancellationToken cancellationToken)
    {
        var payload = new
        {
            url = webhookUrl,
            allowed_updates = new[] { "message" },
            drop_pending_updates = false
        };

        using var response = await http.PostAsJsonAsync($"https://api.telegram.org/bot{token}/setWebhook", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task EnsurePollingReadyAsync(HttpClient http, string token, CancellationToken cancellationToken)
    {
        var webhookInfo = await http.GetFromJsonAsync<TelegramWebhookInfoResponse>(
            $"https://api.telegram.org/bot{token}/getWebhookInfo",
            cancellationToken: cancellationToken);

        if (webhookInfo?.Ok == true && !string.IsNullOrWhiteSpace(webhookInfo.Result?.Url))
        {
            _logger.LogWarning("Telegram webhook {WebhookUrl} is configured. Clearing it so long polling can work.", webhookInfo.Result.Url);
            await http.GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=false", cancellationToken);
        }

        var me = await http.GetFromJsonAsync<TelegramGetMeResponse>(
            $"https://api.telegram.org/bot{token}/getMe",
            cancellationToken: cancellationToken);

        if (me?.Ok == true && me.Result is not null)
        {
            _logger.LogInformation("Telegram polling is enabled for bot @{BotUsername} ({BotId}).", me.Result.Username, me.Result.Id);
        }
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
        public TelegramInboundMessage? Message { get; set; }
    }

    private sealed class TelegramGetMeResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public TelegramBotDto? Result { get; set; }
    }

    private sealed class TelegramBotDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    private sealed class TelegramWebhookInfoResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public TelegramWebhookInfoDto? Result { get; set; }
    }

    private sealed class TelegramWebhookInfoDto
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
