using Discord.Interactions;
using Discord;
using DougBot.Shared;

namespace DougBot.Discord.SlashCommands.Mod
{
    public class ChecMC : InteractionModuleBase
    {
        [SlashCommand("check_mc", "Check who owns an MC code")]
        [EnabledInDm(false)]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task task([Summary(description: "The redeemed code")] string code)
        {
            var member = await new Mongo().GetMemberByMCRedeem(code);
            await RespondAsync(member != null ? $"The code is owned by <@{member["_id"]}> ({member["_id"]})." : "The code is not owned.", ephemeral: true);
        }
    }
}
