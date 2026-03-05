using ClanWarReminder.Application;
using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Api.Auth;
using ClanWarReminder.Application.Services;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Enums;
using ClanWarReminder.Infrastructure;
using ClanWarReminder.Infrastructure.Integrations.Telegram;
using ClanWarReminder.Infrastructure.Persistence;
using System.Text.Json.Serialization;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AuthorizedPlayerRegistry>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();
await app.Services.ApplyMigrationsAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/commands/setup", async (
    SetupCommand request,
    ClanSetupService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PlatformGroupId) || string.IsNullOrWhiteSpace(request.ClanTag))
    {
        return Results.BadRequest(new { error = "platformGroupId and clanTag are required." });
    }

    var group = await service.SetupAsync(request.Platform, request.PlatformGroupId, request.ClanTag, cancellationToken);
    return Results.Ok(new
    {
        group.Id,
        group.Platform,
        group.PlatformGroupId,
        group.ClanTag,
        group.IsActive
    });
});

app.MapPost("/commands/setup/telegram", async (
    TelegramSetupCommand request,
    ClanSetupService service,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.ChatId) || string.IsNullOrWhiteSpace(request.ClanTag))
    {
        return Results.BadRequest(new { error = "chatId and clanTag are required." });
    }

    var group = await service.SetupAsync(PlatformType.Telegram, request.ChatId, request.ClanTag, cancellationToken);
    return Results.Ok(new
    {
        group.Id,
        group.Platform,
        group.PlatformGroupId,
        group.ClanTag,
        group.IsActive
    });
});

app.MapPost("/commands/link", async (
    LinkCommand request,
    PlayerLinkService service,
    CancellationToken cancellationToken) =>
{
    var link = await service.LinkAsync(
        request.Platform,
        request.PlatformGroupId,
        request.PlatformUserId,
        request.DisplayName,
        request.PlayerTag,
        cancellationToken);

    return Results.Ok(new
    {
        link.Id,
        link.GroupId,
        link.UserId,
        link.PlayerTag,
        link.LinkedAtUtc
    });
});

app.MapPost("/miniapp/auth/telegram", (
    TelegramAuthRequest request,
    TelegramInitDataValidator validator) =>
{
    if (!validator.TryValidate(request.InitData, out var identity, out var error))
    {
        return Results.BadRequest(new { error });
    }

    return Results.Ok(new TelegramAuthResponse(
        identity!.UserId,
        identity.DisplayName,
        identity.Username,
        identity.AuthDateUtc));
});

app.MapPost("/miniapp/auth/player", async (
    PlayerAuthRequest request,
    ClanStatusService statusService,
    IClashRoyaleClient clashRoyaleClient,
    TelegramInitDataValidator telegramValidator,
    PlayerLinkService playerLinkService,
    IGroupRepository groups,
    AuthorizedPlayerRegistry authorizedPlayers,
    TelegramBotProfileResolver botProfileResolver,
    CancellationToken cancellationToken) =>
{
    try
    {
        TelegramIdentity? telegramIdentity = null;
        if (!string.IsNullOrWhiteSpace(request.TelegramInitData))
        {
            if (!telegramValidator.TryValidate(request.TelegramInitData, out telegramIdentity, out var telegramError))
            {
                return Results.BadRequest(new { error = telegramError });
            }
        }

        var identity = await clashRoyaleClient.GetPlayerIdentityAsync(request.PlayerTag, cancellationToken);
        authorizedPlayers.MarkAuthorized(identity.PlayerTag);

        string? linkedTelegramGroupId = null;
        if (telegramIdentity is not null)
        {
            var normalizedClanTag = TagNormalizer.NormalizeClanOrPlayerTag(identity.ClanTag);
            var targetGroupId = !string.IsNullOrWhiteSpace(request.TelegramChatId)
                ? request.TelegramChatId.Trim()
                : (await groups.GetActiveByClanTagAsync(PlatformType.Telegram, normalizedClanTag, cancellationToken))?.PlatformGroupId;

            if (!string.IsNullOrWhiteSpace(targetGroupId))
            {
                await playerLinkService.LinkAsync(
                    PlatformType.Telegram,
                    targetGroupId,
                    telegramIdentity.UserId,
                    telegramIdentity.DisplayName,
                    identity.PlayerTag,
                    cancellationToken);

                linkedTelegramGroupId = targetGroupId;
            }
        }

        linkedTelegramGroupId ??= (await groups.GetActiveByClanTagAsync(
            PlatformType.Telegram,
            TagNormalizer.NormalizeClanOrPlayerTag(identity.ClanTag),
            cancellationToken))?.PlatformGroupId;

        var dashboard = await statusService.GetDashboardByClanTagAsync(identity.ClanTag, cancellationToken);
        var authorizedTags = authorizedPlayers.GetAuthorizedTagsForMembers(dashboard.Played.Concat(dashboard.NotPlayed).Select(x => x.PlayerTag));
        var botUsername = await botProfileResolver.GetBotUsernameAsync(cancellationToken);
        var botLink = BuildBotLink(botUsername, identity.PlayerTag);

        return Results.Ok(new PlayerAuthResponse(identity, dashboard, authorizedTags, botLink, linkedTelegramGroupId));
    }
    catch (Exception ex) when (TryMapClashRoyaleError(ex, out var message))
    {
        return Results.BadRequest(new { error = message });
    }
});

