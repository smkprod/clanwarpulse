using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Application.Abstractions.Integrations;

public interface IClashRoyaleClient
{
    Task<ClanWarSnapshot> GetCurrentWarAsync(string clanTag, CancellationToken cancellationToken);
    Task<PlayerIdentityResult> GetPlayerIdentityAsync(string playerTag, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClanCurrentRaceClan>> GetCurrentRaceClansAsync(string clanTag, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClanWarOpponentStatus>> GetCurrentOpponentsAsync(string clanTag, CancellationToken cancellationToken);
    Task<ClanWarForecastResult> BuildForecastAsync(string clanTag, IReadOnlyList<ClanWarOpponentStatus> currentOpponents, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClanWarHistoryEntry>> GetRecentHistoryAsync(string clanTag, CancellationToken cancellationToken);
    Task<ClanWarClanDetails> GetClanDetailsAsync(string clanTag, CancellationToken cancellationToken);
    Task<PlayerWarProfile> GetPlayerWarProfileAsync(string playerTag, CancellationToken cancellationToken);
}
