using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class MessageUpdate
{
    public int UpdateId { get; set; }

    public decimal? MessageId { get; set; }

    public string? ColumnName { get; set; }

    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime? UpdateTimestamp { get; set; }

    public virtual Message? Message { get; set; }
}
