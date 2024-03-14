using System;
using System.Collections.Generic;

namespace DougBot.Shared.Database;

public partial class Serilog
{
    public string? Message { get; set; }

    public string? MessageTemplate { get; set; }

    public string? Level { get; set; }

    public DateTime? RaiseDate { get; set; }

    public string? Exception { get; set; }

    public string? Properties { get; set; }

    public string? MachineName { get; set; }
}
