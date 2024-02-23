using Discord.Interactions;
using DougBot.Shared;

namespace DougBot.Discord.SlashCommands.Everyone
{
    public class mc_redeem : InteractionModuleBase
    {
        private static readonly Random random = new Random();

        [SlashCommand("mc_redeem", "Get a Twitch redemption code for Minecraft")]
        [EnabledInDm(false)]
        public async Task task()
        {
            var member = await new Mongo().GetMember(Context.User.Id);
            if (member != null)
            {
                var random_part = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 5).Select(s => s[random.Next(s.Length)]).ToArray());
                var code = $"DMC-{random_part}";
                member["mc_redeem"] = code;
                await new Mongo().UpdateMember(member);
                await RespondAsync($"Your Minecraft redemption code is: **{code}**\nUse this in the Twitch redemption box", ephemeral: true);
            }
        }
    }
}
