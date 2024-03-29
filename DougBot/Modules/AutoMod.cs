﻿using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using DougBot.Discord.Notifications;
using DougBot.Shared.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Discord.Modules;

public class AutoModMessageReceived : INotificationHandler<MessageReceivedNotification>
{
    public async Task Handle(MessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var message = notification.Message;
            // Check this is not a DM
            if (message.Channel is IDMChannel) return;

            // Features that a bot or mod can trigger
            // AutoPublish
            await AutoPublish(message);

            // Features that a user can trigger
            var guildUser = message.Author as SocketGuildUser;
            if (message.Author.IsBot || guildUser.GuildPermissions.ModerateMembers) return;

            // AttachmentsAutomod
            await AttachmentsAutomod(message);
        });
    }

    private async Task AttachmentsAutomod(SocketMessage message)
    {
        if (message.Attachments.Count > 0)
        {
            // Check if attachment name matches regex
            var regex = new Regex(@"\.(zip|rar|7z|tar|gz|iso|dmg|exe|msi|apk)$", RegexOptions.IgnoreCase);
            foreach (var attachment in message.Attachments)
                if (regex.IsMatch(attachment.Filename))
                {
                    // declare database context
                    await using var db = new DougBotContext();
                    // Send a message to the user
                    await message.Channel.SendMessageAsync(
                        "Please do not upload zip files or executables, the mod team has no way to verify these are not malicious without investing significant time to investigate each upload.");
                    await message.DeleteAsync();
                    // Create an embed to send to the mod channel
                    var embed = new EmbedBuilder()
                        .WithTitle("Attachment Deleted")
                        .WithAuthor($"{message.Author.Username} ({message.Author.Id})", message.Author.GetAvatarUrl())
                        .WithDescription(
                            $"A prohibited attachment has been detected and removed.\n**{attachment.Filename}**")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();
                    var settings = await db.Botsettings.FirstOrDefaultAsync();
                    var guildchannel = (SocketGuild)message.Channel;
                    var modChannel =
                        (IMessageChannel)guildchannel.GetChannel(Convert.ToUInt64(settings.ModChannelId));
                    await modChannel.SendMessageAsync(embed: embed);
                }
        }
    }

    public async Task AutoPublish(SocketMessage message)
    {
        if (message.Channel is INewsChannel)
        {
            var msg = message as IUserMessage;
            await msg.CrosspostAsync();
        }
    }
}

public class AutoModThreadCreated : INotificationHandler<ThreadCreatedNotification>
{
    public async Task Handle(ThreadCreatedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            var thread = notification.Thread;
            if (thread.ParentChannel is not IForumChannel) return;
            await Task.Delay(1000);
            var messages = await thread.GetMessagesAsync(5).FlattenAsync();
            if (messages.OrderBy(m => m.CreatedAt).FirstOrDefault() is IUserMessage msg && !msg.IsPinned)
            {
                await msg.PinAsync();
                var embed = new EmbedBuilder()
                    .WithTitle("Welcome to Your Thread!")
                    .WithDescription(
                        "Server rules apply. Issues? Contact [mod team](https://discord.com/channels/567141138021089308/880127379119415306/1154847821514670160).\n" +
                        $"{thread.Owner.Mention} You can Pin/Unpin posts by right clicking a message and going to **Apps**")
                    .WithThumbnailUrl(
                        "https://cdn.discordapp.com/attachments/886548334154760242/1135511848817545236/image.png")
                    .WithColor(Color.Orange)
                    .WithAuthor(thread.Name, thread.Guild.IconUrl)
                    .Build();
                await thread.SendMessageAsync(embed: embed);
            }
        });
    }
}