using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClanWarReminder.Infrastructure.Integrations.Telegram;

public sealed class TelegramBotProfileResolver
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotProfileResolver> _logger;
    private string? _cachedUsername;

    public TelegramBotProfileResolver(
        HttpClient httpClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramBotProfileResolver> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetBotUsernameAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.BotUsername))
        {
            return _options.BotUsername.Trim().TrimStart('@');
        }

        if (!string.IsNullOrWhiteSpace(_cachedUsername))
        {
            return _cachedUsername;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return null;
        }

        var url = $"https://api.telegram.org/bot{_options.BotToken}/getMe";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<GetMeResponse>(cancellationToken: cancellationToken);
            var username = payload?.Result?.Username?.Trim().TrimStart('@');

            if (!string.IsNullOrWhiteSpace(username))
            {
                _cachedUsername = username;
            }

            return _cachedUsername;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve Telegram bot username via getMe.");
            return null;
        }
    }

    private sealed class GetMeResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public BotResult? Result { get; set; }
    }

    private sealed class BotResult
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}
