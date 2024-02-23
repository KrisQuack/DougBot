using Discord.WebSocket;
using MediatR;

namespace DougBot.Discord.Notifications;

public class ReadyNotification : INotification
{
    public ReadyNotification(DiscordSocketClient client)
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public DiscordSocketClient Client { get; }
}