using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Shared;
using Serilog;

namespace DougBot.Discord.SlashCommands.Owner
{
    public class Reboot : InteractionModuleBase
    {
        [SlashCommand("reboot", "reboot the bot")]
        [EnabledInDm(false)]
        [RequireOwner]
        public async Task task()
        {
            await RespondAsync("Rebooting...", ephemeral: true);
            Environment.Exit(0);
        }
    }
}
