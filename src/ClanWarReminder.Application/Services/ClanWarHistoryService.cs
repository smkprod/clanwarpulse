using ClanWarReminder.Application.Abstractions.Integrations;
using ClanWarReminder.Application.Abstractions.Persistence;
using ClanWarReminder.Application.Models;
using Microsoft.Extensions.Logging;

namespace ClanWarReminder.Application.Services;

public class ClanWarHistoryService
{
    private readonly IClanWarHistoryRepository _historyRepository;
    private readonly IClashRoyaleClient _clashRoyaleClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ClanWarHistoryService> _logger;

    public ClanWarHistoryService(
        IClanWarHistoryRepository historyRepository,
        IClashRoyaleClient clashRoyaleClient,
        IUnitOfWork unitOfWork,
        ILogger<ClanWarHistoryService> logger)
    {
        _historyRepository = historyRepository;
        _clashRoyaleClient = clashRoyaleClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task CaptureCurrentWeekAsync(
        string clanTag,
        string clanName,
        ClanWarSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex) when (IsHistoryWriteConflict(ex))
        {
            _unitOfWork.ClearChanges();
            _logger.LogWarning(ex, "Skipped clan war history write because the same war snapshot for {ClanTag} was written concurrently.", clanTag);
        }
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

    private static bool IsHistoryWriteConflict(Exception ex)
    {
        var typeName = ex.GetType().Name;
        return string.Equals(typeName, "DbUpdateConcurrencyException", StringComparison.Ordinal) ||
               string.Equals(typeName, "DbUpdateException", StringComparison.Ordinal);
    }
}
