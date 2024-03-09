using System.Text.Json.Serialization;

namespace DougBot.Twitch.Models;

internal class WebsocketStreamOnline
{
    public class Root
    {
        public Metadata metadata { get; set; }
        public Payload payload { get; set; }
    }

    public class Event
    {
        public string id { get; set; }
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string type { get; set; }
        public DateTime started_at { get; set; }
    }

    public class Payload
    {
        public Subscription subscription { get; set; }

        [JsonPropertyName("event")] public Event Event { get; set; }
    }
}