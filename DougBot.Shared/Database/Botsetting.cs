namespace DougBot.Shared.Database;

public class Botsetting
{
    public int Id { get; set; }

    public decimal? DmReceiptChannelId { get; set; }

    public decimal? GuildId { get; set; }

    public decimal? LogChannelId { get; set; }

    public decimal? ModRoleId { get; set; }

    public decimal? NewMemberRoleId { get; set; }

    public decimal? FullMemberRoleId { get; set; }

    public List<string>? ReactionFilterEmotes { get; set; }

    public decimal? ReportChannelId { get; set; }

    public string? TwitchBotName { get; set; }

    public string? TwitchBotRefreshToken { get; set; }

    public string? TwitchChannelName { get; set; }

    public string? TwitchClientId { get; set; }

    public string? TwitchClientSecret { get; set; }

    public decimal? TwitchGamblingChannelId { get; set; }

    public decimal? TwitchModChannelId { get; set; }

    public string? YoutubeApi { get; set; }

    public string? AiApiVersion { get; set; }

    public string? AiAzureEndpoint { get; set; }

    public string? AiApiKey { get; set; }

    public decimal? ModChannelId { get; set; }

    public decimal? TwitchChannelId { get; set; }

    public List<decimal>? LogBlacklistChannels { get; set; }

    public List<decimal>? ReactionFilterChannels { get; set; }

    public string? DiscordToken { get; set; }
}