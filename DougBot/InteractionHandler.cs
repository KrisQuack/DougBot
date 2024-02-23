using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;
using System.Reflection;

namespace DougBot.Discord
{
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
                        default:
                            break;
                    }
            }
            catch
            {
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }

        private async Task HandleInteractionExecute(ICommandInfo commandInfo, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
                Log.Error(result.ErrorReason);
                switch (result.Error)
                {
                    case InteractionCommandError.UnmetPrecondition:
                        // implement
                        break;
                    default:
                        break;
                }
        }
    }
}
