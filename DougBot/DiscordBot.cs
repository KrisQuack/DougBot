using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Handlers;
using MediatR;
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
            .AddSingleton<DiscordEventHandler>()
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
        await using var services = ConfigureServices();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Sink(new DelegateSink(services.GetRequiredService<IMediator>()))
            .CreateLogger();

        _client = services.GetRequiredService<DiscordSocketClient>();
        _client.Log += LogAsync;

        var listener = services.GetRequiredService<DiscordEventHandler>();
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
    private readonly IMediator _mediator;

    public DelegateSink(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public void Emit(LogEvent logEvent)
    {
        _mediator.Publish(new LoggingNotification(logEvent));
    }
}
