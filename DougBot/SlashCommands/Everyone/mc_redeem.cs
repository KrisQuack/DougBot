using Discord.Interactions;
using DougBot.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Discord.SlashCommands.Everyone;

public class McRedeem(DougBotContext context) : InteractionModuleBase
{
    private static readonly Random Random = new();
    private readonly DougBotContext _context = context;

    [SlashCommand("mc_redeem", "Get a Twitch redemption code for Minecraft")]
    [EnabledInDm(false)]
    public async Task Task()
    {
        var member = await _context.Members.FirstOrDefaultAsync(x => x.Id == Context.User.Id);
        if (member != null)
        {
            var randomPart = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 5)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
            var code = $"DMC-{randomPart}";
            member.McRedeem = code;
            await _context.SaveChangesAsync();
            await RespondAsync($"Your Minecraft redemption code is: **{code}**\nUse this in the Twitch redemption box",
                ephemeral: true);
        }
    }
}