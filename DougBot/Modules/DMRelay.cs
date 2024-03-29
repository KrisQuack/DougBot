﻿using Discord;
using Discord.Interactions;
using DougBot.Discord.Notifications;
using DougBot.Shared.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DougBot.Discord.Modules;

public class DmRelayMessageReceived : INotificationHandler<MessageReceivedNotification>
{
    public async Task Handle(MessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            if (notification.Message.Channel is IDMChannel && !notification.Message.Author.IsBot)
            {
                // Create an embed to send to the mod channel
                var embeds = new List<Embed>();
                var embed = new EmbedBuilder()
                    .WithAuthor($"{notification.Message.Author.Username} ({notification.Message.Author.Id})",
                        notification.Message.Author.GetAvatarUrl())
                    .WithDescription(notification.Message.Content)
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .Build();
                embeds.Add(embed);
                // Check for attachments
                if (notification.Message.Attachments.Count > 0)
                    foreach (var attachment in notification.Message.Attachments)
                    {
                        var attachmentEmbed = new EmbedBuilder()
                            .WithAuthor($"{notification.Message.Author.Username} ({notification.Message.Author.Id})",
                                notification.Message.Author.GetAvatarUrl())
                            .WithDescription($"**{attachment.Filename}**")
                            .WithImageUrl(attachment.Url)
                            .WithUrl(attachment.Url)
                            .WithColor(Color.Blue)
                            .WithCurrentTimestamp()
                            .Build();
                        embeds.Add(attachmentEmbed);
                    }

                // Create component to view history
                var viewHistoryButton = new ButtonBuilder()
                    .WithLabel("View History")
                    .WithCustomId($"dmhistory:{notification.Message.Author.Id}")
                    .WithStyle(ButtonStyle.Primary);
                var components = new ComponentBuilder()
                    .WithButton(viewHistoryButton)
                    .Build();
                // Send the embed to the mod channel
                await using var db = new DougBotContext();
                var settings = await db.Botsettings.FirstOrDefaultAsync();
                var guild = notification.Client.Guilds.FirstOrDefault();
                var modChannel = (IMessageChannel)guild.GetChannel(Convert.ToUInt64(settings.DmReceiptChannelId));
                await modChannel.SendMessageAsync(embeds: embeds.ToArray(), components: components);
            }
        });
    }
}

public class DmHistory : InteractionModuleBase
{
    [ComponentInteraction("dmhistory:*", true)]
    public async Task Dmhistory(string author)
    {
        try
        {
            var user = await Context.Guild.GetUserAsync(Convert.ToUInt64(author));
            if (user == null) throw new Exception("User not found");
            var dmChannel = await user.CreateDMChannelAsync();
            if (dmChannel == null) throw new Exception("DM Channel not found");

            var messages = await dmChannel.GetMessagesAsync(20).FlattenAsync();
            messages = messages.OrderBy(m => m.CreatedAt);
            // Create the string to send
            var messageString = "";
            foreach (var message in messages)
            {
                messageString += $"\n{message.Author.Username}: {message.Content}";
                if (message.Embeds.Any())
                    foreach (var embed in message.Embeds)
                        messageString += $" {embed.Description}";
            }

            // Create an embed to reply with
            var responseEmbed = new EmbedBuilder()
                .WithDescription(messageString)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .Build();
            // Send the message
            await RespondAsync(embed: responseEmbed, ephemeral: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}]", "DMHistory");
        }
    }
}