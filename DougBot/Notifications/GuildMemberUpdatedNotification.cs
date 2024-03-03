using Discord;
using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications;

public class GuildMemberUpdatedNotification : INotification
{
    public GuildMemberUpdatedNotification(Cacheable<SocketGuildUser, ulong> oldUser, SocketGuildUser newUser)
    {
        OldUser = oldUser;
        NewUser = newUser ?? throw new ArgumentNullException(nameof(newUser));
    }

    public Cacheable<SocketGuildUser, ulong> OldUser { get; }
    public SocketGuildUser NewUser { get; }
}