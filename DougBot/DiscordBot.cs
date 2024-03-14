using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Handlers;
using DougBot.Shared.Database;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NpgsqlTypes;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;

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
            .AddDbContext<DougBotContext>()
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
        
        IDictionary<string, ColumnWriterBase> columnWriters = new Dictionary<string, ColumnWriterBase>
        {
            { "message", new RenderedMessageColumnWriter(NpgsqlDbType.Text) },
            { "message_template", new MessageTemplateColumnWriter(NpgsqlDbType.Text) },
            { "level", new LevelColumnWriter(true, NpgsqlDbType.Varchar) },
            { "raise_date", new TimestampColumnWriter(NpgsqlDbType.TimestampTz) },
            { "exception", new ExceptionColumnWriter(NpgsqlDbType.Text) },
            { "properties", new PropertiesColumnWriter(NpgsqlDbType.Jsonb) },
            { "machine_name", new SinglePropertyColumnWriter("MachineName", PropertyWriteMethod.ToString, NpgsqlDbType.Text, "l") }
        };
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.PostgreSQL(Environment.GetEnvironmentVariable("CONNECTION_STRING"), "serilog", columnWriters, needAutoCreateTable: true)
            .CreateLogger();

        _client = services.GetRequiredService<DiscordSocketClient>();
        _client.Log += LogAsync;

        var listener = services.GetRequiredService<DiscordEventHandler>();
        await listener.StartAsync();

        var interactionHandler = services.GetRequiredService<InteractionHandler>();
        await interactionHandler.InitializeAsync();

        // Get the token
        await using var db = new DougBotContext();
        var token = db.Botsettings.FirstOrDefault().DiscordToken;

        await _client.LoginAsync(TokenType.Bot, token);
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