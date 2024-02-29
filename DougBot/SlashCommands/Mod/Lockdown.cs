using Discord;
using Discord.Interactions;
using DougBot.Shared;
using System.Data;

namespace DougBot.Discord.SlashCommands.Mod
{
    public class Lockdown : InteractionModuleBase
    {
        [SlashCommand("lockdown", "Resrict a channel to stricter automod rules")]
        [EnabledInDm(false)]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task task([Summary(description: "The channel to lockdown")] ITextChannel channel)
        {
            // Check if the channels permissions are synced
            var category = await channel.GetCategoryAsync();
            if (category != null && channel.PermissionOverwrites.SequenceEqual(category.PermissionOverwrites))
            {
                await RespondAsync("Channels permissions are not synced, cannot be locked down", ephemeral: true);
                return;
            }
            // Defer the response as this command will take a while
            await DeferAsync(ephemeral: true);
            // Get the mod channel
            var settings = await new Mongo().GetBotSettings();
            var modChannel = await Context.Guild.GetTextChannelAsync(Convert.ToUInt64(settings["mod_channel_id"]));
            // Check if a message is already pinned with the channel id
            var pinnedMessages = await Context.Channel.GetPinnedMessagesAsync();
            var pinnedMessage = pinnedMessages
                .Where(x => x.Embeds != null && x.Embeds.FirstOrDefault()?.Footer?.Text != null)
                .FirstOrDefault(x => x.Embeds.FirstOrDefault().Footer.Value.Text.Contains(channel.Id.ToString()));
            if (pinnedMessage != null)
            {
                // If a message is already pinned, modify the response to indicate this
                await RespondAsync("This channel is already in lockdown", ephemeral: true);
                return;
            }
            // Create the embed for the lockdown message
            var embed = new EmbedBuilder()
                .WithTitle($"Lockdown: {channel.Name}")
                .WithDescription("""
                This menu is now persistent, You can revisit it at any time by clicking it in the pins. You can toggle the buttons any time to change access. Once you are done with the menu you can click Restore to sync the channels permissions and remove the menu.

                **Strict Automod**: Applies discords over the top automod rules to the channel along side out own list of blocked emotes and words (including letters, numbers and commonly used combinations of offensive emotes)
                **Slowmode**: Sets the channel to a 30 second slowmode (Removes all slow mods when toggled off)
                **Ext. Emotes**: Removes the ability to use emotes from other servers
                **Ext. Stickers**: Removes the ability to use stickers from other servers
                **Reactions**: Removes the ability to react to messages
                **Embeds**: Removes the ability to embed links in chat
                **Attachments**: Removes the ability to send attachments in chat
                **Restore**: Restores the channel to its original permissions and removes the menu
                """)
                .WithColor(Color.DarkPurple)
                .WithAuthor($"{Context.User.Username} ({Context.User.Id})", Context.User.GetAvatarUrl())
                .WithFooter(channel.Id.ToString())
                .WithTimestamp(DateTime.UtcNow)
                .Build();
            // Add the buttons to the embed
            var actionRow1 = new ActionRowBuilder()
                .WithButton("Strict AutoMod Inactive", "automod", ButtonStyle.Danger)
                .WithButton("Slowmode(30s) Inactive", "slowmode", ButtonStyle.Danger);
            var actionRow2 = new ActionRowBuilder()
                .WithButton("Ext. Emotes Allowed", "emotes", ButtonStyle.Success)
                .WithButton("Ext. Stickers Allowed", "stickers", ButtonStyle.Success)
                .WithButton("Reactions Allowed", "reactions", ButtonStyle.Success);
            var actionRow3 = new ActionRowBuilder()
                .WithButton("Embeds Allowed", "embeds", ButtonStyle.Success)
                .WithButton("Attachments Allowed", "attachments", ButtonStyle.Success);
            var actionRow4 = new ActionRowBuilder()
                .WithButton("Sync permissions and restore channel", "restore", ButtonStyle.Primary);
            // Pin the embed to the channel
            var actionRows = new List<ActionRowBuilder> { actionRow1, actionRow2, actionRow3, actionRow4 };
            var components = new ComponentBuilder().WithRows(actionRows).Build();
            var message = await modChannel.SendMessageAsync(embed: embed, components: components);
            await message.PinAsync();
            // Respond to the user
            await FollowupAsync($"Channel locked down generated: {message.GetJumpUrl()}", ephemeral: true);
        }

