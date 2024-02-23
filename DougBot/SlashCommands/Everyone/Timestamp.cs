﻿using Discord;
using Discord.Interactions;
using TimeZoneConverter;

namespace DougBot.Discord.SlashCommands.Everyone
{
    public class Timestamp : InteractionModuleBase
    {
        [SlashCommand("timestamp", "Convert a date and time to a Discord timestamp")]
        [EnabledInDm(false)]
        public async Task task(
            [Summary(description: "The date to convert to a timestamp. Format: 04/Jul/2000"), Autocomplete(typeof(DateAutocompleteHandler))] string date,
            [Summary(description: "The time to convert to a timestamp. Format: 12:00"), Autocomplete(typeof(TimeAutocompleteHandler))] string time,
            [Summary(description: "The time zone to use. Format: America/Los_Angeles"), Autocomplete(typeof(TimezoneAutocompleteHandler))] string timezone)
        {
            // Get the time zone
            var tz = TZConvert.GetTimeZoneInfo(timezone);
            // Parse the date and time
            var dateTime = DateTime.Parse($"{date} {time} {tz.BaseUtcOffset}");
            var dateTimeOffset = new DateTimeOffset(dateTime);
            var parsedUnixTime = dateTimeOffset.ToUnixTimeSeconds();
            var embed = new EmbedBuilder()
                .WithTitle("Discord Timestamp")
                .WithColor(Color.Blue)
                .AddField("Relative Time", "`<t:" + parsedUnixTime + ":R>` : <t:" + parsedUnixTime + ":R>")
                .AddField("Absolute Time", "`<t:" + parsedUnixTime + ":F>` : <t:" + parsedUnixTime + ":F>")
                .AddField("Short Date", "`<t:" + parsedUnixTime + ":f>` : <t:" + parsedUnixTime + ":f>")
                .AddField("Long TIme", "`<t:" + parsedUnixTime + ":T>` : <t:" + parsedUnixTime + ":T>")
                .AddField("Short Time", "`<t:" + parsedUnixTime + ":t>` : <t:" + parsedUnixTime + ":t>")
                .Build();
            await RespondAsync($"<t:{parsedUnixTime}:t> <t:{parsedUnixTime}:R>", embed: embed, ephemeral: true);
        }
    }

    public class DateAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var date = DateTime.UtcNow.ToString("dd/MMM/yyyy");
            return AutocompletionResult.FromSuccess(new[] { new AutocompleteResult(date, date) });
        }
    }

    public class TimeAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var time = DateTime.UtcNow.ToString("HH:mm");
            return AutocompletionResult.FromSuccess(new[] { new AutocompleteResult(time, time) });
        }
    }

    public class TimezoneAutocompleteHandler : AutocompleteHandler
    {
        public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services)
        {
            var current = autocompleteInteraction.Data.Current.Value.ToString();
            var timezones = TZConvert.KnownIanaTimeZoneNames;
            if (!string.IsNullOrEmpty(current))
            {
                IEnumerable<AutocompleteResult> results = timezones.Where(x => x.ToLower().Contains(current.ToLower())).Select(x => new AutocompleteResult(x, x));
                return AutocompletionResult.FromSuccess(results.Take(25));
            }
            else
            {
                return AutocompletionResult.FromSuccess(new[] { new AutocompleteResult("America/Los_Angeles", "America/Los_Angeles") });
            }
        }
    }
}
