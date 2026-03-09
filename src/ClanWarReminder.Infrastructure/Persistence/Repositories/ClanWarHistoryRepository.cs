using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Domain.Common;
using ClanWarReminder.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClanWarReminder.Infrastructure.Persistence.Repositories;

public class ClanWarHistoryRepository : IClanWarHistoryRepository
{
    private readonly AppDbContext _db;

    public ClanWarHistoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertWeekAsync(
        string clanTag,
        string clanName,
        string warKey,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        IReadOnlyList<ClanWarMemberStatus> members,
        CancellationToken cancellationToken)
    {
        var normalizedClanTag = TagNormalizer.NormalizeClanOrPlayerTag(clanTag);
        var week = await _db.ClanWarWeeks
            .Include(x => x.Members)
            .FirstOrDefaultAsync(
                x => x.ClanTag == normalizedClanTag && x.WarKey == warKey,
                cancellationToken);

        if (week is null)
        {
            week = new ClanWarWeek
            {
                ClanTag = normalizedClanTag,
                ClanName = string.IsNullOrWhiteSpace(clanName) ? normalizedClanTag : clanName.Trim(),
                WarKey = warKey.Trim(),
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc
            };
            await _db.ClanWarWeeks.AddAsync(week, cancellationToken);
        }
        else
        {
            week.ClanName = string.IsNullOrWhiteSpace(clanName) ? week.ClanName : clanName.Trim();
            week.StartedAtUtc = startedAtUtc;
            week.EndedAtUtc = endedAtUtc;
            week.LastSeenAtUtc = DateTimeOffset.UtcNow;
        }

        var existingByTag = week.Members.ToDictionary(x => x.PlayerTag, StringComparer.OrdinalIgnoreCase);
        foreach (var member in members)
        {
            var normalizedPlayerTag = TagNormalizer.NormalizeClanOrPlayerTag(member.PlayerTag);
            if (!existingByTag.TryGetValue(normalizedPlayerTag, out var storedMember))
            {
                storedMember = new ClanWarWeekMember
                {
                    ClanWarWeekId = week.Id,
                    PlayerTag = normalizedPlayerTag
                };
                week.Members.Add(storedMember);
                existingByTag[normalizedPlayerTag] = storedMember;
            }

            storedMember.PlayerName = member.PlayerName.Trim();
            storedMember.BattlesPlayed = member.BattlesPlayed;
            storedMember.MaxBattles = member.BattlesPlayed + member.BattlesRemaining;
            storedMember.TotalContribution = member.TotalContribution ?? 0;
            storedMember.AverageContributionPerBattle = member.AverageContributionPerBattle ?? 0;
            storedMember.LastSeenAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public async Task<IReadOnlyList<StoredPlayerWarWeek>> GetPlayerWeeksAsync(string playerTag, CancellationToken cancellationToken)
    {
        var normalizedPlayerTag = TagNormalizer.NormalizeClanOrPlayerTag(playerTag);

        return await _db.ClanWarWeekMembers
            .AsNoTracking()
            .Where(x => x.PlayerTag == normalizedPlayerTag)
            .Select(x => new StoredPlayerWarWeek(
                x.ClanWarWeek.WarKey,
                x.ClanWarWeek.StartedAtUtc,
                x.ClanWarWeek.EndedAtUtc,
                x.ClanWarWeek.ClanTag,
                x.ClanWarWeek.ClanName,
                x.BattlesPlayed,
                x.MaxBattles,
                x.TotalContribution,
                x.AverageContributionPerBattle))
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }
}
