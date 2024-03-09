using System.Text.Json.Serialization;

namespace DougBot.Twitch.Models;

internal class WebsocketChannelChatMessage
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
        public string chatter_user_id { get; set; }
        public string chatter_user_login { get; set; }
        public string chatter_user_name { get; set; }
        public string message_id { get; set; }
        public Message message { get; set; }
        public string color { get; set; }
        public List<Badge> badges { get; set; }
        public string message_type { get; set; }
        public object cheer { get; set; }
        public object reply { get; set; }
        public object channel_points_custom_reward_id { get; set; }
    }

    public class Message
    {
        public string text { get; set; }
        public List<Fragment> fragments { get; set; }
    }

    public class Fragment
    {
        public string type { get; set; }
        public string text { get; set; }
        public object cheermote { get; set; }
        public object emote { get; set; }
        public object mention { get; set; }
    }

    public class Badge
    {
        [JsonPropertyName("set_id")] public string SetId { get; set; }

        public string id { get; set; }
        public string info { get; set; }
    }

    public class Payload
    {
        public Subscription subscription { get; set; }

        [JsonPropertyName("event")] public Event Event { get; set; }
    }
}