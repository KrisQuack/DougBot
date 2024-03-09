namespace DougBot.Shared.Database;

public class YoutubeSetting
{
    public int SettingId { get; set; }

    public string? YoutubeId { get; set; }

    public decimal? MentionRoleId { get; set; }

    public decimal? PostChannelId { get; set; }

    public string? LastVideoId { get; set; }

    public string? UploadPlaylistId { get; set; }
}