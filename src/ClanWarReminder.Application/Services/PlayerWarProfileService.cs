using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Domain.Common;

namespace ClanWarReminder.Application.Services;

public class PlayerWarProfileService
{
    private readonly IClashRoyaleClient _clashRoyaleClient;
    private readonly ClanWarHistoryService _historyService;

    public PlayerWarProfileService(
        IClashRoyaleClient clashRoyaleClient,
        ClanWarHistoryService historyService)
    {
        _clashRoyaleClient = clashRoyaleClient;
        _historyService = historyService;
    }

    public async Task<PlayerWarProfile> GetProfileAsync(string playerTag, int windowWeeks, CancellationToken cancellationToken)
    {
        var requestedWindow = Math.Clamp(windowWeeks, 1, 10);
        var identity = await _clashRoyaleClient.GetPlayerIdentityAsync(playerTag, cancellationToken);
        await _historyService.CaptureCurrentWeekAsync(identity.ClanTag, identity.ClanName, cancellationToken);

        var storedWeeks = await _historyService.GetPlayerWeeksAsync(identity.PlayerTag, cancellationToken);
        var fallbackWeeks = await _clashRoyaleClient.GetPlayerRecentWarWeeksAsync(identity.PlayerTag, cancellationToken);
        var mergedWeeks = MergeWeeks(storedWeeks, fallbackWeeks);
        var trackedWeeks = mergedWeeks.Take(requestedWindow).ToList();

        if (trackedWeeks.Count == 0)
        {
            trackedWeeks.Add(new PlayerWarWeekSummary(
                "NoData",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(4),
                identity.ClanTag,
                identity.ClanName,
                false,
                0,
                16,
                0,
                false,
                0,
                0,
                0,
                0,
                null));
        }

        var currentWeek = trackedWeeks
            .OrderByDescending(x => x.StartedAtUtc)
            .First();
        var overallParticipationRate = Math.Round(trackedWeeks.Average(x => x.ParticipationRate), 1);
        var fullCompletionRate = Math.Round(trackedWeeks.Count(x => x.CompletedAllBattles) * 100d / trackedWeeks.Count, 1);
        var averageBattlesPerWeek = Math.Round(trackedWeeks.Average(x => x.BattlesPlayed), 1);
        var totalTrackedWarBattles = trackedWeeks.Sum(x => x.BattlesPlayed);
        var recentWarWins = trackedWeeks.Sum(x => x.WarWins);
        var recentWarLosses = trackedWeeks.Sum(x => x.WarLosses);
        var recentWarWinRate = recentWarWins + recentWarLosses > 0
            ? Math.Round(recentWarWins * 100d / (recentWarWins + recentWarLosses), 1)
            : 0;
        var activityScore = BuildActivityScore(trackedWeeks);
        var hasMeaningfulHistory = trackedWeeks.Any(x => x.BattlesPlayed > 0 || (x.TotalContribution ?? 0) > 0);
        var activityLabel = !hasMeaningfulHistory
            ? "Нет данных"
            : overallParticipationRate >= 40
                ? "Активный"
                : activityScore >= 25
                    ? "Нестабильный"
                    : "Пассивный";

        var predictedNextWeekBattles = PredictNextWeekBattles(trackedWeeks, averageBattlesPerWeek);
        var predictedNextWeekContribution = PredictNextWeekContribution(trackedWeeks, currentWeek, predictedNextWeekBattles);
        var recentClans = BuildRecentClans(mergedWeeks);
        var availableHistoryWeeks = mergedWeeks.Count;
        var dataQualityLabel = BuildDataQualityLabel(storedWeeks.Count, availableHistoryWeeks, requestedWindow);
        var clanHistoryNote = storedWeeks.Count > 0
            ? "Основная статистика собрана из сохраненных недель КВ в базе. При нехватке истории остаток добирается из battle log Clash Royale API."
            : "История пока ограничена данными Clash Royale API. Полная точность появится после накопления недель КВ в базе.";

        return new PlayerWarProfile(
            identity.PlayerTag,
            identity.PlayerName,
            identity.ClanTag,
            identity.ClanName,
            requestedWindow,
            currentWeek.BattlesPlayed,
            currentWeek.MaxBattles,
            Math.Max(0, currentWeek.MaxBattles - currentWeek.BattlesPlayed),
            currentWeek.TotalContribution ?? 0,
            currentWeek.AverageContributionPerBattle ?? 0,
            overallParticipationRate,
            fullCompletionRate,
            totalTrackedWarBattles,
            averageBattlesPerWeek,
            activityScore,
            activityLabel,
            predictedNextWeekBattles,
            predictedNextWeekContribution,
            recentWarWins,
            recentWarLosses,
            recentWarWinRate,
            currentWeek.WarWins,
            currentWeek.WarLosses,
            currentWeek.WarWinRate ?? 0,
            trackedWeeks,
            recentClans,
            availableHistoryWeeks,
            recentClans.Count,
            clanHistoryNote,
            dataQualityLabel);
    }

    private static List<PlayerWarWeekSummary> MergeWeeks(
        IReadOnlyList<StoredPlayerWarWeek> storedWeeks,
        IReadOnlyList<PlayerWarWeekSummary> fallbackWeeks)
    {
        var merged = new Dictionary<string, PlayerWarWeekSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var week in fallbackWeeks)
        {
            merged[BuildMergeKey(week.WarKey, week.ClanTag)] = week;
        }

