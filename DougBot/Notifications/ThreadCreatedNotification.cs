using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications
{
    public class ThreadCreatedNotification : INotification
    {
        public ThreadCreatedNotification(SocketThreadChannel thread)
        {
            Thread = thread ?? throw new ArgumentNullException(nameof(thread));
        }

        public SocketThreadChannel Thread { get; }
    }
}
