using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class YoutubeSetting
{
    public int SettingId { get; set; }

    public string? YoutubeId { get; set; }

    public decimal? MentionRoleId { get; set; }

    public decimal? PostChannelId { get; set; }

    public string? LastVideoId { get; set; }

    public string? UploadPlaylistId { get; set; }
}
