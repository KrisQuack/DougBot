using System.Text.Json.Serialization;

namespace DougBot.Twitch.Models;

internal class WebsocketChannelUpdate
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
        public string subscription_type { get; set; }
        public string subscription_version { get; set; }
    }

    public class Subscription
    {
        public string id { get; set; }
        public string type { get; set; }
        public string version { get; set; }
        public string status { get; set; }
        public int cost { get; set; }
        public Condition condition { get; set; }
        public Transport transport { get; set; }
        public DateTime created_at { get; set; }
    }

    public class Condition
    {
        public string broadcaster_user_id { get; set; }
    }

    public class Transport
    {
        public string method { get; set; }
        public string callback { get; set; }
    }

    public class Event
    {
        public string broadcaster_user_id { get; set; }
        public string broadcaster_user_login { get; set; }
        public string broadcaster_user_name { get; set; }
        public string title { get; set; }
        public string language { get; set; }
        public string category_id { get; set; }
        public string category_name { get; set; }
        public List<string> content_classification_labels { get; set; }
    }

    public class Payload
    {
        public Subscription subscription { get; set; }

        [JsonPropertyName("event")] public Event Event { get; set; }
    }
}