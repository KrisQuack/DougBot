using Discord;
using Discord.Interactions;
using DougBot.Discord.Functions;
using DougBot.Shared.Database;
using DougBot.Shared.OpenAI;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DougBot.Discord.SlashCommands.Mod;

public class ChatSummary(DougBotContext context) : InteractionModuleBase
{
    private readonly DougBotContext _context = context;

    [SlashCommand("chat_summary", "Generate a summary of the chat")]
    [EnabledInDm(false)]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    public async Task Task([MaxValue(100)]int take = 25)
    {
        try
        {
            await DeferAsync(ephemeral: true);
            var messages = await Context.Channel.GetMessagesAsync(limit: take).FlattenAsync();
            var chatString = MessageFunctions.MessageToString(messages);
            var summary = await new OpenAI().SummarizeChat(chatString);
            
            // Create the embed
            var responseEmbed = new EmbedBuilder()
                .WithTitle("Chat Summary")
                .WithDescription(summary)
                .WithColor(Color.Green);
            
            await FollowupAsync(embed:responseEmbed.Build(), ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"An error occurred: {ex.Message}", ephemeral: true);
            Log.Error(ex, "[{Source}]", "ChatSummary");
        }
    }
}