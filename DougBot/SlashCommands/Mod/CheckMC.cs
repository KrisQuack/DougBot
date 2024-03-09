using Discord;
using Discord.Interactions;
using DougBot.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Discord.SlashCommands.Mod;

public class ChecMc(DougBotContext context) : InteractionModuleBase
{
    private readonly DougBotContext _context = context;

    [SlashCommand("check_mc", "Check who owns an MC code")]
    [EnabledInDm(false)]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    public async Task Task([Summary(description: "The redeemed code")] string code)
    {
        var member = await _context.Members.FirstOrDefaultAsync(x => x.McRedeem == code);
        await RespondAsync(
            member != null ? $"The code is owned by <@{member.Id}> ({member.Id})." : "The code is not owned.",
            ephemeral: true);
    }
}