using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json.Serialization;
using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Models;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ClanWarReminder.Infrastructure.Integrations.ClashRoyale;

public class ClashRoyaleClient : IClashRoyaleClient
{
    private readonly HttpClient _httpClient;
    private readonly ClashRoyaleOptions _options;

    public ClashRoyaleClient(HttpClient httpClient, IOptions<ClashRoyaleOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ClanWarSnapshot> GetCurrentWarAsync(string clanTag, CancellationToken cancellationToken)
    {
        var payload = await GetCurrentRiverRaceAsync(clanTag, cancellationToken);
        var clanMembers = await GetClanMembersRawAsync(clanTag, cancellationToken);
        const int maxBattlesPerDay = 4;
        var participantByTag = payload.Clan.Participants
            .ToDictionary(x => NormalizeTag(x.Tag), StringComparer.OrdinalIgnoreCase);

        var members = clanMembers.Select(member =>
        {
            participantByTag.TryGetValue(NormalizeTag(member.Tag), out var x);

            // Clash Royale payload fields vary by race state; use best available signal.
            var playedBattles = x is null
                ? 0
                : Math.Max(
                    Math.Max(x.BattlesPlayed, x.DecksUsedToday),
                    Math.Max(x.DecksUsed, x.BoatAttacks));

            if (x is not null && playedBattles == 0 && x.PeriodPoints > 0)
            {
                // periodPoints represents today's points in the race window.
                playedBattles = Math.Clamp((int)Math.Ceiling(x.PeriodPoints / 100.0), 1, maxBattlesPerDay);
            }

            if (x is not null && playedBattles == 0 && (x.Fame > 0 || x.RepairPoints > 0))
            {
                playedBattles = 1;
            }

            var totalContribution = x is null ? 0 : x.Fame + x.RepairPoints;
            var averageContribution = playedBattles > 0
                ? Math.Round(totalContribution / (double)playedBattles, 1)
                : 0;

            return new ClanWarMemberStatus(
                NormalizeTag(member.Tag),
                member.Name,
                playedBattles > 0,
                playedBattles,
                Math.Max(0, maxBattlesPerDay - playedBattles),
                totalContribution,
                averageContribution);
        })
        .Concat(payload.Clan.Participants
            .Where(x => !participantByTag.ContainsKey(NormalizeTag(x.Tag)) || !clanMembers.Any(member => string.Equals(NormalizeTag(member.Tag), NormalizeTag(x.Tag), StringComparison.OrdinalIgnoreCase)))
            .Select(x =>
            {
                var playedBattles = ResolveCurrentDayBattles(x, maxBattlesPerDay);
                var totalContribution = x.Fame + x.RepairPoints;
                var averageContribution = playedBattles > 0
                    ? Math.Round(totalContribution / (double)playedBattles, 1)
                    : 0;

                return new ClanWarMemberStatus(
                    NormalizeTag(x.Tag),
                    x.Name,
                    playedBattles > 0,
                    playedBattles,
                    Math.Max(0, maxBattlesPerDay - playedBattles),
                    totalContribution,
                    averageContribution);
            }))
        .GroupBy(x => NormalizeTag(x.PlayerTag), StringComparer.OrdinalIgnoreCase)
        .Select(group => group
            .OrderByDescending(x => x.HasPlayed)
            .ThenByDescending(x => x.BattlesPlayed)
            .First())
        .OrderByDescending(x => x.BattlesPlayed)
        .ThenBy(x => x.PlayerName)
        .ToList();

        return new ClanWarSnapshot(BuildWarKey(payload), members);
    }

    public async Task<IReadOnlyList<ClanWarMemberStatus>> GetClanMembersAsync(string clanTag, CancellationToken cancellationToken)
    {
        var members = await GetClanMembersRawAsync(clanTag, cancellationToken);
        return members
            .Select(member => new ClanWarMemberStatus(
                NormalizeTag(member.Tag),
                member.Name,
                false,
                0,
                0,
                0,
                0))
            .OrderBy(member => member.PlayerName)
            .ToList();
    }

    public async Task<PlayerIdentityResult> GetPlayerIdentityAsync(string playerTag, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(NormalizeTag(playerTag));
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/players/{encodedTag}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PlayerResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Clash Royale API returned empty player payload.");

        if (payload.Clan is null || string.IsNullOrWhiteSpace(payload.Clan.Tag))
        {
            throw new InvalidOperationException("Player is not currently in a clan.");
        }

        return new PlayerIdentityResult(
            NormalizeTag(payload.Tag),
            payload.Name,
            NormalizeTag(payload.Clan.Tag),
            payload.Clan.Name);
    }

    public async Task<IReadOnlyList<ClanWarOpponentStatus>> GetCurrentOpponentsAsync(string clanTag, CancellationToken cancellationToken)
    {
        var selfTag = NormalizeTag(clanTag);
        var clans = await GetCurrentRaceClansAsync(clanTag, cancellationToken);

        return clans
            .Where(x => !string.Equals(x.ClanTag, selfTag, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ClanWarOpponentStatus(
                x.ClanTag,
                x.ClanName,
                x.Fame,
                x.RepairPoints,
                x.PeriodPoints,
                x.TotalScore,
                x.ParticipantsCount))
            .OrderByDescending(x => x.TotalScore)
            .ToList();
    }

    public async Task<IReadOnlyList<ClanCurrentRaceClan>> GetCurrentRaceClansAsync(string clanTag, CancellationToken cancellationToken)
    {
        var payload = await GetCurrentRiverRaceAsync(clanTag, cancellationToken);
        var clans = payload.Clans.Count > 0
            ? payload.Clans
            : new List<RaceClanDto> { payload.Clan };

        return clans
            .Select(MapCurrentRaceClan)
            .OrderByDescending(x => x.TotalScore)
            .ToList();
    }

    public async Task<ClanWarForecastResult> BuildForecastAsync(
        string clanTag,
        IReadOnlyList<ClanWarOpponentStatus> currentOpponents,
        CancellationToken cancellationToken)
    {
        var currentRaceClans = await GetCurrentRaceClansAsync(clanTag, cancellationToken);
        var fallbackHistory = await GetRecentHistoryAsync(clanTag, cancellationToken);
        var fallbackByClan = BuildHistoryMap(fallbackHistory);

        // Forecast is limited to clans that are in the current river race only.
        var candidates = currentRaceClans
            .ToDictionary(
                x => x.ClanTag,
                x => (name: x.ClanName, currentScore: x.TotalScore),
                StringComparer.OrdinalIgnoreCase);

        var ranking = new List<ClanWarForecastItem>();

        foreach (var (tag, info) in candidates)
        {
            var (scoreSamples, rankSamples) = await GetClanHistorySamplesAsync(tag, fallbackByClan, cancellationToken);

            var avgScore = scoreSamples.Count > 0 ? scoreSamples.Average() : info.currentScore;
            var avgRank = rankSamples.Count > 0 ? rankSamples.Average() : 5.0;

            // Weighted blend: historical level + current race pace.
            var predictedScore = (int)Math.Round((avgScore * 0.7) + (info.currentScore * 0.3), MidpointRounding.AwayFromZero);

            ranking.Add(new ClanWarForecastItem(
                tag,
                info.name,
                predictedScore,
                Math.Round(avgScore, 1),
                Math.Round(avgRank, 2),
                scoreSamples.Count));
        }

        var ordered = ranking
            .OrderByDescending(x => x.PredictedScore)
            .ThenBy(x => x.RecentAverageRank)
            .ToList();

        return new ClanWarForecastResult(
            "70% recent river-race history + 30% current race score",
            ordered);
    }

    private async Task<(List<int> Scores, List<int> Ranks)> GetClanHistorySamplesAsync(
        string clanTag,
        Dictionary<string, (List<int> Scores, List<int> Ranks)> fallbackByClan,
        CancellationToken cancellationToken)
    {
        try
        {
            var ownHistory = await GetRecentHistoryAsync(clanTag, cancellationToken);
            var selfTag = NormalizeTag(clanTag);

            var scores = new List<int>();
            var ranks = new List<int>();

            foreach (var race in ownHistory)
            {
                var entry = race.Results.FirstOrDefault(x => string.Equals(x.ClanTag, selfTag, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    continue;
                }

                scores.Add(entry.Score);
                ranks.Add(entry.Rank);
            }

            if (scores.Count > 0)
            {
                return (scores, ranks);
            }
        }
        catch
        {
            // Fallback to race history obtained from the main clan context.
        }

        var normalized = NormalizeTag(clanTag);
        if (fallbackByClan.TryGetValue(normalized, out var fallback))
        {
            return (fallback.Scores, fallback.Ranks);
        }

        return (new List<int>(), new List<int>());
    }

    private static Dictionary<string, (List<int> Scores, List<int> Ranks)> BuildHistoryMap(IReadOnlyList<ClanWarHistoryEntry> history)
    {
        var result = new Dictionary<string, (List<int> Scores, List<int> Ranks)>(StringComparer.OrdinalIgnoreCase);

        foreach (var race in history)
        {
            foreach (var row in race.Results)
            {
                var key = NormalizeTag(row.ClanTag);
                if (!result.TryGetValue(key, out var bucket))
                {
                    bucket = (new List<int>(), new List<int>());
                    result[key] = bucket;
                }

                bucket.Scores.Add(row.Score);
                bucket.Ranks.Add(row.Rank);
            }
        }

        return result;
    }

    public async Task<ClanWarClanDetails> GetClanDetailsAsync(string clanTag, CancellationToken cancellationToken)
    {
        var payload = await GetCurrentRiverRaceAsync(clanTag, cancellationToken);
        var clan = payload.Clan;
        var normalizedClanTag = NormalizeTag(clan.Tag);
        var history = await GetRecentHistoryAsync(normalizedClanTag, cancellationToken);

        var topContributors = clan.Participants
            .Select(x =>
            {
                var total = x.Fame + x.RepairPoints;
                return new ClanContributorStats(
                    NormalizeTag(x.Tag),
                    x.Name,
                    x.Fame,
                    x.RepairPoints,
                    total,
                    x.BattlesPlayed);
            })
            .OrderByDescending(x => x.TotalContribution)
            .ThenByDescending(x => x.Fame)
            .Take(20)
            .ToList();

        var recentWars = history
            .Select(entry => new
            {
                entry.WarKey,
                Result = entry.Results.FirstOrDefault(x => string.Equals(x.ClanTag, normalizedClanTag, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Result is not null)
            .Select(x => new ClanWarClanHistoryPoint(
                x.WarKey,
                x.Result!.Score,
                x.Result.Rank))
            .ToList();

        var avgScore = recentWars.Count > 0 ? recentWars.Average(x => x.Score) : 0;
        var bestScore = recentWars.Count > 0 ? recentWars.Max(x => x.Score) : 0;
        var currentScore = clan.TotalClanScore > 0 ? clan.TotalClanScore : clan.Fame + clan.RepairPoints;

        return new ClanWarClanDetails(
            normalizedClanTag,
            clan.Name,
            currentScore,
            clan.Fame,
            clan.RepairPoints,
            clan.Participants.Count,
            Math.Round(avgScore, 1),
            bestScore,
            topContributors,
            recentWars);
    }

    public async Task<IReadOnlyList<PlayerWarWeekSummary>> GetPlayerRecentWarWeeksAsync(string playerTag, CancellationToken cancellationToken)
    {
        var identity = await GetPlayerIdentityAsync(playerTag, cancellationToken);
        var currentRace = await GetCurrentRiverRaceAsync(identity.ClanTag, cancellationToken);
        var riverRaceLog = await GetRiverRaceLogAsync(identity.ClanTag, cancellationToken);
        var currentParticipant = currentRace.Clan.Participants
            .FirstOrDefault(x => string.Equals(NormalizeTag(x.Tag), identity.PlayerTag, StringComparison.OrdinalIgnoreCase));

        var currentWeekBattles = currentParticipant is null ? 0 : ResolvePlayedBattles(currentParticipant);
        var currentWeekContribution = currentParticipant is null ? 0 : currentParticipant.Fame + currentParticipant.RepairPoints;
        var currentWeekAverage = currentWeekBattles > 0
            ? Math.Round(currentWeekContribution / (double)currentWeekBattles, 1)
            : 0;

        var battleLog = await GetPlayerBattleLogAsync(playerTag, cancellationToken);
        var warEntries = battleLog
            .Where(IsWarBattle)
            .Select(MapWarBattleEntry)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.BattleTimeUtc)
            .ToList();

        var recentWeeks = warEntries
            .GroupBy(x => BuildWeekKey(x.BattleTimeUtc))
            .Select(group =>
            {
                var startedAt = StartOfWarWeek(group.Max(x => x.BattleTimeUtc));
                var endAt = startedAt.AddDays(4);
                var weekClan = group
                    .GroupBy(x => NormalizeTag(x.ClanTag))
                    .OrderByDescending(x => x.Count())
                    .Select(x => x.First())
                    .FirstOrDefault();
                var battlesPlayed = group.Count();
                var contributionSum = group
                    .Where(x => x.Contribution.HasValue)
                    .Select(x => x.Contribution!.Value)
                    .DefaultIfEmpty()
                    .Sum();
                var hasContribution = group.Any(x => x.Contribution.HasValue);
                var warWins = group.Count(x => x.IsWin == true);
                var warLosses = group.Count(x => x.IsWin == false);
                double? avgContributionPerBattle = hasContribution && battlesPlayed > 0
                    ? Math.Round(contributionSum / (double)battlesPlayed, 1)
                    : null;
                double? warWinRate = warWins + warLosses > 0
                    ? Math.Round(warWins * 100d / (warWins + warLosses), 1)
                    : null;
                return new PlayerWarWeekSummary(
                    group.Key,
                    startedAt,
                    endAt,
                    weekClan?.ClanTag ?? identity.ClanTag,
                    weekClan?.ClanName ?? identity.ClanName,
                    false,
                    battlesPlayed,
                    16,
                    Math.Round((battlesPlayed / 16d) * 100d, 1),
                    battlesPlayed >= 16,
                    hasContribution ? contributionSum : null,
                    avgContributionPerBattle,
                    warWins,
                    warLosses,
                    warWinRate);
            })
            .OrderByDescending(x => x.StartedAtUtc)
            .ToList();

        var raceLogWeeks = riverRaceLog.Items
            .Select(item =>
            {
                var ownStanding = item.Standings.FirstOrDefault(x => string.Equals(NormalizeTag(x.Clan.Tag), identity.ClanTag, StringComparison.OrdinalIgnoreCase));
                var participant = ownStanding?.Clan.Participants
                    .FirstOrDefault(x => string.Equals(NormalizeTag(x.Tag), identity.PlayerTag, StringComparison.OrdinalIgnoreCase));

                if (ownStanding is null || participant is null)
                {
                    return null;
                }

                var startedAt = ResolveRaceLogStartedAtUtc(item);
                var endedAt = startedAt.AddDays(4);
                var maxBattles = Math.Max(participant.NumberOfBattles, participant.BattlesPlayed);
                maxBattles = maxBattles > 0 ? maxBattles : 16;
                var totalContribution = participant.CardsEarned;
                double? averageContributionPerBattle = participant.BattlesPlayed > 0
                    ? Math.Round(totalContribution / (double)participant.BattlesPlayed, 1)
                    : null;
                double? warWinRate = participant.BattlesPlayed > 0
                    ? Math.Round(participant.Wins * 100d / participant.BattlesPlayed, 1)
                    : null;

                return new PlayerWarWeekSummary(
                    BuildWarKey(item.SeasonId, item.SectionIndex, 0),
                    startedAt,
                    endedAt,
                    NormalizeTag(ownStanding.Clan.Tag),
                    ownStanding.Clan.Name,
                    false,
                    participant.BattlesPlayed,
                    maxBattles,
                    Math.Round((participant.BattlesPlayed / (double)maxBattles) * 100d, 1),
                    participant.BattlesPlayed >= maxBattles,
                    totalContribution,
                    averageContributionPerBattle,
                    participant.Wins,
                    Math.Max(0, participant.BattlesPlayed - participant.Wins),
                    warWinRate);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        recentWeeks = recentWeeks
            .Concat(raceLogWeeks)
            .GroupBy(x => BuildMergeKey(x.WarKey, x.ClanTag))
            .Select(group => group
                .OrderByDescending(x => x.TotalContribution ?? 0)
                .ThenByDescending(x => x.BattlesPlayed)
                .First())
            .OrderByDescending(x => x.StartedAtUtc)
            .ToList();

        if (currentParticipant is not null)
        {
            var currentWeekKey = BuildWarKey(currentRace);
            var startedAt = StartOfWarWeek(DateTimeOffset.UtcNow);
            var existingIndex = recentWeeks.FindIndex(x => string.Equals(x.WarKey, currentWeekKey, StringComparison.OrdinalIgnoreCase));
            var currentWeek = new PlayerWarWeekSummary(
                currentWeekKey,
                startedAt,
                startedAt.AddDays(4),
                identity.ClanTag,
                identity.ClanName,
                false,
                currentWeekBattles,
                16,
                Math.Round((currentWeekBattles / 16d) * 100d, 1),
                currentWeekBattles >= 16,
                currentWeekContribution,
                currentWeekBattles > 0 ? Math.Round(currentWeekContribution / (double)currentWeekBattles, 1) : 0,
                existingIndex >= 0 ? recentWeeks[existingIndex].WarWins : 0,
                existingIndex >= 0 ? recentWeeks[existingIndex].WarLosses : 0,
                existingIndex >= 0 ? recentWeeks[existingIndex].WarWinRate : null);

            if (existingIndex >= 0)
            {
                recentWeeks[existingIndex] = currentWeek;
            }
            else
            {
                recentWeeks.Insert(0, currentWeek);
            }
        }

        return recentWeeks
            .OrderByDescending(x => x.StartedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ClanWarHistoryEntry>> GetRecentHistoryAsync(string clanTag, CancellationToken cancellationToken)
    {
        var log = await GetRiverRaceLogAsync(clanTag, cancellationToken);
        var entries = new List<ClanWarHistoryEntry>();

        foreach (var item in log.Items)
        {
            var results = item.Standings
                .Select(x =>
                {
                    var score = x.Clan.TotalClanScore > 0
                        ? x.Clan.TotalClanScore
                        : x.Clan.Fame + x.Clan.RepairPoints;

                    return new ClanWarHistoryClanResult(
                        NormalizeTag(x.Clan.Tag),
                        x.Clan.Name,
                        score,
                        Math.Max(x.Rank, 1));
                })
                .OrderBy(x => x.Rank)
                .ToList();

            var key = BuildWarKey(item.SeasonId, item.SectionIndex, entries.Count + 1);
            entries.Add(new ClanWarHistoryEntry(key, results));
        }

        return entries;
    }

    private async Task<CurrentRiverRaceResponse> GetCurrentRiverRaceAsync(string clanTag, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(NormalizeTag(clanTag));
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/clans/{encodedTag}/currentriverrace");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CurrentRiverRaceResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Clash Royale API returned empty current race payload.");
    }

    private async Task<RiverRaceLogResponse> GetRiverRaceLogAsync(string clanTag, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(NormalizeTag(clanTag));
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/clans/{encodedTag}/riverracelog?limit=25");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RiverRaceLogResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Clash Royale API returned empty river race log payload.");
    }

    private async Task<List<PlayerBattleLogEntryDto>> GetPlayerBattleLogAsync(string playerTag, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(NormalizeTag(playerTag));
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/players/{encodedTag}/battlelog");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<PlayerBattleLogEntryDto>>(cancellationToken: cancellationToken)
            ?? new List<PlayerBattleLogEntryDto>();
    }

    private async Task<List<ClanMemberDto>> GetClanMembersRawAsync(string clanTag, CancellationToken cancellationToken)
    {
        var encodedTag = Uri.EscapeDataString(NormalizeTag(clanTag));
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/clans/{encodedTag}/members");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ClanMembersResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Clash Royale API returned empty clan members payload.");

        return payload.Items;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
        return request;
    }

    private static string BuildWarKey(CurrentRiverRaceResponse payload)
    {
        return payload.SectionIndex > 0
            ? $"{payload.SeasonId}-{payload.SectionIndex}"
            : payload.SeasonId.ToString();
    }

    private static string BuildWarKey(int? seasonId, int? sectionIndex, int fallbackIndex)
    {
        if (seasonId is null or <= 0)
        {
            return $"Recent-{fallbackIndex}";
        }

        if (sectionIndex is > 0)
        {
            return $"{seasonId}-{sectionIndex}";
        }

        return seasonId.Value.ToString();
    }

    private static string BuildMergeKey(string warKey, string clanTag)
        => $"{warKey}:{NormalizeTag(clanTag)}";

    private static string NormalizeTag(string tag)
    {
        var value = tag.Trim().ToUpperInvariant();
        return value.StartsWith('#') ? value : $"#{value}";
    }

    private static int ResolvePlayedBattles(ParticipantDto participant)
    {
        return ResolveCurrentDayBattles(participant, 16);
    }

    private static int ResolveCurrentDayBattles(ParticipantDto participant, int maxBattles)
    {
        var playedBattles = Math.Max(
            Math.Max(participant.BattlesPlayed, participant.DecksUsedToday),
            Math.Max(participant.DecksUsed, participant.BoatAttacks));

        if (playedBattles == 0 && participant.PeriodPoints > 0)
        {
            playedBattles = Math.Clamp((int)Math.Ceiling(participant.PeriodPoints / 100.0), 1, maxBattles);
        }

        if (playedBattles == 0 && (participant.Fame > 0 || participant.RepairPoints > 0))
        {
            playedBattles = 1;
        }

        return playedBattles;
    }

    private static bool IsWarBattle(PlayerBattleLogEntryDto entry)
    {
        var type = entry.Type ?? string.Empty;
        return type.Contains("river", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("boat", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("war", StringComparison.OrdinalIgnoreCase);
    }

    private static PlayerWarBattleEntry? MapWarBattleEntry(PlayerBattleLogEntryDto entry)
    {
        if (!TryParseBattleTime(entry.BattleTime, out var battleTimeUtc))
        {
            return null;
        }

        var team = entry.Team.FirstOrDefault();
        var clan = team?.Clan ?? entry.Opponent.FirstOrDefault()?.Clan;
        if (clan is null || string.IsNullOrWhiteSpace(clan.Tag))
        {
            return null;
        }

        int? contribution = null;
        if (entry.Fame > 0)
        {
            contribution = entry.Fame;
        }
        else if (team?.Crowns is > 0)
        {
            contribution = team.Crowns.Value * 100;
        }

        var teamCrowns = team?.Crowns;
        var opponentCrowns = entry.Opponent.FirstOrDefault()?.Crowns;
        bool? isWin = null;
        if (teamCrowns.HasValue && opponentCrowns.HasValue && teamCrowns.Value != opponentCrowns.Value)
        {
            isWin = teamCrowns.Value > opponentCrowns.Value;
        }

        return new PlayerWarBattleEntry(
            battleTimeUtc,
            NormalizeTag(clan.Tag),
            clan.Name,
            contribution,
            isWin);
    }

    private static bool TryParseBattleTime(string? value, out DateTimeOffset battleTimeUtc)
    {
        if (DateTimeOffset.TryParseExact(
            value,
            new[]
            {
                "yyyyMMdd'T'HHmmss'.'fff'Z'",
                "yyyyMMdd'T'HHmmss'.'FF'Z'",
                "yyyyMMdd'T'HHmmss'Z'"
            },
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out battleTimeUtc))
        {
            return true;
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out battleTimeUtc);
    }

    private static string BuildWeekKey(DateTimeOffset battleTimeUtc)
    {
        var weekStart = StartOfWarWeek(battleTimeUtc);
        var week = ISOWeek.GetWeekOfYear(weekStart.UtcDateTime);
        return $"{weekStart.Year}-W{week:00}";
    }

    private static DateTimeOffset ResolveRaceLogStartedAtUtc(RiverRaceLogItemDto item)
    {
        if (TryParseBattleTime(item.CreatedDate, out var createdAtUtc))
        {
            return StartOfWarWeek(createdAtUtc);
        }

        return item.SeasonId is > 0
            ? StartOfWarWeek(DateTimeOffset.UtcNow)
            : DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset StartOfWarWeek(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var offset = utc.DayOfWeek switch
        {
            DayOfWeek.Thursday => 0,
            DayOfWeek.Friday => 1,
            DayOfWeek.Saturday => 2,
            DayOfWeek.Sunday => 3,
            DayOfWeek.Monday => 4,
            DayOfWeek.Tuesday => 5,
            _ => 6
        };
        var weekStart = utc.UtcDateTime.Date.AddDays(-offset);
        return new DateTimeOffset(weekStart, TimeSpan.Zero);
    }

    private static ClanWarOpponentStatus MapOpponent(RaceClanDto clan)
    {
        var totalScore = clan.TotalClanScore > 0
            ? clan.TotalClanScore
            : clan.Fame + clan.RepairPoints;

        return new ClanWarOpponentStatus(
            NormalizeTag(clan.Tag),
            clan.Name,
            clan.Fame,
            clan.RepairPoints,
            clan.PeriodPoints,
            totalScore,
            clan.Participants.Count);
    }

    private static ClanCurrentRaceClan MapCurrentRaceClan(RaceClanDto clan)
    {
        var totalScore = clan.TotalClanScore > 0
            ? clan.TotalClanScore
            : clan.Fame + clan.RepairPoints;

        return new ClanCurrentRaceClan(
            NormalizeTag(clan.Tag),
            clan.Name,
            clan.Fame,
            clan.RepairPoints,
            clan.PeriodPoints,
            totalScore,
            clan.Participants.Count);
    }

    private sealed class PlayerResponse
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("clan")]
        public PlayerClanDto? Clan { get; set; }
    }

    private sealed class PlayerClanDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class CurrentRiverRaceResponse
    {
        [JsonPropertyName("seasonId")]
        public int SeasonId { get; set; }

        [JsonPropertyName("sectionIndex")]
        public int SectionIndex { get; set; }

        [JsonPropertyName("clan")]
        public RaceClanDto Clan { get; set; } = new();

        [JsonPropertyName("clans")]
        public List<RaceClanDto> Clans { get; set; } = new();
    }

    private sealed class RaceClanDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("fame")]
        public int Fame { get; set; }

        [JsonPropertyName("repairPoints")]
        public int RepairPoints { get; set; }

        [JsonPropertyName("totalClanScore")]
        public int TotalClanScore { get; set; }

        [JsonPropertyName("periodPoints")]
        public int PeriodPoints { get; set; }

        [JsonPropertyName("participants")]
        public List<ParticipantDto> Participants { get; set; } = new();
    }

    private sealed class ParticipantDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("battlesPlayed")]
        public int BattlesPlayed { get; set; }

        [JsonPropertyName("decksUsed")]
        public int DecksUsed { get; set; }

        [JsonPropertyName("decksUsedToday")]
        public int DecksUsedToday { get; set; }

        [JsonPropertyName("boatAttacks")]
        public int BoatAttacks { get; set; }

        [JsonPropertyName("periodPoints")]
        public int PeriodPoints { get; set; }

        [JsonPropertyName("fame")]
        public int Fame { get; set; }

        [JsonPropertyName("repairPoints")]
        public int RepairPoints { get; set; }
    }

    private sealed class ClanMembersResponse
    {
        [JsonPropertyName("items")]
        public List<ClanMemberDto> Items { get; set; } = new();
    }

    private sealed class ClanMemberDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RiverRaceLogResponse
    {
        [JsonPropertyName("items")]
        public List<RiverRaceLogItemDto> Items { get; set; } = new();
    }

    private sealed class RiverRaceLogItemDto
    {
        [JsonPropertyName("seasonId")]
        public int? SeasonId { get; set; }

        [JsonPropertyName("createdDate")]
        public string? CreatedDate { get; set; }

        [JsonPropertyName("sectionIndex")]
        public int? SectionIndex { get; set; }

        [JsonPropertyName("standings")]
        public List<RiverRaceStandingDto> Standings { get; set; } = new();
    }

    private sealed class RiverRaceStandingDto
    {
        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        [JsonPropertyName("clan")]
        public RiverRaceClanLogDto Clan { get; set; } = new();
    }

    private sealed class RiverRaceClanLogDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("fame")]
        public int Fame { get; set; }

        [JsonPropertyName("repairPoints")]
        public int RepairPoints { get; set; }

        [JsonPropertyName("totalClanScore")]
        public int TotalClanScore { get; set; }

        [JsonPropertyName("participants")]
        public List<RiverRaceLogParticipantDto> Participants { get; set; } = new();
    }

    private sealed class RiverRaceLogParticipantDto
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("cardsEarned")]
        public int CardsEarned { get; set; }

        [JsonPropertyName("battlesPlayed")]
        public int BattlesPlayed { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("collectionDayBattlesPlayed")]
        public int CollectionDayBattlesPlayed { get; set; }

        [JsonPropertyName("numberOfBattles")]
        public int NumberOfBattles { get; set; }
    }

    private sealed class PlayerBattleLogEntryDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("battleTime")]
        public string BattleTime { get; set; } = string.Empty;

        [JsonPropertyName("fame")]
        public int Fame { get; set; }

        [JsonPropertyName("team")]
        public List<PlayerBattleParticipantDto> Team { get; set; } = new();

        [JsonPropertyName("opponent")]
        public List<PlayerBattleParticipantDto> Opponent { get; set; } = new();
    }

    private sealed class PlayerBattleParticipantDto
    {
        [JsonPropertyName("crowns")]
        public int? Crowns { get; set; }

        [JsonPropertyName("clan")]
        public PlayerClanDto? Clan { get; set; }
    }

    private sealed record PlayerWarBattleEntry(
        DateTimeOffset BattleTimeUtc,
        string ClanTag,
        string ClanName,
        int? Contribution,
        bool? IsWin);
}
