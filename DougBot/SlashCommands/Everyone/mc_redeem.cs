using Discord.Interactions;
using DougBot.Shared;

namespace DougBot.Discord.SlashCommands.Everyone;

public class McRedeem : InteractionModuleBase
{
    private static readonly Random Random = new();

    [SlashCommand("mc_redeem", "Get a Twitch redemption code for Minecraft")]
    [EnabledInDm(false)]
    public async Task Task()
    {
        var member = await new Mongo().GetMember(Context.User.Id);
        if (member != null)
        {
            var randomPart = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 5)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
            var code = $"DMC-{randomPart}";
            member["mc_redeem"] = code;
            await new Mongo().UpdateMember(member);
            await RespondAsync($"Your Minecraft redemption code is: **{code}**\nUse this in the Twitch redemption box",
                ephemeral: true);
        }
    }
}