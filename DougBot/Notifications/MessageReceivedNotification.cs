using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications;

public class MessageReceivedNotification : INotification
{
    public MessageReceivedNotification(SocketMessage message, DiscordSocketClient client)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public SocketMessage Message { get; }
    public DiscordSocketClient Client { get; }
}