        [ComponentInteraction("automod", true)]
        public async Task automod()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetChannelAsync(ulong.Parse(channel_id));
            // Get automod rules
            var rule1 = await Context.Guild.GetAutoModRuleAsync(1194682146124738570);
            var rule2 = await Context.Guild.GetAutoModRuleAsync(1194684593119445134);
            // Toggle the button
            if (rule1.ExemptChannels.Contains(channel.Id))
            {
                // Remove the channel from the exceptions
                var new_exceptions = rule1.ExemptChannels.ToList();
                if (new_exceptions.Contains(channel.Id))
                {
                    new_exceptions.Remove(channel.Id);
                }
                await rule1.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
                await rule2.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
            }
            else
            {
                // Add the channel back to the exceptions
                var new_exceptions = rule1.ExemptChannels.ToList();
                if (!new_exceptions.Contains(channel.Id))
                {
                    new_exceptions.Add(channel.Id);
                }
                await rule1.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
                await rule2.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
            }
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("AutoMod settings updated", ephemeral: true);
        }

        [ComponentInteraction("slowmode", true)]
        public async Task slowmode()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            // Toggle the button
            if (channel.SlowModeInterval == 30)
            {
                // Remove the slowmode
                await channel.ModifyAsync(x => x.SlowModeInterval = 0);
            }
            else
            {
                // Add the slowmode
                await channel.ModifyAsync(x => x.SlowModeInterval = 30);
            }
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("Slowmode settings updated", ephemeral: true);
        }

        [ComponentInteraction("emotes", true)]
        public async Task emotes()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
            // Toggle the button
            if (permissions.Value.UseExternalEmojis == PermValue.Allow)
            {
                // Remove the permission
                permissions = permissions.Value.Modify(useExternalEmojis: PermValue.Deny);
            }
            else
            {
                // Add the permission
                permissions = permissions.Value.Modify(useExternalEmojis: PermValue.Allow);
            }
            // Update the channel permissions
            await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, permissions.Value);
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("External Emotes settings updated", ephemeral: true);
        }

        [ComponentInteraction("stickers", true)]
        public async Task stickers()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
            // Toggle the button
            if (permissions.Value.UseExternalStickers == PermValue.Allow)
            {
                // Remove the permission
                permissions = permissions.Value.Modify(useExternalStickers: PermValue.Deny);
            }
            else
            {
                // Add the permission
                permissions = permissions.Value.Modify(useExternalStickers: PermValue.Allow);
            }
            // Update the channel permissions
            await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, permissions.Value);
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("External Stickers settings updated", ephemeral: true);
        }

        [ComponentInteraction("reactions", true)]
        public async Task reactions()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
            // Toggle the button
            if (permissions.Value.AddReactions == PermValue.Allow)
            {
                // Remove the permission
                permissions = permissions.Value.Modify(addReactions: PermValue.Deny);
            }
            else
            {
                // Add the permission
                permissions = permissions.Value.Modify(addReactions: PermValue.Allow);
            }
            // Update the channel permissions
            await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, permissions.Value);
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("Reactions settings updated", ephemeral: true);
        }

        [ComponentInteraction("embeds", true)]
        public async Task embeds()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
            // Toggle the button
            if (permissions.Value.EmbedLinks == PermValue.Allow)
            {
                // Remove the permission
                permissions = permissions.Value.Modify(embedLinks: PermValue.Deny);
            }
            else
            {
                // Add the permission
                permissions = permissions.Value.Modify(embedLinks: PermValue.Allow);
            }
            // Update the channel permissions
            await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, permissions.Value);
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("Embeds settings updated", ephemeral: true);
        }

        [ComponentInteraction("attachments", true)]
        public async Task attachments()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
            // Toggle the button
            if (permissions.Value.AttachFiles == PermValue.Allow)
            {
                // Remove the permission
                permissions = permissions.Value.Modify(attachFiles: PermValue.Deny);
            }
            else
            {
                // Add the permission
                permissions = permissions.Value.Modify(attachFiles: PermValue.Allow);
            }
            // Update the channel permissions
            await channel.AddPermissionOverwriteAsync(channel.Guild.EveryoneRole, permissions.Value);
            // Edit the message
            await reload_buttons(response);
            await RespondAsync("Attachments settings updated", ephemeral: true);
        }

        [ComponentInteraction("restore", true)]
        public async Task restore()
        {
            // Get the embed from the message
            var response = ((IComponentInteraction)Context.Interaction).Message;
            var channel_id = response.Embeds.FirstOrDefault().Footer.Value.Text;
            var channel = await Context.Guild.GetTextChannelAsync(ulong.Parse(channel_id));
            // Get the message
            var message = await Context.Channel.GetPinnedMessagesAsync();
            var message_id = message.FirstOrDefault(x => x.Embeds.FirstOrDefault().Footer.Value.Text == channel_id).Id;
            var message_to_delete = await Context.Channel.GetMessageAsync(message_id);
            // Restore the automod
            var rule1 = await Context.Guild.GetAutoModRuleAsync(1194682146124738570);
            var rule2 = await Context.Guild.GetAutoModRuleAsync(1194684593119445134);
            var new_exceptions = rule1.ExemptChannels.ToList();
            if (!new_exceptions.Contains(channel.Id))
            {
                new_exceptions.Add(channel.Id);
            }
            await rule1.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
            await rule2.ModifyAsync(x => x.ExemptChannels = new_exceptions.ToArray());
            // Reset the permissions
            await channel.SyncPermissionsAsync();
            // Delete the message
            await message_to_delete.DeleteAsync();
            // Edit the message
            await RespondAsync("Channel permissions restored", ephemeral: true);
        }

        public async Task reload_buttons(IUserMessage message)
        {
            // Get the message
            var embed = message.Embeds.FirstOrDefault();
            var channel_id = embed.Footer.Value.Text;
            var command_channel = (ITextChannel)message.Channel;
            var guild = command_channel.Guild;
            var channel = await guild.GetTextChannelAsync(ulong.Parse(channel_id));
            var permissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);

            // Check if automod is active
            var rule1 = await guild.GetAutoModRuleAsync(1194682146124738570);

            // Recreate the buttons based on the current state of the channel
            var actionRow1 = new ActionRowBuilder()
                .WithButton(!rule1.ExemptChannels.Contains(channel.Id) ? "Strict AutoMod Active" : "Strict AutoMod Inactive", "automod", !rule1.ExemptChannels.Contains(channel.Id) ? ButtonStyle.Success : ButtonStyle.Danger)
                .WithButton(channel.SlowModeInterval == 30 ? "Slowmode(30s) Active" : "Slowmode(30s) Inactive", "slowmode", channel.SlowModeInterval == 30 ? ButtonStyle.Success : ButtonStyle.Danger);
            var actionRow2 = new ActionRowBuilder()
                .WithButton(permissions.Value.UseExternalEmojis == PermValue.Allow ? "Ext. Emotes Allowed" : "Ext. Emotes Blocked", "emotes", permissions.Value.UseExternalEmojis == PermValue.Allow ? ButtonStyle.Success : ButtonStyle.Danger)
                .WithButton(permissions.Value.UseExternalStickers == PermValue.Allow ? "Ext. Stickers Allowed" : "Ext. Stickers Blocked", "stickers", permissions.Value.UseExternalStickers == PermValue.Allow ? ButtonStyle.Success : ButtonStyle.Danger)
                .WithButton(permissions.Value.AddReactions == PermValue.Allow ? "Reactions Allowed" : "Reactions Blocked", "reactions", permissions.Value.AddReactions == PermValue.Allow ? ButtonStyle.Success : ButtonStyle.Danger);
            var actionRow3 = new ActionRowBuilder()
                .WithButton(permissions.Value.EmbedLinks == PermValue.Allow ? "Embeds Allowed" : "Embeds Blocked", "embeds", permissions.Value.EmbedLinks == PermValue.Allow ? ButtonStyle.Success : ButtonStyle.Danger)
                .WithButton(permissions.Value.AttachFiles == PermValue.Allow ? "Attachments Allowed" : "Attachments Blocked", "attachments", permissions.Value.AttachFiles == PermValue.Allow ? ButtonStyle.Success : ButtonStyle.Danger);
            var actionRow4 = new ActionRowBuilder()
                .WithButton("Sync permissions and restore channel", "restore", ButtonStyle.Primary);
            var actionRows = new List<ActionRowBuilder> { actionRow1, actionRow2, actionRow3, actionRow4 };
            var components = new ComponentBuilder().WithRows(actionRows).Build();
            // Modify the message with the new buttons
            await message.ModifyAsync(x => x.Components = components);
        }
    }
}
