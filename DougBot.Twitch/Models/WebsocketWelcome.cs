namespace DougBot.Twitch.Models;

internal class WebsocketWelcome
{
    public class Root
    {
        public Metadata metadata { get; set; }
        public Payload payload { get; set; }
    }

    public class Metadata
    {
        public string message_id { get; set; }
        public string message_type { get; set; }
        public DateTime message_timestamp { get; set; }
    }

    public class Session
    {
        public string id { get; set; }
        public string status { get; set; }
        public DateTime connected_at { get; set; }
        public int keepalive_timeout_seconds { get; set; }
        public object reconnect_url { get; set; }
    }

    public class Payload
    {
        public Session session { get; set; }
    }
}