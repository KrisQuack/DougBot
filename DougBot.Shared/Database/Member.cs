using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class Member
{
    public decimal Id { get; set; }

    public string? Username { get; set; }

    public string? GlobalName { get; set; }

    public string? Nickname { get; set; }

    public List<decimal>? Roles { get; set; }

    public DateTime? JoinedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? McRedeem { get; set; }

    public int? Verification { get; set; }
}
