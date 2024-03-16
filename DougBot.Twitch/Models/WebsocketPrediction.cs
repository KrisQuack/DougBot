using System.Text.Json.Serialization;

namespace DougBot.Twitch.Models;

internal class WebsocketPrediction
{
    public class Root
    {
        public string type { get; set; }
        public Data data { get; set; }
    }

    public class Data
    {
        public string timestamp { get; set; }
        [JsonPropertyName("event")] public Event Event { get; set; }
    }

    public class Event
    {
        public string id { get; set; }
        public string channel_id { get; set; }
        public string created_at { get; set; }
        public Created_by created_by { get; set; }
        public object ended_at { get; set; }
        public object ended_by { get; set; }
        public string locked_at { get; set; }
        public object locked_by { get; set; }
        public Outcomes[] outcomes { get; set; }
        public int prediction_window_seconds { get; set; }
        public string status { get; set; }
        public string title { get; set; }
        public object winning_outcome_id { get; set; }
    }

    public class Created_by
    {
        public string type { get; set; }
        public string user_id { get; set; }
        public string user_display_name { get; set; }
        public object extension_client_id { get; set; }
    }

    public class Outcomes
    {
        public string id { get; set; }
        public string color { get; set; }
        public string title { get; set; }
        public int total_points { get; set; }
        public int total_users { get; set; }
        public Top_predictors[] top_predictors { get; set; }
        public Badge badge { get; set; }
    }

    public class Top_predictors
    {
        public string id { get; set; }
        public string event_id { get; set; }
        public string outcome_id { get; set; }
        public string channel_id { get; set; }
        public int points { get; set; }
        public string predicted_at { get; set; }
        public string updated_at { get; set; }
        public string user_id { get; set; }
        public object result { get; set; }
        public string user_display_name { get; set; }
    }

    public class Badge
    {
        public string version { get; set; }
        public string set_id { get; set; }
    }


}