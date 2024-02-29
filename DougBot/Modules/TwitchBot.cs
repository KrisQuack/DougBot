/////////////////////////
// Dissabled for now ////
/////////////////////////

using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using DougBot.Discord.Notifications;
using MediatR;
using MongoDB.Bson;
using TwitchLib.Api;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Stream;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using Serilog;
using TwitchLib.PubSub.Enums;

namespace DougBot.Discord.Modules
{
    public class TwitchBot_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        private DiscordSocketClient _client;
        private SocketGuild _guild;
        private BsonDocument _settings;
        private TwitchAPI _twitchAPI;
        private IThreadChannel _gambleChannel;
        private IUserMessage? _currentGamble;
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    // Get the settings
                    _client = notification.Client;
                    _guild = _client.Guilds.FirstOrDefault();
                    var settings = await new Mongo().GetBotSettings();
                    _settings = settings;
                    _gambleChannel = _guild.GetThreadChannel(Convert.ToUInt64(settings["twitch_gambling_channel_id"]));
                    // Load the current gamble if there is one
                    var pins = await _gambleChannel.GetPinnedMessagesAsync();
                    _currentGamble = (IUserMessage)pins.FirstOrDefault(x => x.Author.Id == _client.CurrentUser.Id);
                    // Setup the API
                    _twitchAPI = new TwitchAPI();
                    _twitchAPI.Settings.ClientId = settings["twitch_client_id"].AsString;
                    _twitchAPI.Settings.Secret = settings["twitch_client_secret"].AsString;
                    var refresh = await _twitchAPI.Auth.RefreshAuthTokenAsync(settings["twitch_bot_refresh_token"].AsString, _twitchAPI.Settings.Secret, _twitchAPI.Settings.ClientId);
                    _twitchAPI.Settings.AccessToken = refresh.AccessToken;
                    // Setup the PubSub
                    var pubsub = new TwitchPubSub();
                    pubsub.ListenToPredictions(settings["twitch_channel_id"].AsString);
                    pubsub.OnPrediction += onPrediction;
                    pubsub.OnListenResponse += (sender, e) =>
                    {
                        if (e.Successful)
                        {
                            Log.Information("Successfully listening to predictions");
                        }
                        else
                        {
                            Log.Error("Failed to listen to predictions");
                        }
                    };
                    pubsub.OnPubSubServiceError += (sender, e) =>
                    {
                        Log.Error(e.Exception, "Error on Twitch PubSub");
                    };
                    pubsub.OnPubSubServiceConnected += async (sender, e) =>
                    {
                        await pubsub.SendTopicsAsync(_twitchAPI.Settings.AccessToken);
                    };
                    await pubsub.ConnectAsync();

