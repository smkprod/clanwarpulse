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
        services.Configure<ClashRoyaleOptions>(configuration.GetSection(ClashRoyaleOptions.SectionName));
        services.Configure<TelegramOptions>(configuration.GetSection(TelegramOptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));

        var connectionString = ResolveConnectionString(configuration);
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlayerLinkRepository, PlayerLinkRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();

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

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        // 1) Explicit env override on platforms like Render.
        var explicitEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(explicitEnv))
        {
            return NormalizeIfPostgresUri(explicitEnv);
        }

        // 2) Common managed DB url env var.
        var databaseUrl = configuration["DATABASE_URL"];
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

        if (query.TryGetValue("trust server certificate", out var trustCertRaw) &&
            bool.TryParse(trustCertRaw, out var trustCert))
        {
            builder.TrustServerCertificate = trustCert;
        }
        else
        {
            builder.TrustServerCertificate = true;
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
}
