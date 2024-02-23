using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DougBot.Discord.SlashCommands.Everyone
{
    public class Ping : InteractionModuleBase
    {
        [SlashCommand("ping", "Pong!")]
        [EnabledInDm(false)]
        public async Task task()
        {
            // Get the latency of the bot
            var socketClient = Context.Client as DiscordSocketClient;
            var latency = socketClient.Latency;

            // measure http latency
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            await RespondAsync($"Pong!");
            sw.Stop();

            // Create the embed for the response
            var embed = new EmbedBuilder()
                .WithDescription($"🏓 Pong! {latency}ms\n🌐 HTTP: {sw.ElapsedMilliseconds}ms")
                .WithColor(Color.Orange)
                .Build();

            // Modify the response to include the embed
            await ModifyOriginalResponseAsync(x => x.Embeds = new[] { embed });
        }
    }
}
