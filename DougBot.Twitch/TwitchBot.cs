using Discord.WebSocket;
using DougBot.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TwitchLib.Api;

namespace DougBot.Twitch;

public class TwitchBot
{
    private readonly DiscordSocketClient _client;
    private Botsetting _settings;
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
            Log.Information("[{Source}] {Message}", "Twitch Bot", "Initializing");
            // Get the settings
            await using var db = new DougBotContext();
            _settings = await db.Botsettings.FirstOrDefaultAsync();
            // Authenticate
            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = _settings.TwitchClientId;
            _twitchApi.Settings.Secret = _settings.TwitchClientSecret;
            var refresh = await _twitchApi.Auth.RefreshAuthTokenAsync(_settings.TwitchBotRefreshToken,
                _twitchApi.Settings.Secret, _twitchApi.Settings.ClientId);
            _twitchApi.Settings.AccessToken = refresh.AccessToken;
            // Connect to EventSub
            var eventSub = new EventSub();
            _ = eventSub.ConnectAsync(_client, _twitchApi);

            while (true)
            {
                // Get the time untill the refresh token expires
                var time = refresh.ExpiresIn;
                // Convert this to a discord timestamp
                var dateTimeOffset = new DateTimeOffset(DateTime.UtcNow.AddSeconds(time));
                var parsedUnixTime = dateTimeOffset.ToUnixTimeSeconds();
                Log.Information("[{Source}] {Message}", "Twitch Bot",
                    $"Refresh token expires in <t:{parsedUnixTime}:R> at <t:{parsedUnixTime}:F>");
                // Wait until 10 minutes before the token expires
                await Task.Delay((time - 600) * 1000);
                // Refresh the token
                refresh = await _twitchApi.Auth.RefreshAuthTokenAsync(_settings.TwitchBotRefreshToken,
                    _twitchApi.Settings.Secret, _twitchApi.Settings.ClientId);
                // Update the settings
                _twitchApi.Settings.AccessToken = refresh.AccessToken;
                eventSub.UpdateAccessToken(refresh.AccessToken);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Source}]", "TwitchBot");
        }
    }
}