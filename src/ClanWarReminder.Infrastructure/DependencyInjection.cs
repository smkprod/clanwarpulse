using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Infrastructure.Integrations.ClashRoyale;
using ClanWarReminder.Infrastructure.Integrations.Messaging;
using ClanWarReminder.Infrastructure.Integrations.Telegram;
using ClanWarReminder.Infrastructure.Options;
using ClanWarReminder.Infrastructure.Persistence;
using ClanWarReminder.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClanWarReminder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dotEnv = LoadDotEnv(FindDotEnvPath());

        services.Configure<ClashRoyaleOptions>(configuration.GetSection(ClashRoyaleOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
        services.PostConfigure<ClashRoyaleOptions>(options =>
        {
            options.BaseUrl = ResolveString(
                options.BaseUrl,
                configuration["ClashRoyale:BaseUrl"],
                configuration["CLASH_ROYALE_BASE_URL"],
                GetDotEnv(dotEnv, "CLASH_ROYALE_BASE_URL"),
                "https://api.clashroyale.com/v1");
            options.ApiToken = ResolveString(
                options.ApiToken,
                configuration["ClashRoyale:ApiToken"],
                configuration["CLASH_ROYALE_API_TOKEN"],
                GetDotEnv(dotEnv, "CLASH_ROYALE_API_TOKEN"));
        });
        services.PostConfigure<TelegramOptions>(options =>
        {
            options.BotToken = ResolveString(
                options.BotToken,
                configuration["Telegram:BotToken"],
                configuration["TELEGRAM_BOT_TOKEN"],
                GetDotEnv(dotEnv, "TELEGRAM_BOT_TOKEN"));
            options.BotUsername = ResolveString(
                options.BotUsername,
                configuration["Telegram:BotUsername"],
                configuration["TELEGRAM_BOT_USERNAME"],
                GetDotEnv(dotEnv, "TELEGRAM_BOT_USERNAME"));
            options.WebhookUrl = ResolveString(
                options.WebhookUrl,
                configuration["Telegram:WebhookUrl"],
                configuration["TELEGRAM_WEBHOOK_URL"],
                configuration["RENDER_EXTERNAL_URL"],
                GetDotEnv(dotEnv, "TELEGRAM_WEBHOOK_URL"),
                GetDotEnv(dotEnv, "RENDER_EXTERNAL_URL"));
            options.WebhookSecret = ResolveString(
                options.WebhookSecret,
                configuration["Telegram:WebhookSecret"],
                configuration["TELEGRAM_WEBHOOK_SECRET"],
                GetDotEnv(dotEnv, "TELEGRAM_WEBHOOK_SECRET"));
        });

        var connectionString = ResolveConnectionString(configuration, dotEnv);
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerLinkRepository, PlayerLinkRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IClanWarHistoryRepository, ClanWarHistoryRepository>();

        services.AddHttpClient<ClashRoyaleClient>();
        services.AddHttpClient<TelegramBotProfileResolver>();
        services.AddHttpClient<TelegramMessenger>();
        services.AddScoped<IClashRoyaleClient, ClashRoyaleClient>();
        services.AddSingleton<TelegramInitDataValidator>();

        services.AddScoped<DiscordMessenger>();
        services.AddScoped<IPlatformMessenger>(sp =>
        {
            var messengers = new IPlatformMessenger[]
            {
                sp.GetRequiredService<TelegramMessenger>(),
                sp.GetRequiredService<DiscordMessenger>()
            };

            return new CompositePlatformMessenger(messengers);
        });

        return services;
    }

    private static string ResolveString(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) &&
                !value.Contains("<YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string ResolveConnectionString(IConfiguration configuration, IReadOnlyDictionary<string, string> dotEnv)
    {
        // 1) Explicit env override on platforms like Render.
        var explicitEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(explicitEnv))
        {
            return NormalizeIfPostgresUri(explicitEnv);
        }

        var dotEnvConnection = GetDotEnv(dotEnv, "ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(dotEnvConnection))
        {
            return NormalizeIfPostgresUri(dotEnvConnection);
        }

        // 2) Common managed DB url env var.
        var databaseUrl = configuration["DATABASE_URL"] ?? GetDotEnv(dotEnv, "DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return BuildNpgsqlConnectionString(databaseUrl);
        }

        // 3) Fallback to app configuration (local/dev defaults).
        var direct = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return NormalizeIfPostgresUri(direct);
        }

        throw new InvalidOperationException(
            "Database connection is not configured. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");
    }

    private static string NormalizeIfPostgresUri(string value)
    {
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return BuildNpgsqlConnectionString(value);
        }

        return value;
    }

    private static string BuildNpgsqlConnectionString(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
        {
            return databaseUrl;
        }

        if (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return databaseUrl;
        }

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = username,
            Password = password,
            Database = database
        };

        var query = ParseQuery(uri.Query);
        if (query.TryGetValue("sslmode", out var sslMode) &&
            Enum.TryParse<SslMode>(sslMode, true, out var parsedSslMode))
        {
            builder.SslMode = parsedSslMode;
        }
        else
        {
            builder.SslMode = SslMode.Require;
        }

        return builder.ConnectionString;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var parts = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(part[..idx]).Trim();
            var value = Uri.UnescapeDataString(part[(idx + 1)..]).Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static Dictionary<string, string> LoadDotEnv(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return values;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var delimiterIndex = line.IndexOf('=');
            if (delimiterIndex <= 0)
            {
                continue;
            }

            var key = line[..delimiterIndex].Trim();
            var value = line[(delimiterIndex + 1)..].Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(key))
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static string? GetDotEnv(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    private static string FindDotEnvPath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var depth = 0; depth < 4 && current is not null; depth++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }
}
