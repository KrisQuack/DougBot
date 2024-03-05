using MediatR;
using Serilog.Events;

public class LoggingNotification : INotification
{
    public LogEvent LogEvent { get; }

    public LoggingNotification(LogEvent logEvent)
    {
        LogEvent = logEvent;
    }
}