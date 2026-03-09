using ClanWarReminder.Domain.Enums;

namespace ClanWarReminder.Application.Models;

public sealed record ClanWarMemberStatus(
    string PlayerTag,
    string PlayerName,
    bool HasPlayed,
    int BattlesPlayed,
    int BattlesRemaining,
    int? TotalContribution = null,
    double? AverageContributionPerBattle = null);

public sealed record ClanWarSnapshot(string WarKey, IReadOnlyList<ClanWarMemberStatus> Members);

public sealed record ClanWarOpponentStatus(
    string ClanTag,
    string ClanName,
    int Fame,
    int RepairPoints,
    int PeriodPoints,
    int TotalScore,
    int ParticipantsCount);

public sealed record ClanCurrentRaceClan(
    string ClanTag,
    string ClanName,
    int Fame,
    int RepairPoints,
    int PeriodPoints,
    int TotalScore,
    int ParticipantsCount);

public sealed record ClanContributorStats(
    string PlayerTag,
    string PlayerName,
    int Fame,
    int RepairPoints,
    int TotalContribution,
    int BattlesPlayed);

public sealed record ClanWarClanHistoryPoint(
    string WarKey,
    int Score,
    int Rank);

public sealed record ClanWarClanDetails(
    string ClanTag,
    string ClanName,
    int CurrentScore,
    int Fame,
    int RepairPoints,
    int ParticipantsCount,
    double AverageRecentScore,
    int BestRecentScore,
    IReadOnlyList<ClanContributorStats> TopContributors,
    IReadOnlyList<ClanWarClanHistoryPoint> RecentWars);

public sealed record ClanWarForecastItem(
    string ClanTag,
    string ClanName,
    int PredictedScore,
    double RecentAverageScore,
    double RecentAverageRank,
    int SampleSize);

public sealed record ClanWarForecastResult(
    string Basis,
    IReadOnlyList<ClanWarForecastItem> Ranking);

public sealed record ClanWarHistoryClanResult(
    string ClanTag,
    string ClanName,
    int Score,
    int Rank);

public sealed record ClanWarHistoryEntry(
    string WarKey,
    IReadOnlyList<ClanWarHistoryClanResult> Results);

public sealed record ClanWarDashboard(
    string ClanTag,
    string ClanName,
    string WarKey,
    IReadOnlyList<ClanCurrentRaceClan> CurrentRaceClans,
    IReadOnlyList<ClanWarMemberStatus> Played,
    IReadOnlyList<ClanWarMemberStatus> NotPlayed,
    IReadOnlyList<ClanWarOpponentStatus> Opponents,
    ClanWarForecastResult Forecast,
    IReadOnlyList<ClanWarHistoryEntry> History);

public sealed record PlayerIdentityResult(
    string PlayerTag,
    string PlayerName,
    string ClanTag,
    string ClanName);

public sealed record PlayerWarWeekSummary(
    string WarKey,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc,
    string ClanTag,
    string ClanName,
    bool IsColosseumWeighted,
    int BattlesPlayed,
    int MaxBattles,
    double ParticipationRate,
    bool CompletedAllBattles,
    int? TotalContribution,
    double? AverageContributionPerBattle);

public sealed record PlayerRecentClanEntry(
    string ClanTag,
    string ClanName,
    int WarBattles,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record PlayerWarProfile(
    string PlayerTag,
    string PlayerName,
    string CurrentClanTag,
    string CurrentClanName,
    int ProfileWindowWeeks,
    int CurrentWeekBattlesPlayed,
    int CurrentWeekMaxBattles,
    int CurrentWeekBattlesRemaining,
    int CurrentWeekContribution,
    double CurrentWeekAverageContributionPerBattle,
    double OverallParticipationRate,
    double FullCompletionRate,
    int TotalTrackedWarBattles,
    double AverageBattlesPerWeek,
    int ActivityScore,
    string ActivityLabel,
    int PredictedNextWeekBattles,
    int PredictedNextWeekContribution,
    IReadOnlyList<PlayerWarWeekSummary> RecentWeeks,
    IReadOnlyList<PlayerRecentClanEntry> RecentClans,
    int AvailableClanHistoryEntries,
    string ClanHistoryNote);

public sealed record ReminderMessage(
    PlatformType Platform,
    string PlatformGroupId,
    string PlatformUserId,
    string Text,
    bool IsHtml = false);

public sealed record GroupStatusResult(
    string ClanTag,
    string WarKey,
    IReadOnlyList<ClanWarMemberStatus> Played,
    IReadOnlyList<ClanWarMemberStatus> NotPlayed,
    IReadOnlyList<ClanWarOpponentStatus>? Opponents = null,
    ClanWarForecastResult? Forecast = null);
