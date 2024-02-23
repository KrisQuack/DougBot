using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications;

public class UserJoinedNotification : INotification
{
    public UserJoinedNotification(SocketGuildUser user)
    {
        User = user ?? throw new ArgumentNullException(nameof(user));
    }

    public SocketGuildUser User { get; }
}