using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class Message
{
    public decimal Id { get; set; }

    public decimal? ChannelId { get; set; }

    public decimal? MemberId { get; set; }

    public string? Content { get; set; }

    public List<string>? Attachments { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<MessageUpdate> MessageUpdates { get; set; } = new List<MessageUpdate>();
}
