using System.Text.Json.Serialization;

namespace DougBot.Twitch.Models;

internal class WebsocketStreamOffline
{
    public class Root
    {
        public Metadata metadata { get; set; }
        public Payload payload { get; set; }
    }

    public class Event
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
    }

    public class Payload
    {
        public Subscription subscription { get; set; }

        [JsonPropertyName("event")] public Event Event { get; set; }
    }
}