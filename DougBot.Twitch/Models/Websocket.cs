using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DougBot.Twitch.Models
{
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
        public string status { get; set; }
        public string type { get; set; }
        public string version { get; set; }
        public Condition condition { get; set; }
        public Transport transport { get; set; }
        public DateTime created_at { get; set; }
        public int cost { get; set; }
    }

    public class Condition
    {
        public string broadcaster_user_id { get; set; }
        public string user_id { get; set; }
    }

    public class Transport
    {
        public string method { get; set; }
        public string session_id { get; set; }
    }
}
