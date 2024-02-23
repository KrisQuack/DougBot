using Discord.Interactions;
using Discord;

namespace DougBot.Discord.SlashCommands.Mod
{
    public class SetSlowmode : InteractionModuleBase
    {
        [SlashCommand("set_slowmode", "Set the slow mode for a channel")]
        [EnabledInDm(false)]
        [RequireUserPermission(GuildPermission.ModerateMembers)]
        public async Task task([Summary(description: "The channel to set the slow mode in")] ITextChannel channel, [Summary(description: "The amount of seconds to set the slow mode to")][MaxValue(21600)] int seconds)
        {
            await channel.ModifyAsync(x => x.SlowModeInterval = seconds);
            await RespondAsync($"Set slow mode to {seconds} seconds in {channel.Mention}", ephemeral: true);
        }
    }
}
