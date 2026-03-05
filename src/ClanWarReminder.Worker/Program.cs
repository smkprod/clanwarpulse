using ClanWarReminder.Application;
using ClanWarReminder.Infrastructure;
using ClanWarReminder.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<WarReminderWorker>();
builder.Services.AddHostedService<TelegramCommandWorker>();

var host = builder.Build();
host.Run();