        foreach (var week in storedWeeks)
        {
            var mergeKey = BuildMergeKey(week.WarKey, week.ClanTag);
            merged.TryGetValue(mergeKey, out var fallbackWeek);
            merged[mergeKey] = new PlayerWarWeekSummary(
                week.WarKey,
                week.StartedAtUtc,
                week.EndedAtUtc,
                week.ClanTag,
                week.ClanName,
                week.BattlesPlayed >= 8 || week.TotalContribution >= 1200,
                week.BattlesPlayed,
                Math.Max(week.MaxBattles, 16),
                Math.Round((week.BattlesPlayed / (double)Math.Max(week.MaxBattles, 16)) * 100d, 1),
                week.BattlesPlayed >= Math.Max(week.MaxBattles, 16),
                week.TotalContribution,
                week.AverageContributionPerBattle > 0 ? week.AverageContributionPerBattle : null,
                fallbackWeek?.WarWins ?? 0,
                fallbackWeek?.WarLosses ?? 0,
                fallbackWeek?.WarWinRate);
        }

        return merged.Values
            .OrderByDescending(x => x.StartedAtUtc)
            .ToList();
    }

    private static List<PlayerRecentClanEntry> BuildRecentClans(IReadOnlyList<PlayerWarWeekSummary> weeks)
    {
        return weeks
            .GroupBy(x => x.ClanTag, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PlayerRecentClanEntry(
                group.First().ClanTag,
                group.First().ClanName,
                group.Sum(x => x.BattlesPlayed),
                group.Min(x => x.StartedAtUtc),
                group.Max(x => x.EndedAtUtc)))
            .OrderByDescending(x => x.LastSeenAtUtc)
            .Take(12)
            .ToList();
    }

    private static int PredictNextWeekBattles(IReadOnlyList<PlayerWarWeekSummary> weeks, double averageBattlesPerWeek)
    {
        var weightedBattles = weeks
            .Select((week, index) => new
            {
                week.BattlesPlayed,
                Weight = BuildPredictionWeight(index, week.IsColosseumWeighted)
            })
            .ToList();

        return weightedBattles.Count > 0
            ? Math.Clamp(
                (int)Math.Round(weightedBattles.Sum(x => x.BattlesPlayed * x.Weight) / weightedBattles.Sum(x => x.Weight), MidpointRounding.AwayFromZero),
                0,
                16)
            : Math.Clamp((int)Math.Round(averageBattlesPerWeek, MidpointRounding.AwayFromZero), 0, 16);
    }

    private static int PredictNextWeekContribution(
        IReadOnlyList<PlayerWarWeekSummary> weeks,
        PlayerWarWeekSummary currentWeek,
        int predictedNextWeekBattles)
    {
        var weightedContribution = weeks
            .Where(x => x.AverageContributionPerBattle.HasValue && x.AverageContributionPerBattle.Value > 0)
            .Select((week, index) => new
            {
                Average = week.AverageContributionPerBattle!.Value,
                Weight = BuildPredictionWeight(index, week.IsColosseumWeighted)
            })
            .ToList();

        var perBattleContribution = weightedContribution.Count > 0
            ? weightedContribution.Sum(x => x.Average * x.Weight) / weightedContribution.Sum(x => x.Weight)
            : (currentWeek.AverageContributionPerBattle > 0 ? currentWeek.AverageContributionPerBattle.Value : 100d);

        return (int)Math.Round(predictedNextWeekBattles * perBattleContribution, MidpointRounding.AwayFromZero);
    }

    private static string BuildDataQualityLabel(int storedWeeksCount, int availableHistoryWeeks, int requestedWindow)
    {
        if (storedWeeksCount >= requestedWindow)
        {
            return "Высокая";
        }

        if (storedWeeksCount >= 2 || availableHistoryWeeks >= requestedWindow)
        {
            return "Средняя";
        }

        return "Ограниченная";
    }

    private static string BuildMergeKey(string warKey, string clanTag)
        => $"{warKey}:{TagNormalizer.NormalizeClanOrPlayerTag(clanTag)}";

    private static double BuildPredictionWeight(int index, bool isColosseumWeighted)
    {
        var recencyWeight = index switch
        {
            0 => 1.8,
            1 => 1.45,
            2 => 1.2,
            3 => 1.0,
            _ => 0.85
        };

        return isColosseumWeighted ? recencyWeight * 1.35 : recencyWeight;
    }

    private static int BuildActivityScore(IReadOnlyList<PlayerWarWeekSummary> trackedWeeks)
    {
        if (trackedWeeks.Count == 0)
        {
            return 0;
        }

        var battlesRatio = trackedWeeks.Average(x => x.BattlesPlayed / Math.Max((double)x.MaxBattles, 16d));
        var completionRatio = trackedWeeks.Count(x => x.CompletedAllBattles) / (double)trackedWeeks.Count;
        var activeWeeksRatio = trackedWeeks.Count(x => x.BattlesPlayed >= 2) / (double)trackedWeeks.Count;
        var score = (battlesRatio * 55d) + (completionRatio * 20d) + (activeWeeksRatio * 25d);
        return Math.Clamp((int)Math.Round(score, MidpointRounding.AwayFromZero), 0, 100);
    }
}
