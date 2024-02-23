using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using DougBot.Discord.Notifications;
using MediatR;
using System.Text.RegularExpressions;

namespace DougBot.Discord.Modules
{
    public class AutoMod_MessageReceived : INotificationHandler<MessageReceivedNotification>
    {
        public async Task Handle(MessageReceivedNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                var message = notification.Message;
                // Check this is not a DM
                if(message.Channel is IDMChannel) { return; }

                // Features that a bot or mod can trigger
                // AutoPublish
                await AutoPublish(message);

                // Features that a user can trigger
                var GuildUser = message.Author as SocketGuildUser;
                if(message.Author.IsBot || GuildUser.GuildPermissions.ModerateMembers) { return; }

                // AttachmentsAutomod
                await AttachmentsAutomod(message);
            });
            
        }

        private async Task AttachmentsAutomod(SocketMessage message)
        {
            if(message.Attachments.Count > 0)
            {
                // Check if attachment name matches regex
                var regex = new Regex(@"\.(zip|rar|7z|tar|gz|iso|dmg|exe|msi|apk)$", RegexOptions.IgnoreCase);
                foreach(var attachment in message.Attachments)
                {
                    if(regex.IsMatch(attachment.Filename))
                    {
                        await message.Channel.SendMessageAsync($"Please do not upload zip files or executables, the mod team has no way to verify these are not malicious without investing significant time to investigate each upload.");
                        await message.DeleteAsync();
                        // Create an embed to send to the mod channel
                        var embed = new EmbedBuilder()
                            .WithTitle("Attachment Deleted")
                            .WithAuthor($"{message.Author.Username} ({message.Author.Id})", message.Author.GetAvatarUrl())
                            .WithDescription($"A prohibited attachment has been detected and removed.\n**{attachment.Filename}**")
                            .WithColor(Color.Red)
                            .WithCurrentTimestamp()
                            .Build();
                        var settings = await new Mongo().GetBotSettings();
                        var guildchannel = (SocketGuild)message.Channel;
                        var modChannel = (IMessageChannel)guildchannel.GetChannel(Convert.ToUInt64(settings["mod_channel_id"]));
                        await modChannel.SendMessageAsync(embed: embed);
                    }
                }
            }
        }

        public async Task AutoPublish(SocketMessage message)
        {
            if(message.Channel is INewsChannel)
            {
                var msg = message as IUserMessage;
                await msg.CrosspostAsync();
            }
        }
    }

    public class AutoMod_ThreadCreated : INotificationHandler<ThreadCreatedNotification>
    {
        public async Task Handle(ThreadCreatedNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                var thread = notification.Thread;
                if (thread.ParentChannel is not IForumChannel)
                {
                    return;
                }
                await Task.Delay(1000);
                var messages = await thread.GetMessagesAsync(5).FlattenAsync();
                if (messages.OrderBy(m => m.CreatedAt).FirstOrDefault() is IUserMessage msg)
                {
                    await msg.PinAsync();
                    var embed = new EmbedBuilder()
                        .WithTitle("Welcome to Your Thread!")
                        .WithDescription("Server rules apply. Issues? Contact [mod team](https://discord.com/channels/567141138021089308/880127379119415306/1154847821514670160).\n" +
                            $"{thread.Owner.Mention}: You can Pin/Unpin posts. [How?](https://cdn.discordapp.com/attachments/886548334154760242/1135511848817545236/image.png)")
                        .WithColor(Color.Orange)
                        .WithAuthor(thread.Name, thread.Guild.IconUrl)
                        .Build();
                    await thread.SendMessageAsync(embed: embed);
                }
            });
            
        }
    }
}
