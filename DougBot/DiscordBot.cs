using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace DougBot.Discord;

public class DiscordBot
{
    private static DiscordSocketClient _client;

    private static readonly Dictionary<LogSeverity, LogEventLevel> SeverityLevelMapping = new()
    {
        { LogSeverity.Critical, LogEventLevel.Fatal },
        { LogSeverity.Error, LogEventLevel.Error },
        { LogSeverity.Warning, LogEventLevel.Warning },
        { LogSeverity.Info, LogEventLevel.Information },
        { LogSeverity.Verbose, LogEventLevel.Verbose },
        { LogSeverity.Debug, LogEventLevel.Debug }
    };

    private static ServiceProvider ConfigureServices()
    {
        return new ServiceCollection()
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DiscordBot).Assembly))
            .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = true,
                MessageCacheSize = 100,
                GatewayIntents = GatewayIntents.All & ~GatewayIntents.GuildScheduledEvents &
                                 ~GatewayIntents.GuildPresences & ~GatewayIntents.GuildInvites,
                LogLevel = LogSeverity.Info
            }))
            .AddSingleton<DiscordEventListener>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<InteractionHandler>()
            .BuildServiceProvider();
    }

    private static async Task Main()
    {
        // Run the bot
        await new DiscordBot().RunAsync();

        // Run the API in a separate thread
        //_ = Task.Run(async () =>
        //{
        //    var apiArgs = new[] { "--urls", "https://localhost:5001;http://localhost:5000" };
        //    var apiServer = new APIServer();
        //    _ = apiServer.RunAsync(apiArgs);
        //});
    }

    private async Task RunAsync()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Sink(new DelegateSink(async e =>
            {
                // Pick color based on log level
                var color = e.Level switch
                {
                    LogEventLevel.Fatal => Color.DarkRed,
                    LogEventLevel.Error => Color.Red,
                    LogEventLevel.Warning => Color.Orange,
                    LogEventLevel.Information => Color.Blue,
                    LogEventLevel.Debug => Color.DarkBlue,
                    LogEventLevel.Verbose => Color.DarkGrey,
                    _ => Color.Default
                };

                var description = e.RenderMessage();
                if (description.Length > 4096) description = description.Substring(0, 4000);

                var embed = new EmbedBuilder()
                    .WithDescription(description)
                    .WithColor(color)
                    .WithCurrentTimestamp();

                if (e.Exception != null)
                    embed.AddField(e.Exception.Message, $"```{e.Exception}```");

                var message = e.Level >= LogEventLevel.Warning ? "<@&1072596548636135435>" : "";

                var settings = await new Mongo().GetBotSettings();
                if (_client?.Guilds.FirstOrDefault() is { } guild)
                {
                    var logChannelId = settings["log_channel_id"].AsString;
                    if (ulong.TryParse(logChannelId, out var channelId) &&
                        guild.GetTextChannel(channelId) is { } logChannel)
                        await logChannel.SendMessageAsync(message, embed: embed.Build());
                }
            }))
            .CreateLogger();

        await using var services = ConfigureServices();

        _client = services.GetRequiredService<DiscordSocketClient>();
        _client.Log += LogAsync;

        var listener = services.GetRequiredService<DiscordEventListener>();
        await listener.StartAsync();

        var interactionHandler = services.GetRequiredService<InteractionHandler>();
        await interactionHandler.InitializeAsync();

        await _client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("TOKEN"));
        await _client.StartAsync();

        await Task.Delay(Timeout.Infinite);
        Environment.Exit(0);
    }

    private static async Task LogAsync(LogMessage message)
    {
        var severity = SeverityLevelMapping.TryGetValue(message.Severity, out var logEventLevel)
            ? logEventLevel
            : LogEventLevel.Information;
        Log.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
    }
}

public class DelegateSink : ILogEventSink
{
    private readonly Action<LogEvent> _emit;

    public DelegateSink(Action<LogEvent> emit)
    {
        _emit = emit ?? throw new ArgumentNullException(nameof(emit));
    }

    public void Emit(LogEvent logEvent)
    {
        _emit(logEvent);
    }
}