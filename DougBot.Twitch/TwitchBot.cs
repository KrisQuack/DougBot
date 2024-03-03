using Discord.WebSocket;
using DougBot.Shared;
using MongoDB.Bson;
using Serilog;
using TwitchLib.Api;

namespace DougBot.Twitch;

public class TwitchBot
{
    private readonly DiscordSocketClient _client;
    private BsonDocument _settings;
    private TwitchAPI _twitchApi;

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
            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = _settings["twitch_client_id"].AsString;
            _twitchApi.Settings.Secret = _settings["twitch_client_secret"].AsString;
            var refresh = await _twitchApi.Auth.RefreshAuthTokenAsync(_settings["twitch_bot_refresh_token"].AsString,
                _twitchApi.Settings.Secret, _twitchApi.Settings.ClientId);
            _twitchApi.Settings.AccessToken = refresh.AccessToken;
            // Connect to EventSub
            var eventSub = new EventSub();
            _ = eventSub.ConnectAsync(_client, _twitchApi);

            while (true)
            {
                // Get the time untill the refresh token expires
                var time = refresh.ExpiresIn;
                Log.Information($"Twitch Bot initialized. Refresh token expires in {time} seconds");
                // Wait untill 10 minutes before the token expires
                await Task.Delay((time - 600) * 1000);
                // Refresh the token
                refresh = await _twitchApi.Auth.RefreshAuthTokenAsync(_settings["twitch_bot_refresh_token"].AsString,
                    _twitchApi.Settings.Secret, _twitchApi.Settings.ClientId);
                // Update the settings
                _twitchApi.Settings.AccessToken = refresh.AccessToken;
                eventSub.UpdateAccessToken(refresh.AccessToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in TwitchBot");
        }
    }
}