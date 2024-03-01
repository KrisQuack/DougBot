using DougBot.Shared;
using DougBot.Discord.Notifications;
using MediatR;
using MongoDB.Bson;
using Serilog;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using System.Xml;
using Discord;
using DougBot.Twitch;

namespace DougBot.Discord.Modules
{
    public class TwitchBot_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var twitch = new TwitchBot(notification.Client);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Error in Youtube_ReadyHandler");
                    }
                    // Sleep 5 minutes
                    await Task.Delay(300000);
                }
            });
        }
    }
}
