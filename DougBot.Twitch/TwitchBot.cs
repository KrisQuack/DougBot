using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using DougBot.Twitch.Models;
using MongoDB.Bson;
using Serilog;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace DougBot.Twitch
{
    public class TwitchBot
    {
        private TwitchAPI _twitchAPI;
        private string _websocketBaseUrl = "wss://eventsub.wss.twitch.tv/ws?keepalive_timeout_seconds=60";
        private ClientWebSocket _websocketClient;
        private string _websocketSessionId;
        private DateTime _lastKeepalive = DateTime.UtcNow;
        private DiscordSocketClient _client;
        private BsonDocument _settings;
        private User _botUser;
        private User _dougUser;
        public TwitchBot(DiscordSocketClient client)
        {
            _client = client;
            Task.Run(() => InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            while (true)
            {
                try
                {
                    Log.Information("Initializing Twitch Bot");
                    // Get the settings
                    _settings = await new Mongo().GetBotSettings();
                    // Authenticate
                    _twitchAPI = new TwitchAPI();
                    _twitchAPI.Settings.ClientId = _settings["twitch_client_id"].AsString;
                    _twitchAPI.Settings.Secret = _settings["twitch_client_secret"].AsString;
                    var refresh = await _twitchAPI.Auth.RefreshAuthTokenAsync(_settings["twitch_bot_refresh_token"].AsString, _twitchAPI.Settings.Secret, _twitchAPI.Settings.ClientId);
                    _twitchAPI.Settings.AccessToken = refresh.AccessToken;
                    //
                    var botUser = await _twitchAPI.Helix.Users.GetUsersAsync(logins: new List<string> { _settings["twitch_bot_name"].AsString });
                    var dougUser = await _twitchAPI.Helix.Users.GetUsersAsync(logins: new List<string> { _settings["twitch_channel_name"].AsString });
                    _botUser = botUser.Users[0];
                    _dougUser = dougUser.Users[0];
                    // Unsubscribe from all events
                    var subscriptions = await _twitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync();
                    foreach (var sub in subscriptions.Subscriptions)
                    {
                        await _twitchAPI.Helix.EventSub.DeleteEventSubSubscriptionAsync(sub.Id);
                    }
                    // Connect to the websocket and print the response
                    _websocketClient = new ClientWebSocket();
                    await _websocketClient.ConnectAsync(new Uri(_websocketBaseUrl), CancellationToken.None);
                    await ListenToWebsocket();
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
                    // Print the status of the subscriptions
                    subscriptions = await _twitchAPI.Helix.EventSub.GetEventSubSubscriptionsAsync();
                    // Print the status of the subscriptions
                    Log.Information($"Subscriptions:\n{string.Join(Environment.NewLine, subscriptions.Subscriptions.Select(sub => $"{sub.Type} - {sub.Status}"))}");
                    // Wait for the websocket to close
                    while (_websocketClient.State == WebSocketState.Open)
                    {
                        // Check if the keepalive is older than 5 minutes
                        if (DateTime.UtcNow - _lastKeepalive > TimeSpan.FromMinutes(5))
                        {
                            await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Keepalive", CancellationToken.None);
                            break;
                        }
                        // Wait 10 seconds
                        await Task.Delay(10000);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error in TwitchBot");
                }
            }
        }

        private async Task ListenToWebsocket()
        {
            _ = Task.Run(async () =>
            {
                while (_websocketClient.State == WebSocketState.Open)
                {
                    try
                    {
                        var buffer = new byte[4096];
                        var result = await _websocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        // Parse the response using system.text.json
                        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var json = JsonDocument.Parse(response);
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
                        // Handle Reconnect
                        else if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() == "session_reconnect")
                        {
                            await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Keepalive", CancellationToken.None);
                            return;
                        }
                        // Handle Revocation
                        else if (json.RootElement.GetProperty("metadata").GetProperty("message_type").GetString() == "revocation")
                        {
                            await _websocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Revocation", CancellationToken.None);
                            return;
                        }
                        // If there is the subscription type, get it
                        else if(json.RootElement.GetProperty("metadata").TryGetProperty("subscription_type", out var subscriptionType))
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
                            Log.Warning($"Invalid Request: {json.RootElement}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in TwitchBot websocket");
                    }
                }
            });
        }

        private async Task ChannelChatMessage(WebsocketChannelChatMessage.Root? chatMessage)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    if(chatMessage.payload.Event.message.text.Contains("DMC-") && chatMessage.payload.Event.channel_points_custom_reward_id.ToString() == "a5b9d1c7-44f9-4964-b0f7-42c39cb04f98")
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
                                var mcRole = guild.GetRole(698681714616303646);
                                var pesosRole = guild.GetRole(954017881652342786);
                                await discordUser.AddRoleAsync(mcRole);
                                await discordUser.AddRoleAsync(pesosRole);
                                // Send a message
                                var mcChannel = guild.GetTextChannel(698679698699583529);
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
            });
        }

        private async Task ChannelStreamOffline(WebsocketStreamOffline.Root? streamOffline)
        {
            _ = Task.Run(async () =>
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
            });
        }

        private async Task ChannelSteamOnline(WebsocketStreamOnline.Root? streamOnline)
        {
            _ = Task.Run(async () =>
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
                    var stream_event = await _client.Guilds.FirstOrDefault().CreateEventAsync(stream.Title, DateTimeOffset.UtcNow, GuildScheduledEventType.External, location: "https://twitch.tv/dougdoug", coverImage: new Image(memStream));
                    // Start the event
                    await stream_event.StartAsync();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error on streamOnline");
                }
            });
        }

        private async Task ChannelUpdateHandler(WebsocketChannelUpdate.Root? channelUpdate)
        {
            _ = Task.Run(async () =>
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
                        await e.ModifyAsync(x => x.Name = channelUpdate.payload.Event.title);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error on channelUpdate");
                }
            });
        }
    }
}
