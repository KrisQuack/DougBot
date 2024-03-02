using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using DougBot.Twitch.Models;
using MongoDB.Bson;
using Serilog;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DougBot.Twitch
{
    internal class EventSub
    {
        private const string WEBSOCKET_BASE_URL = "wss://eventsub.wss.twitch.tv/ws";
        private const ulong MC_ROLE_ID = 698681714616303646;
        private const ulong PESOS_ROLE_ID = 954017881652342786;
        private const ulong MC_CHANNEL_ID = 698679698699583529;

        private TwitchAPI _twitchAPI;
        private ClientWebSocket _websocketClient;
        private string _websocketSessionId;
        private DateTime _lastKeepalive = DateTime.UtcNow;
        private DiscordSocketClient _client;
        private BsonDocument _settings;
        private User _botUser;
        private User _dougUser;
        
        public async Task ConnectAsync (DiscordSocketClient client, TwitchAPI twitchAPI)
        {
            _client = client;
            _twitchAPI = twitchAPI;
            _settings = await new Mongo().GetBotSettings();
            // Get the bot and doug user
            var botUser = await _twitchAPI.Helix.Users.GetUsersAsync(logins: new List<string> { _settings["twitch_bot_name"].AsString });
            var dougUser = await _twitchAPI.Helix.Users.GetUsersAsync(logins: new List<string> { _settings["twitch_channel_name"].AsString });
            _botUser = botUser.Users[0];
            _dougUser = dougUser.Users[0];
            // Listen to the websocket
            await ListenToWebsocket();
            // Connect to the websocket
            while (true)
            {
                // Unsubscribe from all events
                var subscriptions = await _twitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync();
                foreach (var sub in subscriptions.Subscriptions)
                {
                    await _twitchAPI.Helix.EventSub.DeleteEventSubSubscriptionAsync(sub.Id);
                }
                // Make sure the websocket is closed
                if (_websocketClient != null && _websocketClient.State == WebSocketState.Open)
                {
                    await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                // Connect to the websocket and print the response
                _websocketClient = new ClientWebSocket();
                await _websocketClient.ConnectAsync(new Uri(WEBSOCKET_BASE_URL), CancellationToken.None);
                // Wait for the websocket session id
                while (string.IsNullOrEmpty(_websocketSessionId)) { await Task.Delay(1000); }
                // Subscribe to websocket events
                var channel_update_sub = await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.update", "2",
                    new Dictionary<string, string>
                    {
                            {"broadcaster_user_id", _dougUser.Id},
                    },
                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                    _websocketSessionId
                );
                var stream_online_sub = await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "stream.online", "1",
                    new Dictionary<string, string>
                    {
                            {"broadcaster_user_id", _dougUser.Id},
                    },
                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                    _websocketSessionId
                );
                var stream_offline_sub = await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "stream.offline", "1",
                    new Dictionary<string, string>
                    {
                            {"broadcaster_user_id", _dougUser.Id},
                    },
                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                    _websocketSessionId
                );
                var channel_chat_message_sub = await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.message", "1",
                    new Dictionary<string, string>
                    {
                            {"broadcaster_user_id", _dougUser.Id},
                            {"user_id", _botUser.Id}
                    },
                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                    _websocketSessionId
                );
                // Ensure the health of the websocket
                var lastStatus = DateTime.UtcNow.AddMinutes(-10);
                while (_websocketClient.State == WebSocketState.Open)
                {
                    // Check if the keepalive is older than 1 minutes
                    if (_lastKeepalive.AddMinutes(1) < DateTime.UtcNow)
                    {
                        await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Keepalive", CancellationToken.None);
                        Log.Warning($"Websocket closed due to keepalive timeout: {_lastKeepalive.ToString("hh:mm:ss")}");
                        break;
                    }
                    // If the last status is older than 5 minutes, print the status
                    if (lastStatus.AddMinutes(5) < DateTime.UtcNow)
                    {
                        subscriptions = await _twitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync();
                        // Check if any are not "enabled"
                        if (subscriptions.Subscriptions.Any(sub => sub.Status != "enabled"))
                        {
                            Log.Warning($"Some subscriptions are not enabled:\n{string.Join(Environment.NewLine, subscriptions.Subscriptions.Select(sub => $"{sub.Type} - {sub.Status}"))}");
                            break;
                        }
                        Log.Information($"Subscriptions:\n{string.Join(Environment.NewLine, subscriptions.Subscriptions.Select(sub => $"{sub.Type} - {sub.Status}"))}");
                        lastStatus = DateTime.UtcNow;
                    }
                    // Wait 10 seconds
                    await Task.Delay(10000);
                }
                Log.Warning($"EventSub reconnecting: {_websocketClient.State}");
                // Wait 10 seconds before reconnecting
                await Task.Delay(10000);
            }
        }
        public void UpdateAccessToken(string newToken)
        {
            _twitchAPI.Settings.AccessToken = newToken;
        }

        private async Task ListenToWebsocket()
        {
            _ = Task.Run(async () =>
            {
                var buffer = new byte[8192];
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
                        _ = ProcessNotification(buffer, result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in ListenToWebsocket");
                    }
                }
            });
        }

        private async Task ProcessNotification(byte[] buffer, WebSocketReceiveResult result)
        {
            try
            {
                // Check if there is any data to read
                if (result.Count == 0)
                {
                    return;
                }
                // Try to parse the json
                var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                JsonDocument json;
                try
                {
                    json = JsonDocument.Parse(response);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, $"Error parsing JSON: {response}");
                    return;
                }
                // Handle welcome message
                if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() == "session_welcome")
                {
                    var welcomeMessage = JsonSerializer.Deserialize<WebsocketWelcome.Root>(response);
                    _websocketSessionId = welcomeMessage.payload.session.id;
                    Log.Information($"Twitch websocket session id: {_websocketSessionId}");
                }
                // Handle keepalive
                else if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() == "session_keepalive")
                {
                    _lastKeepalive = DateTime.UtcNow;
                }
                // If there is the subscription type, get it
                else if (json.RootElement.GetProperty("metadata").TryGetProperty("subscription_type", out var subscriptionType))
                {
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
                            Log.Warning($"Invalid Subscription: {json.RootElement}");
                            break;
                    }
                }
                else
                {
                    Log.Warning($"ListenToWebsocket Invalid Request: {json.RootElement}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in ProcessNotification");
            }
        }

        private async Task ChannelChatMessage(WebsocketChannelChatMessage.Root? chatMessage)
        {
            try
            {
                if (chatMessage.payload.Event.message.text.Contains("DMC-") && chatMessage.payload.Event.channel_points_custom_reward_id.ToString() == "a5b9d1c7-44f9-4964-b0f7-42c39cb04f98")
                {
                    // Get the user
                    var dbUser = await new Mongo().GetMemberByMCRedeem(chatMessage.payload.Event.message.text);
                    if (dbUser != null)
                    {
                        var guild = _client.Guilds.FirstOrDefault();
                        var discordUser = guild.GetUser(Convert.ToUInt64(dbUser["_id"]));
                        if (discordUser != null)
                        {
                            // Add the roles
                            var mcRole = guild.GetRole(MC_ROLE_ID);
                            var pesosRole = guild.GetRole(PESOS_ROLE_ID);
                            await discordUser.AddRoleAsync(mcRole);
                            await discordUser.AddRoleAsync(pesosRole);
                            // Send a message
                            var mcChannel = guild.GetTextChannel(MC_CHANNEL_ID);
                            await mcChannel.SendMessageAsync($"{discordUser.Mention} Redemption succesful, Please link your minecraft account using the instructions in <#743938486888824923>");

                            var modChannel = guild.GetTextChannel(Convert.ToUInt64(_settings["twitch_mod_channel_id"]));
                            await modChannel.SendMessageAsync($"Minecraft redemption succesful, Please approve in the redemption queue\nTwitch: **{chatMessage.payload.Event.chatter_user_login}**\nDiscord:{discordUser.Mention}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on chatMessage");
            }
        }

        private async Task ChannelStreamOffline(WebsocketStreamOffline.Root? streamOffline)
        {
            try
            {
                // Get the events from the guild
                var events = await _client.Guilds.FirstOrDefault().GetEventsAsync();
                // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
                var active_events = events.Where(e => e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") && e.CreatorId == _client.CurrentUser.Id);
                // Close the events
                foreach (var e in active_events)
                {
                    await e.EndAsync();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on streamOffline");
            }
        }

        private async Task ChannelSteamOnline(WebsocketStreamOnline.Root? streamOnline)
        {
            try
            {
                // get stream info
                var streams = await _twitchAPI.Helix.Streams.GetStreamsAsync(userIds: new List<string> { _dougUser.Id });
                var stream = streams.Streams.FirstOrDefault();
                // Get the stream thumbnail from url to Image
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(stream.ThumbnailUrl.Replace("{width}", "1920").Replace("{height}", "1080"));
                var stream_thumbnail = await response.Content.ReadAsByteArrayAsync();
                using var memStream = new MemoryStream(stream_thumbnail);
                // Create a new event
                var stream_event = await _client.Guilds.FirstOrDefault().CreateEventAsync(stream.Title, DateTimeOffset.UtcNow, GuildScheduledEventType.External, GuildScheduledEventPrivacyLevel.Private, stream.GameName, DateTime.UtcNow.AddDays(1), location: "https://twitch.tv/dougdoug", coverImage: new Image(memStream));
                // Start the event
                await stream_event.StartAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on streamOnline");
            }
        }

        private async Task ChannelUpdateHandler(WebsocketChannelUpdate.Root? channelUpdate)
        {
            try
            {
                // Get the events from the guild
                var events = await _client.Guilds.FirstOrDefault().GetEventsAsync();
                // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
                var active_events = events.Where(e => e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") && e.CreatorId == _client.CurrentUser.Id);
                // Update the events
                foreach (var e in active_events)
                {
                    await e.ModifyAsync(x =>
                    {
                        x.Name = channelUpdate.payload.Event.title;
                        x.Description = channelUpdate.payload.Event.category_name;
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on channelUpdate");
            }
        }
    }
}