app.MapGet("/miniapp/player/dashboard", async (
    string playerTag,
    ClanStatusService statusService,
    IClashRoyaleClient clashRoyaleClient,
    IGroupRepository groups,
    AuthorizedPlayerRegistry authorizedPlayers,
    TelegramBotProfileResolver botProfileResolver,
    CancellationToken cancellationToken) =>
{
    try
    {
        var identity = await clashRoyaleClient.GetPlayerIdentityAsync(playerTag, cancellationToken);
        authorizedPlayers.MarkAuthorized(identity.PlayerTag);
        var dashboard = await statusService.GetDashboardByClanTagAsync(identity.ClanTag, cancellationToken);
        var authorizedTags = authorizedPlayers.GetAuthorizedTagsForMembers(dashboard.Played.Concat(dashboard.NotPlayed).Select(x => x.PlayerTag));
        var botUsername = await botProfileResolver.GetBotUsernameAsync(cancellationToken);
        var botLink = BuildBotLink(botUsername, identity.PlayerTag);
        var linkedTelegramGroupId = (await groups.GetActiveByClanTagAsync(
            PlatformType.Telegram,
            TagNormalizer.NormalizeClanOrPlayerTag(identity.ClanTag),
            cancellationToken))?.PlatformGroupId;

        return Results.Ok(new PlayerAuthResponse(identity, dashboard, authorizedTags, botLink, linkedTelegramGroupId));
    }
    catch (Exception ex) when (TryMapClashRoyaleError(ex, out var message))
    {
        return Results.BadRequest(new { error = message });
    }
});

app.MapGet("/miniapp/clan/details", async (
    string clanTag,
    IClashRoyaleClient clashRoyaleClient,
    CancellationToken cancellationToken) =>
{
    try
    {
        var details = await clashRoyaleClient.GetClanDetailsAsync(clanTag, cancellationToken);
        return Results.Ok(details);
    }
    catch (Exception ex) when (TryMapClashRoyaleError(ex, out var message))
    {
        return Results.BadRequest(new { error = message });
    }
});

