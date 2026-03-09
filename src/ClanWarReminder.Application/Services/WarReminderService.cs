using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ClanWarReminder.Application.Services;

public class WarReminderService
{
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> LastKnownClanMembers = new();
    private static readonly ConcurrentDictionary<Guid, Dictionary<string, int>> LastKnownBattles = new();

    private readonly IGroupRepository _groups;
    private readonly IPlayerLinkRepository _links;
    private readonly IReminderRepository _reminders;
    private readonly IClashRoyaleClient _clashRoyale;
    private readonly IPlatformMessenger _messenger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WarReminderService> _logger;
    private readonly ClanWarHistoryService _historyService;

    public WarReminderService(
        IGroupRepository groups,
        IPlayerLinkRepository links,
        IReminderRepository reminders,
        IClashRoyaleClient clashRoyale,
        IPlatformMessenger messenger,
        IUnitOfWork unitOfWork,
        ILogger<WarReminderService> logger,
        ClanWarHistoryService historyService)
    {
        _groups = groups;
        _links = links;
        _reminders = reminders;
        _clashRoyale = clashRoyale;
        _messenger = messenger;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _historyService = historyService;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var sentCount = 0;
        var groups = await _groups.GetActiveAsync(cancellationToken);

        foreach (var group in groups)
        {
            var snapshot = await _clashRoyale.GetCurrentWarAsync(group.ClanTag, cancellationToken);
            await _historyService.CaptureCurrentWeekAsync(group.ClanTag, group.ClanTag, snapshot, cancellationToken);
            await LogClanMembershipChangesAsync(group, snapshot, cancellationToken);
            await LogBattleProgressChangesAsync(group, snapshot, cancellationToken);

            var inactiveTags = snapshot.Members
                .Where(m => !m.HasPlayed)
                .Select(m => m.PlayerTag)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var links = await _links.GetByGroupAsync(group.Id, cancellationToken);

            foreach (var link in links.Where(l => inactiveTags.Contains(l.PlayerTag)))
            {
                if (await _reminders.ExistsAsync(link.UserId, group.Id, snapshot.WarKey, cancellationToken))
                {
                    continue;
                }

                await _messenger.SendReminderAsync(
                    new ReminderMessage(
                        group.Platform,
                        group.PlatformGroupId,
                        link.User.PlatformUserId,
                        $"Reminder: complete your clan war battles for {group.ClanTag}."),
                    cancellationToken);

                await _reminders.AddAsync(new Reminder
                {
                    UserId = link.UserId,
                    GroupId = group.Id,
                    WarKey = snapshot.WarKey,
                    SentAtUtc = DateTimeOffset.UtcNow
                }, cancellationToken);

                sentCount++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return sentCount;
    }

    private async Task LogClanMembershipChangesAsync(Group group, ClanWarSnapshot snapshot, CancellationToken cancellationToken)
    {
        var groupId = group.Id;
        var clanTag = group.ClanTag;
        var current = snapshot.Members
            .Select(m => NormalizeTag(m.PlayerTag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!LastKnownClanMembers.TryGetValue(groupId, out var previous))
        {
            LastKnownClanMembers[groupId] = current;
            return;
        }

        var joined = current.Except(previous).ToList();
        var left = previous.Except(current).ToList();

        if (joined.Count == 0 && left.Count == 0)
        {
            return;
        }

        var byTag = snapshot.Members.ToDictionary(m => NormalizeTag(m.PlayerTag), m => m.PlayerName, StringComparer.OrdinalIgnoreCase);
        var joinedNames = joined.Select(tag => byTag.TryGetValue(tag, out var name) ? $"{name} ({tag})" : tag).ToList();
        var leftNames = left.ToList();

        if (joinedNames.Count > 0)
        {
            _logger.LogInformation("Clan {ClanTag} members joined: {Joined}", clanTag, string.Join(", ", joinedNames));
            await _messenger.SendReminderAsync(
                new ReminderMessage(
                    group.Platform,
                    group.PlatformGroupId,
                    string.Empty,
                    $"Joined clan {clanTag}: {string.Join(", ", joinedNames)}"),
                cancellationToken);
        }

        if (leftNames.Count > 0)
        {
            _logger.LogInformation("Clan {ClanTag} members left: {Left}", clanTag, string.Join(", ", leftNames));
            await _messenger.SendReminderAsync(
                new ReminderMessage(
                    group.Platform,
                    group.PlatformGroupId,
                    string.Empty,
                    $"Left clan {clanTag}: {string.Join(", ", leftNames)}"),
                cancellationToken);
        }

        LastKnownClanMembers[groupId] = current;
    }

    private async Task LogBattleProgressChangesAsync(Group group, ClanWarSnapshot snapshot, CancellationToken cancellationToken)
    {
        var current = snapshot.Members.ToDictionary(
            m => NormalizeTag(m.PlayerTag),
            m => m.BattlesPlayed,
            StringComparer.OrdinalIgnoreCase);

        if (!LastKnownBattles.TryGetValue(group.Id, out var previous))
        {
            previous = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var member in snapshot.Members)
        {
            var tag = NormalizeTag(member.PlayerTag);
            previous.TryGetValue(tag, out var prevPlayed);
            var nowPlayed = member.BattlesPlayed;

            if (nowPlayed <= prevPlayed)
            {
                continue;
            }

            var text = nowPlayed >= 4
                ? $"{member.PlayerName} ({tag}) played 4/4 battles for {group.ClanTag}."
                : $"{member.PlayerName} ({tag}) played {nowPlayed}/4 battles for {group.ClanTag}.";
            _logger.LogInformation(text);

            await _messenger.SendReminderAsync(
                new ReminderMessage(
                    group.Platform,
                    group.PlatformGroupId,
                    string.Empty,
                    text),
                cancellationToken);
        }

        LastKnownBattles[group.Id] = current;
    }

    private static string NormalizeTag(string tag)
    {
        var value = tag.Trim().ToUpperInvariant();
        return value.StartsWith('#') ? value : $"#{value}";
    }
}
