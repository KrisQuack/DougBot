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
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DougBot.Twitch;

internal class EventSub
{
    private const string WebsocketBaseUrl = "wss://eventsub.wss.twitch.tv/ws";
    private const ulong McRoleId = 698681714616303646;
    private const ulong PesosRoleId = 954017881652342786;
    private const ulong McChannelId = 698679698699583529;
    private User _botUser;
    private DiscordSocketClient _client;
    private User _dougUser;
    private DateTime _lastMessage = DateTime.UtcNow;
    private Botsetting _settings;

    private TwitchAPI _twitchApi;
    private ClientWebSocket? _websocketClient;
    private string? _websocketSessionId;

    public async Task ConnectAsync(DiscordSocketClient client, TwitchAPI twitchApi)
    {
        _client = client;
        _twitchApi = twitchApi;
        await using var db = new DougBotContext();
        _settings = await db.Botsettings.FirstOrDefaultAsync();
        // Get the bot and doug user
        var botUser = await _twitchApi.Helix.Users.GetUsersAsync(logins: [_settings.TwitchBotName]);
        var dougUser = await _twitchApi.Helix.Users.GetUsersAsync(logins: [_settings.TwitchChannelName]);
        _botUser = botUser.Users[0];
        _dougUser = dougUser.Users[0];
        // Listen to the websocket
        await ListenToWebsocket();
        // Connect to the websocket
        while (true)
        {
            // Unsubscribe from all events
            var subscriptions = await _twitchApi.Helix.EventSub.GetEventSubSubscriptionsAsync();
            foreach (var sub in subscriptions.Subscriptions)
                await _twitchApi.Helix.EventSub.DeleteEventSubSubscriptionAsync(sub.Id);
            // Make sure the websocket is closed
            if (_websocketClient != null && _websocketClient.State == WebSocketState.Open)
                await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                    CancellationToken.None);
            // Connect to the websocket and print the response
            _websocketClient = new ClientWebSocket();
            await _websocketClient.ConnectAsync(new Uri(WebsocketBaseUrl), CancellationToken.None);
            // Wait for the websocket session id
            while (string.IsNullOrEmpty(_websocketSessionId)) await Task.Delay(1000);
            // Subscribe to websocket events
            var channelUpdateSub = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.update", "2",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", _dougUser.Id }
                },
                EventSubTransportMethod.Websocket,
                _websocketSessionId
            );
            var streamOnlineSub = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.online", "1",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", _dougUser.Id }
                },
                EventSubTransportMethod.Websocket,
                _websocketSessionId
            );
            var streamOfflineSub = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "stream.offline", "1",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", _dougUser.Id }
                },
                EventSubTransportMethod.Websocket,
                _websocketSessionId
            );
            var channelChatMessageSub = await _twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                "channel.chat.message", "1",
                new Dictionary<string, string>
                {
                    { "broadcaster_user_id", _dougUser.Id },
                    { "user_id", _botUser.Id }
                },
                EventSubTransportMethod.Websocket,
                _websocketSessionId
            );
            // Ensure the health of the websocket
            var lastStatus = DateTime.UtcNow.AddMinutes(-10);
            while (_websocketClient.State == WebSocketState.Open)
            {
                // Check if the keepalive is older than 1 minute
                if (_lastMessage.AddMinutes(1) < DateTime.UtcNow)
                {
                    await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Keepalive",
                        CancellationToken.None);
                    Log.Warning("[{Source}] {Message}", "EventSub",
                        $"Websocket closed due to keepalive timeout: {_lastMessage:hh:mm:ss}");
                    break;
                }

                // If the last status is older than 30 minutes, print the status
                if (lastStatus.AddMinutes(30) < DateTime.UtcNow)
                {
                    subscriptions = await _twitchApi.Helix.EventSub.GetEventSubSubscriptionsAsync();
                    // Check if any are not "enabled"
                    if (subscriptions.Subscriptions.Any(sub => sub.Status != "enabled"))
                    {
                        Log.Warning("[{Source}] {Message}", "EventSub",
                            $"Subscriptions:\n{string.Join(Environment.NewLine, subscriptions.Subscriptions.Select(sub => $"{sub.Type} - {sub.Status}"))}");
                        break;
                    }

                    Log.Information("[{Source}] {Message}", "EventSub",
                        $"Subscriptions:\n{string.Join(Environment.NewLine, subscriptions.Subscriptions.Select(sub => $"{sub.Type} - {sub.Status}"))}");
                    lastStatus = DateTime.UtcNow;
                }

                // Wait 10 seconds
                await Task.Delay(10000);
            }

            Log.Warning("[{Source}] {Message}", "EventSub", $"EventSub reconnecting: {_websocketClient.State}");
            _websocketSessionId = null;
            _lastMessage = DateTime.UtcNow;
            // Wait 10 seconds before reconnecting
            await Task.Delay(10000);
        }
    }

    public void UpdateAccessToken(string newToken)
    {
        _twitchApi.Settings.AccessToken = newToken;
    }

    private async Task ListenToWebsocket()
    {
        _ = Task.Run(async () =>
        {
            var buffer = new byte[16384];
            while (true)
                try
                {
                    // Check if the websocket is open
                    if (_websocketClient == null || _websocketClient.State != WebSocketState.Open)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    var result =
                        await _websocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    _ = ProcessNotification(buffer, result);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Source}]", "ListenToWebsocket");
                }
        });
    }

    private async Task ProcessNotification(byte[] buffer, WebSocketReceiveResult result)
    {
        try
        {
            // Check if there is any data to read
            if (result.Count == 0) return;
            // Try to parse the json
            var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(response);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{Source}] {Message}", "EventSub", $"Error parsing JSON: {response}");
                return;
            }

            // Handle welcome message
            if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() == "session_welcome")
            {
                var welcomeMessage = JsonSerializer.Deserialize<WebsocketWelcome.Root>(response);
                _websocketSessionId = welcomeMessage.payload.session.id;
                Log.Information("[{Source}] {Message}", "EventSub",
                    $"Twitch websocket session id: {_websocketSessionId}");
            }
            // Handle keepalive
            else if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() ==
                     "session_keepalive")
            {
                _lastMessage = DateTime.UtcNow;
            }
            // If there is the subscription type, get it
            else if (json.RootElement.GetProperty("metadata")
                     .TryGetProperty("subscription_type", out var subscriptionType))
            {
                _lastMessage = DateTime.UtcNow;
                switch (subscriptionType.GetString())
                {
                    case "channel.update":
                        var channelUpdate = JsonSerializer.Deserialize<WebsocketChannelUpdate.Root>(response);
                        await ChannelUpdateHandler(channelUpdate);
                        break;
                    case "stream.online":
                        var streamOnline = JsonSerializer.Deserialize<WebsocketStreamOnline.Root>(response);
                        await ChannelSteamOnline(streamOnline);
                        break;
                    case "stream.offline":
                        var streamOffline = JsonSerializer.Deserialize<WebsocketStreamOffline.Root>(response);
                        await ChannelStreamOffline(streamOffline);
                        break;
                    case "channel.chat.message":
                        var chatMessage = JsonSerializer.Deserialize<WebsocketChannelChatMessage.Root>(response);
                        await ChannelChatMessage(chatMessage);
                        break;
                    default:
                        Log.Warning("[{Source}] {Message}", "EventSub", $"Invalid Subscription: {json.RootElement}");
                        break;
                }
            }
            else
            {
                Log.Warning("[{Source}] {Message}", "EventSub",
                    $"ListenToWebsocket Invalid Request: {json.RootElement}");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}]", "ProcessNotification");
        }
    }

    private async Task ChannelChatMessage(WebsocketChannelChatMessage.Root? chatMessage)
    {
        try
        {
            if (chatMessage.payload.Event.message.text.Contains("DMC-") &&
                chatMessage.payload.Event.channel_points_custom_reward_id.ToString() ==
                "a5b9d1c7-44f9-4964-b0f7-42c39cb04f98")
            {
                // Get the user
                var db = new DougBotContext();
                var dbUser = await db.Members.FirstOrDefaultAsync(x => x.McRedeem == chatMessage.payload.Event.message.text);
                if (dbUser != null)
                {
                    var guild = _client.Guilds.FirstOrDefault();
                    var discordUser = guild.GetUser(Convert.ToUInt64(dbUser.Id));
                    if (discordUser != null)
                    {
                        // Add the roles
                        var mcRole = guild.GetRole(McRoleId);
                        var pesosRole = guild.GetRole(PesosRoleId);
                        await discordUser.AddRoleAsync(mcRole);
                        await discordUser.AddRoleAsync(pesosRole);
                        // Send a message
                        var mcChannel = guild.GetTextChannel(McChannelId);
                        await mcChannel.SendMessageAsync(
                            $"{discordUser.Mention} Redemption successful, Please link your minecraft account using the instructions in <#743938486888824923>");

                        var modChannel = guild.GetTextChannel(Convert.ToUInt64(_settings.TwitchModChannelId));
                        await modChannel.SendMessageAsync(
                            $"Minecraft redemption successful, Please approve in the redemption queue\nTwitch: **{chatMessage.payload.Event.chatter_user_login}**\nDiscord:{discordUser.Mention}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Error on chatMessage");
            Log.Error(e, "[{Source}]", "chatMessage");
        }
    }

    private async Task ChannelStreamOffline(WebsocketStreamOffline.Root? streamOffline)
    {
        try
        {
            // Get the events from the guild
            var events = await _client.Guilds.FirstOrDefault().GetEventsAsync();
            // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
            var activeEvents = events.Where(e =>
                e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") &&
                e.CreatorId == _client.CurrentUser.Id);
            // Close the events
            foreach (var e in activeEvents) await e.EndAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Source}]", "streamOffline");
        }
    }

    private async Task ChannelSteamOnline(WebsocketStreamOnline.Root? streamOnline)
    {
        try
        {
            // get stream info
            var streams = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> { _dougUser.Id });
            var stream = streams.Streams.FirstOrDefault();
            // Create a new event
            var banner = new Image("Data/LiveBanner.png");
            var streamEvent = await _client.Guilds.FirstOrDefault().CreateEventAsync(stream.Title,
                DateTimeOffset.UtcNow.AddMinutes(10), GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private,
                stream.GameName, DateTime.UtcNow.AddDays(1), location: "https://twitch.tv/dougdoug",
                coverImage: banner);
            // Start the event
            await streamEvent.StartAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Source}]", "streamOnline");
        }
    }

    private async Task ChannelUpdateHandler(WebsocketChannelUpdate.Root? channelUpdate)
    {
        try
        {
            // Get the events from the guild
            var events = await _client.Guilds.FirstOrDefault().GetEventsAsync();
            // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
            var activeEvents = events.Where(e =>
                e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") &&
                e.CreatorId == _client.CurrentUser.Id);
            // Update the events
            foreach (var e in activeEvents)
                await e.ModifyAsync(x =>
                {
                    x.Name = channelUpdate.payload.Event.title;
                    x.Description = channelUpdate.payload.Event.category_name;
                });
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Source}]", "channelUpdate");
        }
    }
}