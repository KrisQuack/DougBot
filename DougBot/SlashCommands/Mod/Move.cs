using Discord;
using Discord.Interactions;
using Discord.Webhook;
using Discord.WebSocket;
using Embed = Discord.Embed;

namespace DougBot.Discord.SlashCommands.Mod
{
    public class Move : InteractionModuleBase
    {
        private const string WebhookName = "Wahaha";

        [SlashCommand("move", "Move the message to a new channel")]
        [EnabledInDm(false)]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task task([Summary(description: "The ID of the message")] string message, [Summary(description: "The channel to move it to")] IChannel channel)
        {
            // Send a response to the user indicating the message is being moved
            await RespondAsync("Moving the message", ephemeral: true);

            // Get the thread channel and its ID and parent channel if it exists
            var threadChannel = channel as SocketThreadChannel;
            var threadChannelId = threadChannel?.Id;
            var parentChannel = threadChannel?.ParentChannel;
            // Get the text channel or forum channel if it exists
            var textChannel = parentChannel as ITextChannel ?? channel as ITextChannel;
            var forumChannel = parentChannel as IForumChannel ?? channel as IForumChannel;

            // Get the message to move
            var messageToMove = await Context.Channel.GetMessageAsync(Convert.ToUInt64(message));
            if (messageToMove is null)
            {
                // If the message doesn't exist, modify the response to indicate this
                await ModifyOriginalResponseAsync(x => x.Content = "Message not found");
                return;
            }

            // Get or create the webhook for the channel
            IWebhook wahWebhook;
            if (textChannel != null)
            {
                wahWebhook = await GetOrCreateWebhook(textChannel);
            }
            else if (forumChannel != null)
            {
                wahWebhook = await GetOrCreateWebhook(forumChannel);
            }
            else
            {
                // If the channel type is invalid, modify the response to indicate this
                await ModifyOriginalResponseAsync(x =>
                    x.Content = "Invalid channel type. Only text channels, forum channels and threads are supported.");
                return;
            }

            // Create a new webhook client
            var webhook = new DiscordWebhookClient(wahWebhook.Id, wahWebhook.Token);
            // Get the author of the message
            var authorObj = await Context.Guild.GetUserAsync(messageToMove.Author.Id);
            var authorName = authorObj.Nickname ?? authorObj.GlobalName;
            // Get the embeds from the message
            var embedList = messageToMove.Embeds.Select(e => e as Embed).ToList();

            // If the message has attachments, send them with the message
            if (messageToMove.Attachments.Count > 0)
            {
                var files = new List<FileAttachment>();
                foreach (var attachment in messageToMove.Attachments)
                {
                    using var httpClient = new HttpClient();
                    var attachmentBytes = await httpClient.GetByteArrayAsync(attachment.Url);
                    var fileStream = new MemoryStream(attachmentBytes);
                    files.Add(new FileAttachment(fileStream, attachment.Filename));
                }

                await webhook.SendFilesAsync(files, messageToMove.Content, embeds: embedList,
                    username: authorName, avatarUrl: messageToMove.Author.GetAvatarUrl(),
                    allowedMentions: AllowedMentions.None, threadId: threadChannelId);

                foreach (var file in files)
                {
                    file.Dispose();
                }
            }
            else
            {
                // If the message doesn't have attachments, just send the message
                await webhook.SendMessageAsync(messageToMove.Content, embeds: embedList,
                    username: authorName, avatarUrl: messageToMove.Author.GetAvatarUrl(),
                    allowedMentions: AllowedMentions.None, threadId: threadChannelId);
            }

            // Delete the original message
            await messageToMove.DeleteAsync();
            // Modify the original response to indicate the message has been moved
            await ModifyOriginalResponseAsync(x => x.Content = "Message moved");
            // Send a reply to the user indicating the message has been moved
            await ReplyAsync($"{messageToMove.Author.Mention} your message has been moved to <#{channel.Id}>");
        }

        // This method gets or creates a webhook for a text channel
        private async Task<IWebhook> GetOrCreateWebhook(ITextChannel channel)
        {
            var webhooks = await channel.GetWebhooksAsync();
            return webhooks.FirstOrDefault(x => x.Name == WebhookName) ?? await channel.CreateWebhookAsync(WebhookName);
        }

        // This method gets or creates a webhook for a forum channel
        private async Task<IWebhook> GetOrCreateWebhook(IForumChannel channel)
        {
            var webhooks = await channel.GetWebhooksAsync();
            return webhooks.FirstOrDefault(x => x.Name == WebhookName) ?? await channel.CreateWebhookAsync(WebhookName);
        }
    }
}
