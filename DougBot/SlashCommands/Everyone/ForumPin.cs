using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DougBot.Discord.SlashCommands.Everyone;

[Group("forum", "Commands for runnng a Forum thread")]
[EnabledInDm(false)]
public class ForumCmd : InteractionModuleBase
{
    [MessageCommand("Forum: Pin")]
    public async Task PinMessage(IMessage message)
    {
        //Is it a thread
        var threadChannel = message.Channel as SocketThreadChannel;
        if (threadChannel != null)
        {
            //Get the first pinned message based on creation time
            var pinnedMessages = await threadChannel.GetPinnedMessagesAsync();
            var firstPinnedMessage = pinnedMessages.OrderBy(x => x.CreatedAt).FirstOrDefault();
            //Is it the owner
            if (firstPinnedMessage != null && firstPinnedMessage.Author.Id == Context.User.Id)
            {
                //Pin the message
                await (message as SocketUserMessage).PinAsync();
                await RespondAsync("Message pinned", ephemeral: true);
                return;
            }
        }

        await RespondAsync("This command can only be used in threads you own", ephemeral: true);
    }

    [MessageCommand("Forum: Unpin")]
    public async Task UnpinMessage(IMessage message)
    {
        //Is it a thread
        var threadChannel = message.Channel as SocketThreadChannel;
        if (threadChannel != null)
        {
            //Get the first pinned message based on creation time
            var pinnedMessages = await threadChannel.GetPinnedMessagesAsync();
            var firstPinnedMessage = pinnedMessages.OrderBy(x => x.CreatedAt).FirstOrDefault();
            //Is it the owner
            if (firstPinnedMessage != null && firstPinnedMessage.Author.Id == Context.User.Id &&
                message.Id != firstPinnedMessage.Id)
            {
                //Pin the message
                await (message as SocketUserMessage).UnpinAsync();
                await RespondAsync("Message unpinned", ephemeral: true);
                return;
            }
        }

        await RespondAsync("This command can only be used in threads you own and not on the first message",
            ephemeral: true);
    }
}