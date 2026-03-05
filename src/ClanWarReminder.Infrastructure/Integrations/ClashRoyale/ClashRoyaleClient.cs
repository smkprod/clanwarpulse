using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        const int maxBattlesPerDay = 4;

        var members = payload.Clan.Participants.Select(x =>
        {
            // Clash Royale payload fields vary by race state; use best available signal.
            var playedBattles = Math.Max(
                Math.Max(x.BattlesPlayed, x.DecksUsedToday),
                Math.Max(x.DecksUsed, x.BoatAttacks));

            if (playedBattles == 0 && x.PeriodPoints > 0)
            {
                // periodPoints represents today's points in the race window.
                playedBattles = Math.Clamp((int)Math.Ceiling(x.PeriodPoints / 100.0), 1, maxBattlesPerDay);
            }

            if (playedBattles == 0 && (x.Fame > 0 || x.RepairPoints > 0))
            {
                playedBattles = 1;
            }

            return new ClanWarMemberStatus(
                x.Tag,
                x.Name,
                playedBattles > 0,
                playedBattles,
                Math.Max(0, maxBattlesPerDay - playedBattles));
        }).ToList();

        return new ClanWarSnapshot(BuildWarKey(payload), members);
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
        using var request = CreateRequest(HttpMethod.Get, $"{_options.BaseUrl}/clans/{encodedTag}/riverracelog?limit=10");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RiverRaceLogResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Clash Royale API returned empty river race log payload.");
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

    private static string NormalizeTag(string tag)
    {
        var value = tag.Trim().ToUpperInvariant();
        return value.StartsWith('#') ? value : $"#{value}";
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

    private sealed class RiverRaceLogResponse
    {
        [JsonPropertyName("items")]
        public List<RiverRaceLogItemDto> Items { get; set; } = new();
    }

    private sealed class RiverRaceLogItemDto
    {
        [JsonPropertyName("seasonId")]
        public int? SeasonId { get; set; }

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
    }
}