                    // Setup the EventSub
                    var eventsub = new EventSubWebsocketClient();
                    eventsub.ChannelUpdate += channelUpdate;
                    eventsub.StreamOnline += streamOnline;
                    eventsub.StreamOffline += streamOffline;
                    eventsub.WebsocketConnected += async (sender, e) =>
                    {
                        // Subscribe to the events
                        await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "channel.update", "2",
                            new Dictionary<string, string>
                            {
                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
                            },
                            TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                            eventsub.SessionId
                        );
                        await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "stream.online", "1",
                            new Dictionary<string, string>
                            {
                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
                            },
                            TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                            eventsub.SessionId
                        );
                        await _twitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync(
                            "stream.offline", "1",
                            new Dictionary<string, string>
                            {
                        {"broadcaster_user_id", settings["twitch_channel_id"].AsString},
                            },
                            TwitchLib.Api.Core.Enums.EventSubTransportMethod.Websocket,
                            eventsub.SessionId
                        );
                        Log.Information("Successfully connected to Twitch EventSub");
                    };
                    eventsub.ErrorOccurred += async (sender, e) =>
                    {
                        Log.Error(e.Exception, "Error on Twitch EventSub");
                    };
                    await eventsub.ConnectAsync();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error on TwitchBot_ReadyHandler");
                }
            });
        }

        private async Task streamOffline(object sender, StreamOfflineArgs args)
        {
            try
            {
                // Get the events from the guild
                var events = await _guild.GetEventsAsync();
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

        private async Task streamOnline(object sender, StreamOnlineArgs args)
        {
            try
            {
                // get stream info
                var streams = await _twitchAPI.Helix.Streams.GetStreamsAsync(userIds: new List<string> { _settings["twitch_channel_id"].AsString });
                var stream = streams.Streams.FirstOrDefault();
                // Get the stream thumbnail from url to Image
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(stream.ThumbnailUrl.Replace("{width}", "1920").Replace("{height}", "1080"));
                var stream_thumbnail = await response.Content.ReadAsByteArrayAsync();
                using var memStream = new MemoryStream(stream_thumbnail);
                // Create a new event
                var stream_event = await _guild.CreateEventAsync(stream.Title, DateTimeOffset.UtcNow, GuildScheduledEventType.External, location: "https://twitch.tv/dougdoug", coverImage: new Image(memStream));
                // Start the event
                await stream_event.StartAsync();
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on streamOnline");
            }
        }

        private async Task channelUpdate(object sender, ChannelUpdateArgs args)
        {
            try
            {
                // Get the events from the guild
                var events = await _guild.GetEventsAsync();
                // Get the event that is active has "https://twitch.tv/dougdoug" in the location and was created by the bot
                var active_events = events.Where(e => e.Status == GuildScheduledEventStatus.Active && e.Location.Contains("https://twitch.tv/dougdoug") && e.CreatorId == _client.CurrentUser.Id);
                // Update the events
                foreach (var e in active_events)
                {
                    await e.ModifyAsync(x => x.Name = args.Notification.Payload.Event.Title);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on channelUpdate");
            }
        }

        private async void onPrediction(object? sender, OnPredictionArgs args)
        {
            try
            {
                // Dictionary of color and its emoji
                var colors = new Dictionary<string, string>
                {
                    {"BLUE", "🟦"},
                    {"PINK", "🟪"}
                };

                // If it is a new prediction
                if (args.Type == PredictionType.EventCreated)
                {
                    // Create the embed
                    var embed = new EmbedBuilder()
                        .WithTitle(args.Title)
                        .WithColor(Color.Orange)
                        .WithTimestamp(args.EndedAt.Value);
                    // Add a field for the outcomes
                    embed.AddField("Outcomes", string.Join("\n", args.Outcomes.Select(o => $"{colors[o.Color]} {o.Title}")));
                    // Send the embed to the gamble channel
                    _currentGamble = await _gambleChannel.SendMessageAsync("<@&1080237787174948936>", embed: embed.Build());
                    await _currentGamble.PinAsync();
                }
                // Else if is is an update
                else if (args.Type == PredictionType.EventUpdated)
                {
                    // Create the embed
                    var embed = new EmbedBuilder()
                        .WithTitle(args.Title)
                        .WithColor(Color.Orange)
                        .WithTimestamp(args.EndedAt.Value);
                    // If the prediction is ongoing
                    if (args.Status == PredictionStatus.Active)
                    {
                        // Display the ongoing stats for the outcomes and top 5 gamblers
                        foreach (var outcome in args.Outcomes)
                        {
                            embed.AddField(
                                $"{colors[outcome.Color]} {outcome.Title}",
                                string.Join("\n", outcome.TopPredictors.Select(x => $"{x.DisplayName} be {x.Points} points")),
                                false);
                        }
                    }
                    // Else if the prediction is resolved
                    else if (args.Status == PredictionStatus.Resolved)
                    {
                        var totalPoints = args.Outcomes.Sum(o => o.TotalPoints);
                        var totalUsers = args.Outcomes.Sum(o => o.TotalUsers);

                        foreach (var outcome in args.Outcomes)
                        {
                            var isWinner = outcome.Id == args.WinningOutcomeId ? "✅" : "❌";
                            var userPercentage = (double)outcome.TotalUsers / totalUsers * 100;
                            var pointsPercentage = (double)outcome.TotalPoints / totalPoints * 100;
                            var ratio = (double)totalPoints / outcome.TotalPoints;
                            var topPredictorsStr = string.Join("\n", outcome.TopPredictors.Select(x => $"{x.DisplayName} {(outcome.Id == args.WinningOutcomeId ? "won" : "lost")} {x.Points * ratio} points").Take(5)) ?? "None";

                            embed.AddField(
                                $"{colors[outcome.Color]} Outcome: {outcome.Title} {isWinner}",
                                $"Points: {outcome.TotalPoints} ({pointsPercentage:F2}%)\nUsers: {outcome.TotalUsers} ({userPercentage:F2}%)\nRatio: {ratio:F2}\n**Top Predictors:**\n{topPredictorsStr}",
                                false);
                        }
                    }
                    else if(args.Status == PredictionStatus.Canceled)
                    {
                        await _currentGamble.DeleteAsync();
                        _currentGamble = null;
                        return;
                    }
                    // Edit the message with the new embed if it exists and it is a 5th second
                    if(_currentGamble != null && DateTime.UtcNow.Second % 5 == 0)
                    {
                        await _currentGamble.ModifyAsync(x => x.Embed = embed.Build());
                    }
                    // Else send the new embed
                    else
                    {
                        _currentGamble = await _gambleChannel.SendMessageAsync("<@&1080237787174948936>", embed: embed.Build());
                        await _currentGamble.PinAsync();
                    }
                    // Finally if it was resolved, clearn the current gamble
                    if (args.Status == PredictionStatus.Resolved)
                    {
                        await _gambleChannel.SendMessageAsync($"The prediction has been resolved! {_currentGamble.GetJumpUrl()}");
                        await _currentGamble.UnpinAsync();
                        _currentGamble = null;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error on onPrediction");
            }
        }
    }
}
