using Discord;
using DougBot.Discord.Notifications;
using DougBot.Shared.Database;
using MediatR;
using Serilog;

namespace DougBot.Modules;

public class WatchdogReadyHandler : INotificationHandler<ReadyNotification>
{
    public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    // Get logs from the database where status is error and the time is within the last 2 minutes
                    using var db = new DougBotContext();
                    var logs = db.Serilogs
                        .Where(x => x.Level == "Error" && x.RaiseDate > DateTime.UtcNow.AddMinutes(-2))
                        .ToList();
                    // If there are any, DM the owner
                    if (logs.Any())
                    {
                        var owner = notification.Client.GetApplicationInfoAsync().Result.Owner;
                        await owner.SendMessageAsync($"There are {logs.Count} errors in the logs");
                        foreach (var log in logs)
                        {
                            var errorEmbed = new EmbedBuilder
                            {
                                Description = $"{log.Message}\n```{log.Exception}```",
                                Color = Color.Red
                            };
                            await owner.SendMessageAsync(embed: errorEmbed.Build());
                        }
                        
                        // If there are more than 5 then reboot the bot
                        if (logs.Count > 5)
                        {
                            Environment.Exit(0);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error in Watchdog_ReadyHandler");
                }

                // Sleep for 60 seconds
                await Task.Delay(60000);
            }
        });
    }
}