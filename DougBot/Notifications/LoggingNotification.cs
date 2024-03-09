using MediatR;
using Serilog.Events;

public class LoggingNotification : INotification
{
    public LoggingNotification(LogEvent logEvent)
    {
        LogEvent = logEvent;
    }

    public LogEvent LogEvent { get; }
}