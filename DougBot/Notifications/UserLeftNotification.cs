using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications
{
    public class UserLeftNotification : INotification
    {
        public UserLeftNotification(SocketGuild guild, SocketUser user)
        {
            Guild = guild ?? throw new ArgumentNullException(nameof(guild));
            User = user ?? throw new ArgumentNullException(nameof(user));
        }

        public SocketGuild Guild { get; }
        public SocketUser User { get; }
    }
}
