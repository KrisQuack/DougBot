using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class MemberUpdate
{
    public int Id { get; set; }

    public decimal? MemberId { get; set; }

    public string? ColumnName { get; set; }

    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    public DateTime? UpdateTimestamp { get; set; }
}
