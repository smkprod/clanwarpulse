using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Application.Abstractions.Persistence;

public interface IClanWarHistoryRepository
{
    Task UpsertWeekAsync(
        string clanTag,
        string clanName,
        string warKey,
        DateTimeOffset startedAtUtc,
        DateTimeOffset endedAtUtc,
        IReadOnlyList<ClanWarMemberStatus> members,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredPlayerWarWeek>> GetPlayerWeeksAsync(string playerTag, CancellationToken cancellationToken);
}
