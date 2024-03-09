using Discord;
using Discord.Interactions;
using DougBot.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Discord.SlashCommands.Mod;

public class SendDm(DougBotContext context) : InteractionModuleBase
{
    private readonly DougBotContext _context = context;

    [SlashCommand("send_dm", "Send a DM to a user")]
    [EnabledInDm(false)]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    public async Task Task([Summary(description: "The user to send the DM to")] IGuildUser user,
        [Summary(description: "The message to send to the user")]
        string message)
    {
        // Create the embed for the user
        var userEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(Color.Orange)
            .WithAuthor($"{Context.Guild.Name} Mods", Context.Guild.IconUrl)
            .WithFooter("Any replies to this DM will be sent to the mod team")
            .WithTimestamp(DateTime.UtcNow)
            .Build();

        // Send the DM
        await user.SendMessageAsync(embed: userEmbed);

        // Create the receipt embed for the mod team
        var modEmbed = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(Color.Orange)
            .WithAuthor($"DM to {user.Username} ({user.Id}) from {Context.User.Username}", Context.User.GetAvatarUrl())
            .WithTimestamp(DateTime.UtcNow)
            .Build();

        // Get the channels to send the receipt to
        var settings = await _context.Botsettings.FirstOrDefaultAsync();
        var modChannel = await Context.Guild.GetTextChannelAsync(Convert.ToUInt64(settings.DmReceiptChannelId));
        await modChannel.SendMessageAsync(embed: modEmbed);

        await RespondAsync("DM sent!", ephemeral: true);
    }
}