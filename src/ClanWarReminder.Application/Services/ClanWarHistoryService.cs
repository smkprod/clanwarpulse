using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;

namespace ClanWarReminder.Application.Services;

public class ClanWarHistoryService
{
    private readonly IClanWarHistoryRepository _historyRepository;
    private readonly IClashRoyaleClient _clashRoyaleClient;
    private readonly IUnitOfWork _unitOfWork;

    public ClanWarHistoryService(
        IClanWarHistoryRepository historyRepository,
        IClashRoyaleClient clashRoyaleClient,
        IUnitOfWork unitOfWork)
    {
        _historyRepository = historyRepository;
        _clashRoyaleClient = clashRoyaleClient;
        _unitOfWork = unitOfWork;
    }

    public async Task CaptureCurrentWeekAsync(
        string clanTag,
        string clanName,
        ClanWarSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = StartOfWarWeek(DateTimeOffset.UtcNow);
        await _historyRepository.UpsertWeekAsync(
            clanTag,
            clanName,
            snapshot.WarKey,
            startedAtUtc,
            startedAtUtc.AddDays(4),
            snapshot.Members,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CaptureCurrentWeekAsync(string clanTag, string clanName, CancellationToken cancellationToken)
    {
        var snapshot = await _clashRoyaleClient.GetCurrentWarAsync(clanTag, cancellationToken);
        await CaptureCurrentWeekAsync(clanTag, clanName, snapshot, cancellationToken);
    }

    public Task<IReadOnlyList<StoredPlayerWarWeek>> GetPlayerWeeksAsync(string playerTag, CancellationToken cancellationToken)
        => _historyRepository.GetPlayerWeeksAsync(playerTag, cancellationToken);

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
}
