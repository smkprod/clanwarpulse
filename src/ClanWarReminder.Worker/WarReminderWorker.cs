using ClanWarReminder.Application.Services;
using ClanWarReminder.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ClanWarReminder.Worker;

public class WarReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _options;
    private readonly ILogger<WarReminderWorker> _logger;

    public WarReminderWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        ILogger<WarReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Clamp(_options.PollMinutes, 5, 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<WarReminderService>();
                var sent = await service.RunAsync(stoppingToken);
                _logger.LogInformation("Reminder cycle completed. Sent {SentCount} reminders.", sent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder cycle failed.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
