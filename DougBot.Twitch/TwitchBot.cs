using Discord;
using Discord.WebSocket;
using DougBot.Shared;
using MongoDB.Bson;
using Serilog;
using TwitchLib.Api;

namespace DougBot.Twitch
{
    public class TwitchBot
    {
        private TwitchAPI _twitchAPI;
        private DiscordSocketClient _client;
        private BsonDocument _settings;
        public TwitchBot(DiscordSocketClient client)
        {
            _client = client;
            Task.Run(() => InitializeAsync());
        }

        private async Task InitializeAsync()
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
                // Connect to EventSub
                var eventSub = new EventSub();
                _ = eventSub.ConnectAsync(_client, _twitchAPI);

                while (true)
                {
                    // Get the time untill the refresh token expires
                    var time = refresh.ExpiresIn;
                    Log.Information($"Twitch Bot initialized. Refresh token expires in {time} seconds");
                    // Wait untill 10 minutes before the token expires
                    await Task.Delay((time - 600) * 1000);
                    // Refresh the token
                    refresh = await _twitchAPI.Auth.RefreshAuthTokenAsync(_settings["twitch_bot_refresh_token"].AsString, _twitchAPI.Settings.Secret, _twitchAPI.Settings.ClientId);
                    // Update the settings
                    _twitchAPI.Settings.AccessToken = refresh.AccessToken;
                    eventSub.UpdateAccessToken(refresh.AccessToken);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in TwitchBot");
            }
        }
    }
}
