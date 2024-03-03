using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Shared;
using Serilog;

namespace DougBot.Discord;

public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _handler;
    private readonly IServiceProvider _services;

    public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
    {
        _client = client;
        _handler = handler;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        // Process when the client is ready, so we can register our commands.
        _client.Ready += ReadyAsync;

        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        // Process the InteractionCreated payloads to execute Interactions commands
        _client.InteractionCreated += HandleInteraction;

        // Also process the result of the command execution.
        _handler.InteractionExecuted += HandleInteractionExecute;
    }

    private async Task ReadyAsync()
    {
        // Register the commands globally.
        await _handler.RegisterCommandsGloballyAsync();
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _handler.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
                Log.Error(result.ErrorReason);
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    // implement
                    break;
            }
        }
        catch
        {
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync().ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }

    private async Task HandleInteractionExecute(ICommandInfo commandInfo, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            Log.Error($"An error occurred while executing the slash command {commandInfo.Name}: {result.ErrorReason}");
            switch (result.Error)
            {
                case InteractionCommandError.UnmetPrecondition:
                    await context.Interaction.RespondAsync("You do not have permission to execute this command.",
                        ephemeral: true);
                    break;
                case InteractionCommandError.UnknownCommand:
                    await context.Interaction.RespondAsync("The command you are trying to execute does not exist.",
                        ephemeral: true);
                    break;
                case InteractionCommandError.ParseFailed:
                    await context.Interaction.RespondAsync("The parameters you provided are not valid.",
                        ephemeral: true);
                    break;
                default:
                    await context.Interaction.RespondAsync(
                        $"An error occurred while executing the command: {result.ErrorReason}", ephemeral: true);
                    break;
            }
        }

        // Print the command result to the log channel
        var data = context.Interaction.Data as SocketSlashCommandData;
        var auditFields = new List<EmbedFieldBuilder>
        {
            new() { Name = "Command", Value = commandInfo.Name, IsInline = true },
            new() { Name = "User", Value = context.User.Mention, IsInline = true },
            new() { Name = "Channel", Value = (context.Channel as SocketTextChannel).Mention, IsInline = true },
            new()
            {
                Name = "Parameters",
                Value = data != null && data.Options.Any()
                    ? string.Join("\n", data.Options.Select(x => $"{x.Name}: {x.Value}"))
                    : "null",
                IsInline = false
            },
            new() { Name = "Error", Value = result.ErrorReason ?? "null", IsInline = false }
        };
        // If a fields value is null, it will not be added to the embed
        auditFields.RemoveAll(x => x.Value == "null");

        var embed = new EmbedBuilder()
            .WithTitle("Slash Command Executed")
            .WithFields(auditFields)
            .WithCurrentTimestamp()
            .WithColor(result.IsSuccess ? Color.Green : Color.Red);

        var settings = await new Mongo().GetBotSettings();
        var logChannelId = settings["log_channel_id"].AsString;
        var guild = _client.Guilds.FirstOrDefault();
        var logChannel = guild.GetTextChannel(ulong.Parse(logChannelId));
        await logChannel.SendMessageAsync(embed: embed.Build());
    }
}