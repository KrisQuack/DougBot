using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using MediatR;
using Serilog.Events;

namespace DougBot.Handlers
{
    public class LogNotificationHandler : INotificationHandler<LoggingNotification>
    {
        private readonly DiscordSocketClient _client;

        public LogNotificationHandler(DiscordSocketClient client)
        {
            _client = client;
        }

        public async Task Handle(LoggingNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait for the client to be ready
                    var loops = 0;
                    while (_client?.CurrentUser == null)
                    {
                        await Task.Delay(1000, cancellationToken);
                        loops++;
                        if (loops > 10) return;
                    }
                    // Pick color based on log level
                    var color = notification.LogEvent.Level switch
                    {
                        LogEventLevel.Fatal => Color.DarkRed,
                        LogEventLevel.Error => Color.Red,
                        LogEventLevel.Warning => Color.Orange,
                        LogEventLevel.Information => Color.Blue,
                        LogEventLevel.Debug => Color.DarkBlue,
                        LogEventLevel.Verbose => Color.DarkGrey,
                        _ => Color.Default
                    };

                    // Get log level name
                    var logLevel = notification.LogEvent.Level.ToString();
                    // Get the source
                    var logSource = notification.LogEvent.Properties.TryGetValue("Source", out var sourceContext)
                        ? sourceContext.ToString()
                        : "Unknown";
                    // Get message
                    var logMessage = notification.LogEvent.Properties.TryGetValue("Message", out var messageContext)
                        ? messageContext.ToString().Length > 4000 ? messageContext.ToString()[..4000] : messageContext.ToString()
                        : "None";
                    // For logMessage and logSource if it begins and ends with quotes, remove them
                    if (logSource.StartsWith("\"") && logSource.EndsWith("\""))
                        logSource = logSource[1..^1];
                    if (logMessage.StartsWith("\"") && logMessage.EndsWith("\""))
                        logMessage = logMessage[1..^1];
                    // Create embeds
                    var embeds = new List<Embed>
                    {
                        new EmbedBuilder()
                            .WithTitle($"[{logLevel}] {logSource}")
                            .WithDescription(logMessage)
                            .WithColor(color)
                            .WithCurrentTimestamp().Build()
                    };

                    if (notification.LogEvent.Exception != null)
                        embeds.Add(new EmbedBuilder()
                            .WithTitle("Exception")
                            .WithDescription($"```{notification.LogEvent.Exception}```")
                            .WithColor(color)
                            .WithCurrentTimestamp().Build());

                    var message = notification.LogEvent.Level >= LogEventLevel.Warning ? "<@&1072596548636135435>" : "";

                    var settings = await new Mongo().GetBotSettings();
                    if (_client?.Guilds.FirstOrDefault() is { } guild)
                    {
                        var logChannelId = settings["log_channel_id"].AsString;
                        if (ulong.TryParse(logChannelId, out var channelId) &&
                            guild.GetTextChannel(channelId) is { } logChannel)
                            await logChannel.SendMessageAsync(message, embeds: embeds.ToArray());
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }, cancellationToken);
        }
    }
}
