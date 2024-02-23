///////////////////////////
//// Dissabled for now ////
///////////////////////////

//using Discord;
//using Discord.WebSocket;
//using DougBot.Shared;
//using DougBot.Discord.Notifications;
//using MediatR;
//using MongoDB.Bson;
//using TwitchLib.Api;
//using TwitchLib.EventSub.Websockets;
//using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
//using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
//using TwitchLib.PubSub;
//using TwitchLib.PubSub.Events;

//namespace DougBot.Discord.Modules
//{
//    public class TwitchBot_ReadyHandler : INotificationHandler<ReadyNotification>
//    {
//        private DiscordSocketClient _client;
//        private SocketGuild _guild;
//        private BsonDocument _settings;
//        private TwitchAPI _twitchAPI;
//        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
//        {
//            _ = Task.Run(async () =>
//            {
//                // Get the settings
//                _client = notification.Client;
//                _guild = _client.Guilds.FirstOrDefault();
//                var settings = await new Mongo().GetBotSettings();
//                _settings = settings;
//                // Setup the API
//                _twitchAPI = new TwitchAPI();
//                _twitchAPI.Settings.ClientId = settings["twitch_client_id"].AsString;
//                _twitchAPI.Settings.Secret = settings["twitch_client_secret"].AsString;
//                var refresh = await _twitchAPI.Auth.RefreshAuthTokenAsync(settings["twitch_bot_refresh_token"].AsString, _twitchAPI.Settings.Secret, _twitchAPI.Settings.ClientId);
//                _twitchAPI.Settings.AccessToken = refresh.AccessToken;
//                // Setup the PubSub
//                var pubsub = new TwitchPubSub();
//                pubsub.OnPrediction += onPrediction;
//                pubsub.OnPubSubServiceConnected += (sender, e) =>
//                {
//                    pubsub.ListenToPredictions(settings["twitch_channel_id"].AsString);
//                };
//                await pubsub.ConnectAsync();

//                // Setup the EventSub
//                var eventsub = new EventSubWebsocketClient();
//                eventsub.ChannelUpdate += channelUpdate;
//                eventsub.StreamOnline += streamOnline;
//                eventsub.StreamOffline += streamOffline;
//                await eventsub.ConnectAsync();
//                // Subscribe to the events
//                await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
//                    "channel.update", "2",
//                    new Dictionary<string, string>
//                    {
//                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
//                    },
//                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket
//                );
//                await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
//                    "stream.online", "1",
//                    new Dictionary<string, string>
//                    {
//                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
//                    },
//                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket
//                );
//                await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
//                    "stream.offline", "1",
//                    new Dictionary<string, string>
//                    {
//                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
//                    },
//                    TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket
//                );
//            });
//        }

//        private async Task streamOffline(object sender, StreamOfflineArgs args)
//        {
//            // Get the events from the guild
//            var events = await _guild.GetEventsAsync();
//            // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
//            var active_events = events.Where(e => e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") && e.CreatorId == _client.CurrentUser.Id);
//            // Close the events
//            foreach (var e in active_events)
//            {
//                await e.EndAsync();
//            }
//        }

//        private async Task streamOnline(object sender, StreamOnlineArgs args)
//        {
//            // get stream info
//            var streams = await _twitchAPI.Helix.Streams.GetStreamsAsync(userIds: new List<string> { _settings["twitch_channel_id"].AsString });
//            var stream = streams.Streams.FirstOrDefault();
//            // Get the stream thumbnail from url to Image
//            using var httpClient = new HttpClient();
//            var response = await httpClient.GetAsync(stream.ThumbnailUrl.Replace("{width}", "1920").Replace("{height}", "1080"));
//            var stream_thumbnail = await response.Content.ReadAsByteArrayAsync();
//            using var memStream = new MemoryStream(stream_thumbnail);
//            // Create a new event
//            var stream_event = await _guild.CreateEventAsync(stream.Title, DateTimeOffset.UtcNow, GuildScheduledEventType.External, location: "https://twitch.tv/dougdoug", coverImage: new Image(memStream));
//            // Start the event
//            await stream_event.StartAsync();
//        }

//        private async Task channelUpdate(object sender, ChannelUpdateArgs args)
//        {
//            // Get the events from the guild
//            var events = await _guild.GetEventsAsync();
//            // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
//            var active_events = events.Where(e => e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") && e.CreatorId == _client.CurrentUser.Id);
//            // Update the events
//            foreach (var e in active_events)
//            {
//                await e.ModifyAsync(x => x.Name = args.Notification.Payload.Event.Title);
//            }
//        }

//        private async void onPrediction(object? sender, OnPredictionArgs e)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
