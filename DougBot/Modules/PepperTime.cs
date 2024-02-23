using DougBot.Discord.Notifications;
using MediatR;
using Serilog;
using System;
using TimeZoneConverter;

namespace DougBot.Modules
{
    public class PepperTime_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                while(true)
                {
                    try
                    {
                        // Get current doug time
                        var tz = TZConvert.GetTimeZoneInfo("America/Los_Angeles");
                        var dougTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
                        // If it is a 10th minute, update the pepper time
                        if (dougTime.Minute % 10 == 0)
                        {
                            var guild = notification.Client.Guilds.FirstOrDefault();
                            var channel = guild?.GetCategoryChannel(567147619122544641);
                            await channel.ModifyAsync(properties =>
                            {
                                properties.Name = $"PEPPER TIME: {dougTime:hh:mm tt}";
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error in PepperTime_ReadyHandler");
                    }
                    // Sleep for 60 seconds
                    await Task.Delay(60000);
                }
            });
        }
    }
}
