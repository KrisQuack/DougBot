using Discord;
using MediatR;

namespace DougBot.Discord.Notifications;

public class MessageDeletedNotification : INotification
{
    public MessageDeletedNotification(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        Message = message;
        Channel = channel;
    }

    public Cacheable<IMessage, ulong> Message { get; }
    public Cacheable<IMessageChannel, ulong> Channel { get; }
}