using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ClanWarReminder.Infrastructure.Integrations.Telegram;

public class TelegramInitDataValidator
{
    private readonly TelegramOptions _options;

    public TelegramInitDataValidator(IOptions<TelegramOptions> options)
    {
        _options = options.Value;
    }

    public bool TryValidate(string initData, out TelegramIdentity? identity, out string error)
    {
        identity = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            error = "Telegram bot token is not configured.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(initData))
        {
            error = "initData is required.";
            return false;
        }

        var parsed = ParseInitData(initData);
        if (!parsed.TryGetValue("hash", out var hashValue) || string.IsNullOrWhiteSpace(hashValue))
        {
            error = "initData hash is missing.";
            return false;
        }

        var checkPairs = parsed
            .Where(kv => kv.Key != "hash")
            .Select(kv => $"{kv.Key}={kv.Value}")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var dataCheckString = string.Join('\n', checkPairs);
        var expectedHash = ComputeHash(dataCheckString, _options.BotToken);
        var actualHash = hashValue.Trim().ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedHash),
                Encoding.UTF8.GetBytes(actualHash)))
        {
            error = "Invalid Telegram signature.";
            return false;
        }

        if (!parsed.TryGetValue("auth_date", out var authDateRaw) ||
            !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authUnix))
        {
            error = "initData auth_date is missing or invalid.";
            return false;
        }

        var authDateUtc = DateTimeOffset.FromUnixTimeSeconds(authUnix);
        var maxAge = TimeSpan.FromMinutes(Math.Clamp(_options.MaxAuthAgeMinutes, 1, 24 * 60));
        if (DateTimeOffset.UtcNow - authDateUtc > maxAge)
        {
            error = "Telegram auth data is expired.";
            return false;
        }

        if (!parsed.TryGetValue("user", out var userRaw) || string.IsNullOrWhiteSpace(userRaw))
        {
            error = "Telegram user payload is missing.";
            return false;
        }

        TelegramWebAppUser? user;
        try
        {
            user = JsonSerializer.Deserialize<TelegramWebAppUser>(userRaw);
        }
        catch (JsonException)
        {
            error = "Telegram user payload is invalid JSON.";
            return false;
        }

        if (user is null || user.Id <= 0)
        {
            error = "Telegram user payload is invalid.";
            return false;
        }

        var displayName = BuildDisplayName(user);
        identity = new TelegramIdentity(user.Id.ToString(CultureInfo.InvariantCulture), displayName, user.Username, authDateUtc);
        return true;
    }

    private static string ComputeHash(string dataCheckString, string botToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(botToken);
        var dataBytes = Encoding.UTF8.GetBytes(dataCheckString);

        using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = secretHmac.ComputeHash(tokenBytes);

        using var hashHmac = new HMACSHA256(secretKey);
        var hash = hashHmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildDisplayName(TelegramWebAppUser user)
    {
        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username;
        }

        return user.Id.ToString(CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> ParseInitData(string initData)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pairs = initData.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = WebUtility.UrlDecode(pair[..idx]);
            var value = WebUtility.UrlDecode(pair[(idx + 1)..]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = value ?? string.Empty;
        }

        return result;
    }

    private sealed class TelegramWebAppUser
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
}
