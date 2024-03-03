using DougBot.Discord.Notifications;
using DougBot.Twitch;
using MediatR;

namespace DougBot.Discord.Modules;

public class TwitchBotReadyHandler : INotificationHandler<ReadyNotification>
{
    public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var twitch = new TwitchBot(notification.Client);
        });
    }
}