app.MapGet("/miniapp/telegram/sync", async (
    string playerTag,
    ClanStatusService statusService,
    IClashRoyaleClient clashRoyaleClient,
    IGroupRepository groups,
    IPlayerLinkRepository links,
    AuthorizedPlayerRegistry authorizedPlayers,
    CancellationToken cancellationToken) =>
{
    try
    {
        var identity = await clashRoyaleClient.GetPlayerIdentityAsync(playerTag, cancellationToken);
        var normalizedClanTag = TagNormalizer.NormalizeClanOrPlayerTag(identity.ClanTag);
        var dashboard = await statusService.GetDashboardByClanTagAsync(identity.ClanTag, cancellationToken);
        var members = dashboard.Played.Concat(dashboard.NotPlayed).ToList();
        var memberTagSet = members
            .Select(x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var authorizedSet = authorizedPlayers
            .GetAuthorizedTagsForMembers(members.Select(x => x.PlayerTag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var group = await groups.GetActiveByClanTagAsync(PlatformType.Telegram, normalizedClanTag, cancellationToken);
        if (group is null)
        {
            var noGroupMembers = members
                .Select(x => new TelegramSyncMember(
                    x.PlayerTag,
                    x.PlayerName,
                    x.HasPlayed,
                    x.BattlesPlayed,
                    x.BattlesRemaining,
                    false,
                    null,
                    null,
                    authorizedSet.Contains(TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag))))
                .OrderByDescending(x => x.BattlesRemaining)
                .ThenBy(x => x.PlayerName)
                .ToList();

            return Results.Ok(new TelegramSyncResponse(
                identity.ClanTag,
                null,
                noGroupMembers,
                Array.Empty<TelegramSyncLinkedUser>()));
        }

        var groupLinks = await links.GetByGroupAsync(group.Id, cancellationToken);
        var linkByTag = groupLinks
            .GroupBy(x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var syncMembers = members
            .Select(x =>
            {
                var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag);
                var hasLink = linkByTag.TryGetValue(normalizedTag, out var link);

                return new TelegramSyncMember(
                    x.PlayerTag,
                    x.PlayerName,
                    x.HasPlayed,
                    x.BattlesPlayed,
                    x.BattlesRemaining,
                    hasLink,
                    link?.User.PlatformUserId,
                    link?.User.DisplayName,
                    authorizedSet.Contains(normalizedTag));
            })
            .OrderByDescending(x => x.BattlesRemaining)
            .ThenBy(x => x.PlayerName)
            .ToList();

        var linkedUsers = groupLinks
            .Select(x =>
            {
                var normalizedTag = TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag);
                return new TelegramSyncLinkedUser(
                    x.User.PlatformUserId,
                    x.User.DisplayName,
                    x.PlayerTag,
                    memberTagSet.Contains(normalizedTag),
                    x.LinkedAtUtc);
            })
            .OrderByDescending(x => x.LinkedAtUtc)
            .ToList();

        return Results.Ok(new TelegramSyncResponse(
            identity.ClanTag,
            group.PlatformGroupId,
            syncMembers,
            linkedUsers));
    }
    catch (Exception ex) when (TryMapClashRoyaleError(ex, out var message))
    {
        return Results.BadRequest(new { error = message });
    }
});

app.MapPost("/miniapp/telegram/relink", async (
    TelegramRelinkCommand request,
    PlayerLinkService playerLinkService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PlatformGroupId) ||
        string.IsNullOrWhiteSpace(request.PlatformUserId) ||
        string.IsNullOrWhiteSpace(request.DisplayName) ||
        string.IsNullOrWhiteSpace(request.PlayerTag))
    {
        return Results.BadRequest(new { error = "platformGroupId, platformUserId, displayName and playerTag are required." });
    }

    var link = await playerLinkService.LinkAsync(
        PlatformType.Telegram,
        request.PlatformGroupId,
        request.PlatformUserId,
        request.DisplayName,
        request.PlayerTag,
        cancellationToken);

    return Results.Ok(new
    {
        link.Id,
        link.PlayerTag,
        link.LinkedAtUtc
    });
});

app.MapGet("/commands/status", async (
    PlatformType platform,
    string platformGroupId,
    ClanStatusService service,
    CancellationToken cancellationToken) =>
{
    var status = await service.GetStatusAsync(platform, platformGroupId, cancellationToken);
    return Results.Ok(status);
});

