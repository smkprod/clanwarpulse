using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Application.Services;

public class ClanStatusService
{
    private readonly IGroupRepository _groups;
    private readonly IClashRoyaleClient _clashRoyale;
    private readonly ClanWarHistoryService _historyService;

    public ClanStatusService(IGroupRepository groups, IClashRoyaleClient clashRoyale, ClanWarHistoryService historyService)
    {
        _groups = groups;
        _clashRoyale = clashRoyale;
        _historyService = historyService;
    }

    public async Task<GroupStatusResult> GetStatusAsync(
        Domain.Enums.PlatformType platform,
        string platformGroupId,
        CancellationToken cancellationToken)
    {
        var group = await _groups.GetByPlatformGroupAsync(platform, platformGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Group is not configured. Run /setup first.");

        var dashboard = await GetDashboardByClanTagAsync(group.ClanTag, cancellationToken);
        return new GroupStatusResult(
            dashboard.ClanTag,
            dashboard.WarKey,
            dashboard.Played,
            dashboard.NotPlayed,
            dashboard.Opponents,
            dashboard.Forecast);
    }

    public async Task<ClanWarDashboard> GetDashboardByClanTagAsync(string clanTag, CancellationToken cancellationToken)
    {
        var snapshot = await _clashRoyale.GetCurrentWarAsync(clanTag, cancellationToken);
        var currentRaceClans = await _clashRoyale.GetCurrentRaceClansAsync(clanTag, cancellationToken);
        var opponents = await _clashRoyale.GetCurrentOpponentsAsync(clanTag, cancellationToken);
        var history = await _clashRoyale.GetRecentHistoryAsync(clanTag, cancellationToken);
        var forecast = await _clashRoyale.BuildForecastAsync(clanTag, opponents, cancellationToken);
        var played = snapshot.Members.Where(x => x.HasPlayed).ToList();
        var notPlayed = snapshot.Members.Where(x => !x.HasPlayed).ToList();

        var ownClanName = forecast.Ranking
            .FirstOrDefault(x => string.Equals(x.ClanTag, clanTag, StringComparison.OrdinalIgnoreCase))
            ?.ClanName
            ?? clanTag;

        await _historyService.CaptureCurrentWeekAsync(clanTag, ownClanName, snapshot, cancellationToken);

        return new ClanWarDashboard(
            clanTag,
            ownClanName,
            snapshot.WarKey,
            currentRaceClans,
            played,
            notPlayed,
            opponents,
            forecast,
            history);
    }

    public async Task<ClanWarDashboard> GetDashboardByPlayerTagAsync(string playerTag, CancellationToken cancellationToken)
    {
        var identity = await _clashRoyale.GetPlayerIdentityAsync(playerTag, cancellationToken);
        var dashboard = await GetDashboardByClanTagAsync(identity.ClanTag, cancellationToken);
        return dashboard with { ClanName = identity.ClanName };
    }
}
