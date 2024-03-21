using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DougBot.Shared.Database;
using DougBot.Twitch.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DougBot.Twitch;

public class PubSub
{
    private const string WebsocketBaseUrl = "wss://pubsub-edge.twitch.tv";
    private DiscordSocketClient _client;
    private User _dougUser;
    private Botsetting _settings;
    private ITextChannel? _gambleChannel;
    private IUserMessage? _lastGambleMessage;
    private DateTime _lastGamblePost = DateTime.UtcNow;

    private TwitchAPI _twitchApi;
    private ClientWebSocket? _websocketClient;
    private DateTime _lastMessage = DateTime.UtcNow;

    public async Task ConnectAsync(DiscordSocketClient client, TwitchAPI twitchApi)
    {
        _client = client;
        _twitchApi = twitchApi;
        await using var db = new DougBotContext();
        _settings = await db.Botsettings.FirstOrDefaultAsync();
        // Get the most recent gamble post if there is one
        _gambleChannel = _client.GetChannel(Convert.ToUInt64(_settings.TwitchGamblingChannelId)) as ITextChannel;
        var pins = await _gambleChannel.GetPinnedMessagesAsync();
        var pin = pins.FirstOrDefault(x => x.Author.Id == _client.CurrentUser.Id);
        if (pin != null)
        {
            _lastGambleMessage = pin as IUserMessage;
        }
        // Get the bot and doug user
        var dougUser = await _twitchApi.Helix.Users.GetUsersAsync(logins: [_settings.TwitchChannelName]);
        _dougUser = dougUser.Users[0];
        // Listen to the websocket
        await ListenToWebsocket();
        // Connect to the websocket
        while (true)
        {
            // Make sure the websocket is closed
            if (_websocketClient != null && _websocketClient.State == WebSocketState.Open)
                await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                    CancellationToken.None);
            // Connect to the websocket and print the response
            _websocketClient = new ClientWebSocket();
            await _websocketClient.ConnectAsync(new Uri(WebsocketBaseUrl), CancellationToken.None);
            // Subscribe to websocket events
            await _websocketClient.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                type = "LISTEN",
                data = new
                {
                    topics = new[] {$"predictions-channel-v1.{_dougUser.Id}"},
                    auth_token = _twitchApi.Settings.AccessToken
                }
            })), WebSocketMessageType.Text, true, CancellationToken.None);
            // Ensure the health of the websocket
            var lastStatus = DateTime.UtcNow.AddHours(-1);
            while (_websocketClient.State == WebSocketState.Open)
            {
                // If the last message was more than 5 minutes ago, reconnect
                if ((DateTime.UtcNow - _lastMessage).TotalSeconds > 300)
                {
                    await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "pong",
                        CancellationToken.None);
                    Log.Warning("[{Source}] {Message}", "PubSub",
                        $"Websocket closed due to no pong: {_lastMessage:hh:mm:ss}");
                    break;
                }
                // Send a ping
                await _websocketClient.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    type = "PING"
                })), WebSocketMessageType.Text, true, CancellationToken.None);
                // If the last status is older than 30 minutes, print the status
                if (lastStatus.AddMinutes(30) < DateTime.UtcNow)
                {
                    Log.Information("[{Source}] {Message}", "PubSub",
                        $"Websocket status: {_websocketClient.State}\nLast message: {_lastMessage:hh:mm:ss}");
                    lastStatus = DateTime.UtcNow;
                }
                // Wait 30 seconds
                await Task.Delay(30000);
            }
            Log.Warning("[{Source}] {Message}", "PubSub", $"PubSub reconnecting: {_websocketClient.State}");
            // Wait 10 seconds before reconnecting
            await Task.Delay(10000);
            _lastMessage = DateTime.UtcNow;
        }
    }
    
    private async Task ListenToWebsocket()
    {
        _ = Task.Run(async () =>
        {
            var buffer = new byte[16384];
            var message = new StringBuilder();

            while (true)
            {
                try
                {
                    // Check if the websocket is open
                    if (_websocketClient == null || _websocketClient.State != WebSocketState.Open)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    var result = await _websocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        _ = ProcessNotification(message.ToString());
                        message.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Source}]", "PubSub ListenToWebsocket");
                }
            }
        });
    }
    
    private async Task ProcessNotification(string jsonMessage)
    {
        try
        {
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(jsonMessage);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{Source}] {Message}", "PubSub", $"Error parsing JSON: {jsonMessage}");
                return;
            }
            
            
            // Process the notification
            var type = json.RootElement.GetProperty("type").GetString();
            switch (type)
            {
                case "PONG":
                    _lastMessage = DateTime.UtcNow;
                    break;
                case "AUTH_REVOKED":
                    Log.Warning("[{Source}] {Message}", "PubSub", "Auth Revoked");
                    await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Auth Revoked",
                        CancellationToken.None);
                    break;
                case "RESPONSE":
                {
                    var error = json.RootElement.GetProperty("type").GetString();
                    if (string.IsNullOrEmpty(error))
                    {
                        Log.Warning( "[{Source}] {Message}", "PubSub", $"Error from response: {error}");
                    }

                    break;
                }
                // If there is the subscription type, get it
                case "MESSAGE":
                {
                    var topic = json.RootElement.GetProperty("data").GetProperty("topic");
                    if (topic.GetString().Contains("predictions-channel-v1"))
                    {
                        var message = json.RootElement.GetProperty("data").GetProperty("message").GetString();
                        var prediction = JsonSerializer.Deserialize<WebsocketPrediction.Root>(message);
                        await predictionHandler(prediction);
                    }
                    else
                    {
                        Log.Warning("[{Source}] {Message}", "PubSub", $"Invalid Subscription: {json.RootElement}");
                    }

                    break;
                }
                default:
                    Log.Warning("[{Source}] {Message}", "PubSub",
                        $"ListenToWebsocket Invalid Request: {json.RootElement}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}]", "ProcessNotification");
        }
    }

    private async Task predictionHandler(WebsocketPrediction.Root prediction)
    {
        try
        {
            // Get end datetime
            var endTime = new DateTimeOffset(DateTime.Parse(prediction.data.Event.created_at).AddSeconds(prediction.data.Event.prediction_window_seconds));
            var unixTime = endTime.ToUnixTimeSeconds();
            var endTimeString = $"<t:{unixTime}:R>";
            // Create an embed
            var embed = new EmbedBuilder
            {
                Title = prediction.data.Event.title,
                Description = $"Ends {endTimeString}",
                Color = Color.Orange
            };
            embed.WithFooter("This feature of the bot is still in development and is not guaranteed to work. Any issues will likely not be fixed until next stream");
            // Dictionary for colors
            var colors = new Dictionary<string, string>
            {
                {"BLUE", "🟦"},
                {"PINK", "🟪"},
            };
            // If the prediction is created
            if (prediction.type == "event-updated")
            {
                var totalPoints = prediction.data.Event.outcomes.Sum(outcome => outcome.total_points);
                var totalUsers = prediction.data.Event.outcomes.Sum(outcome => outcome.total_users);
                var isResolved = prediction.data.Event.status == "RESOLVED";
                foreach (var outcome in prediction.data.Event.outcomes)
                {
                    var ratio = Math.Round((double)totalPoints /outcome.total_points, 2);
                    var userPercentage = (double) outcome.total_users / totalUsers * 100;
                    var pointsPercentage = (double) outcome.total_points / totalPoints * 100;
                    var isWinner = outcome.id == prediction.data.Event.winning_outcome_id?.ToString();
                    var prefix = isWinner ? "🏆" : "";
                    var topPredictors = outcome.top_predictors.OrderByDescending(p => p.points).Aggregate("", (current, predictor) =>
                    {
                        var profit = isWinner ? predictor.points * ratio : 0;
                        if(isResolved)
                            return current + $"{predictor.user_display_name}: {predictor.points} -> {profit}\n";
                        return current + $"{predictor.user_display_name}: {predictor.points}\n";
                    });
                    embed.AddField($"{prefix}{colors[outcome.color]} {outcome.title}", $"""
                         Points: {outcome.total_points} ({pointsPercentage:0.00}%)
                         Users: {outcome.total_users} ({userPercentage:0.00}%)
                         Ratio: {ratio}
                         
                         **__High Rollers__**
                         {topPredictors}
                         """);
                }
            }
            
            if (_lastGambleMessage != null)
            {
                // If the gamble is not locked or resolved or canceled, update only every 10 seconds
                var isStatusValid = prediction.data.Event.status != "RESOLVED" && prediction.data.Event.status != "LOCKED" && prediction.data.Event.status != "CANCELED";
                var isWithinUpdateInterval = (DateTime.UtcNow - _lastGamblePost).TotalSeconds >= 10;
                if (isStatusValid && isWithinUpdateInterval)
                {
                    return;
                }

                await _lastGambleMessage.ModifyAsync(properties => properties.Embed = embed.Build());
                _lastGamblePost = DateTime.UtcNow;
            }
            else
            {
                _lastGambleMessage = await _gambleChannel.SendMessageAsync("<@&1080237787174948936>", embed: embed.Build());
                await _lastGambleMessage.PinAsync();
                _lastGamblePost = DateTime.UtcNow;
            }
            
            switch (prediction.data.Event.status)
            {
                case "RESOLVED":
                    await _gambleChannel.SendMessageAsync($"The gamble has been resolved {_lastGambleMessage.GetJumpUrl()}");
                    await _lastGambleMessage.UnpinAsync();
                    _lastGambleMessage = null;
                    break;
                case "CANCELED":
                    await _lastGambleMessage.DeleteAsync();
                    _lastGambleMessage = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}]", "predictionHandler");
        }
    }

    public void UpdateAccessToken(string refreshAccessToken)
    {
        _twitchApi.Settings.AccessToken = refreshAccessToken;
    }
}