using Discord;
using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications;

public class MessageUpdatedNotification : INotification
{
    public MessageUpdatedNotification(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage,
        ISocketMessageChannel channel)
    {
        OldMessage = oldMessage;
        NewMessage = newMessage ?? throw new ArgumentNullException(nameof(newMessage));
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    public Cacheable<IMessage, ulong> OldMessage { get; }
    public SocketMessage NewMessage { get; }
    public ISocketMessageChannel Channel { get; }
}