app.MapPost("/commands/notify/not-played", async (
    NotifyNotPlayedCommand request,
    IGroupRepository groups,
    IPlayerLinkRepository links,
    ClanStatusService statusService,
    IPlatformMessenger messenger,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.PlatformGroupId))
    {
        return Results.BadRequest(new { error = "platformGroupId is required." });
    }

    var group = await groups.GetByPlatformGroupAsync(request.Platform, request.PlatformGroupId, cancellationToken);
    if (group is null)
    {
        return Results.BadRequest(new { error = "Group is not configured. Run /setup first." });
    }

    var dashboard = await statusService.GetDashboardByClanTagAsync(group.ClanTag, cancellationToken);
    var notPlayedByTag = dashboard.NotPlayed.ToDictionary(
        x => TagNormalizer.NormalizeClanOrPlayerTag(x.PlayerTag),
        x => x,
        StringComparer.OrdinalIgnoreCase);

    if (notPlayedByTag.Count == 0)
    {
        return Results.Ok(new { sent = false, text = "All players have completed clan war battles." });
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

    var text = string.Join('\n', lines);

    await messenger.SendReminderAsync(
        new ReminderMessage(
            group.Platform,
            group.PlatformGroupId,
            string.Empty,
            text,
            IsHtml: true),
        cancellationToken);

    return Results.Ok(new
    {
        sent = true,
        linkedTargets = linkedTargets.Count,
        unlinkedTargets = unlinkedTargets.Count,
        totalNotPlayed = notPlayedByTag.Count
    });
});

app.MapHealthChecks("/health");
app.MapGet("/miniapp", () => Results.Redirect("/miniapp/index.html"));

app.Run();

static string? BuildBotLink(string? botUsername, string playerTag)
{
    if (string.IsNullOrWhiteSpace(botUsername))
    {
        return null;
    }

    var normalizedTag = playerTag.Trim().TrimStart('#').ToUpperInvariant();
    return $"https://t.me/{botUsername}?start=player_{normalizedTag}";
}

static bool TryMapClashRoyaleError(Exception ex, out string message)
{
    if (ex is InvalidOperationException invalidOperationException)
    {
        message = invalidOperationException.Message;
        return true;
    }

    if (ex is HttpRequestException httpRequestException)
    {
        message = httpRequestException.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => "Player or clan tag was not found in Clash Royale API.",
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "Clash Royale API token is invalid or missing.",
            _ => "Failed to load data from Clash Royale API."
        };
        return true;
    }

    message = string.Empty;
    return false;
}

static string BuildTelegramMention(string platformUserId, string displayName)
{
    var safeName = WebUtility.HtmlEncode(displayName);

    if (long.TryParse(platformUserId, out _))
    {
        return $"<a href=\"tg://user?id={platformUserId}\">{safeName}</a>";
    }

    return safeName;
}

public sealed record SetupCommand(PlatformType Platform, string PlatformGroupId, string ClanTag);
public sealed record TelegramSetupCommand(string ChatId, string ClanTag);
public sealed record LinkCommand(PlatformType Platform, string PlatformGroupId, string PlatformUserId, string DisplayName, string PlayerTag);
public sealed record TelegramAuthRequest(string InitData);
public sealed record TelegramAuthResponse(string UserId, string DisplayName, string? Username, DateTimeOffset AuthDateUtc);
public sealed record PlayerAuthRequest(string PlayerTag, string? TelegramInitData = null, string? TelegramChatId = null);
public sealed record PlayerAuthResponse(
    PlayerIdentityResult Identity,
    ClanWarDashboard Dashboard,
    IReadOnlyList<string> AuthorizedPlayerTags,
    string? BotLink,
    string? LinkedTelegramGroupId);
public sealed record TelegramRelinkCommand(string PlatformGroupId, string PlatformUserId, string DisplayName, string PlayerTag);
public sealed record TelegramSyncMember(
    string PlayerTag,
    string PlayerName,
    bool HasPlayed,
    int BattlesPlayed,
    int BattlesRemaining,
    bool IsLinked,
    string? PlatformUserId,
    string? TelegramDisplayName,
    bool IsAuthorized);
public sealed record TelegramSyncLinkedUser(
    string PlatformUserId,
    string DisplayName,
    string PlayerTag,
    bool InCurrentClan,
    DateTimeOffset LinkedAtUtc);
public sealed record TelegramSyncResponse(
    string ClanTag,
    string? PlatformGroupId,
    IReadOnlyList<TelegramSyncMember> Members,
    IReadOnlyList<TelegramSyncLinkedUser> LinkedUsers);
public sealed record NotifyNotPlayedCommand(PlatformType Platform, string PlatformGroupId);
