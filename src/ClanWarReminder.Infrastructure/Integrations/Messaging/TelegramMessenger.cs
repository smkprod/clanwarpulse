using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ClanWarReminder.Infrastructure.Integrations.Messaging;

public class TelegramMessenger : IPlatformMessenger
{
    private readonly HttpClient _httpClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramMessenger> _logger;

    public TelegramMessenger(
        HttpClient httpClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramMessenger> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendReminderAsync(ReminderMessage message, CancellationToken cancellationToken)
    {
        if (message.Platform != Domain.Enums.PlatformType.Telegram)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Telegram bot token is not configured. Message skipped for group {GroupId}.", message.PlatformGroupId);
            return;
        }

        var chatId = message.PlatformGroupId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(chatId))
        {
            _logger.LogWarning("Telegram chat id is empty. Message skipped.");
            return;
        }

        var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";

        var payload = new TelegramSendMessageRequest(chatId, message.Text, message.IsHtml ? "HTML" : null);
        using var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            // Fallback: if HTML parse failed, retry as plain text.
            if (message.IsHtml && (int)response.StatusCode == 400)
            {
                _logger.LogWarning("Telegram HTML send failed for group {GroupId}, retrying plain text. Body: {Body}", chatId, body);
                var fallbackPayload = new TelegramSendMessageRequest(chatId, StripHtml(message.Text), null);
                using var fallbackResponse = await _httpClient.PostAsJsonAsync(url, fallbackPayload, cancellationToken);
                if (!fallbackResponse.IsSuccessStatusCode)
                {
                    var fallbackBody = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Telegram fallback send failed ({StatusCode}) for group {GroupId}: {Body}", (int)fallbackResponse.StatusCode, chatId, fallbackBody);
                    return;
                }

                _logger.LogInformation("Telegram message sent via fallback for group {GroupId}.", chatId);
                return;
            }

            _logger.LogWarning("Telegram sendMessage failed ({StatusCode}) for group {GroupId}: {Body}", (int)response.StatusCode, chatId, body);
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<TelegramSendMessageResponse>(cancellationToken: cancellationToken);
        if (result?.Ok != true)
        {
            _logger.LogWarning("Telegram sendMessage returned not ok for group {GroupId}.", chatId);
            return;
        }

        _logger.LogInformation("Telegram message sent to group {GroupId}.", chatId);
    }

    private static string StripHtml(string text)
    {
        var plain = text
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);

        return System.Text.RegularExpressions.Regex.Replace(plain, "<.*?>", string.Empty);
    }

    private sealed record TelegramSendMessageRequest(
        [property: JsonPropertyName("chat_id")] string ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("parse_mode")] string? ParseMode);

    private sealed class TelegramSendMessageResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }
    }
}
