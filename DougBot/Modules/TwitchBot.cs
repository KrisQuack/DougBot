using DougBot.Discord.Notifications;
using MediatR;
using DougBot.Twitch;

namespace DougBot.Discord.Modules
{
    public class TwitchBot_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                var twitch = new TwitchBot(notification.Client);
            });
        }
    }
}
