using Discord.Interactions;

namespace DougBot.Discord.SlashCommands.Owner;

public class Reboot : InteractionModuleBase
{
    [SlashCommand("reboot", "reboot the bot")]
    [EnabledInDm(false)]
    [RequireOwner]
    public async Task Task()
    {
        await RespondAsync("Rebooting...", ephemeral: true);
        Environment.Exit(0);
    }
}