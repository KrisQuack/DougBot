using System.Text.Json;
using Discord;
using Discord.Interactions;
using DougBot.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace DougBot.Discord.SlashCommands.Owner;

public class Logs(DougBotContext context) : InteractionModuleBase
{
    private readonly DougBotContext _context = context;
    
    [SlashCommand("logs", "reboot the bot")]
    [EnabledInDm(false)]
    [RequireOwner]
    public async Task Task([MaxValue(250)]int take = 25,
        [Choice("Discord", "Discord"),
         Choice("Gateway", "Gateway"),
         Choice("Twitch Bot", "Twitch Bot"),
         Choice("EventSub", "EventSub"),
         Choice("Reaction Filter", "Reaction Filter"),
        Choice("Database Sync", "Database Sync"),
        Choice("Verification Checks", "Verification Checks")] string source = null
        )
    {
        try
        {
            // Get the logs from the database
            var logs = new List<Shared.Database.Serilog>();
            if (source == null)
            {
                logs = await _context.Serilogs
                    .OrderByDescending(l => l.RaiseDate)
                    .Take(take)
                    .ToListAsync();
            }
            else
            {
                logs = await _context.Serilogs
                    .Where(l => l.Properties.Contains($"\"Source\": \"{source}\""))
                    .OrderByDescending(l => l.RaiseDate)
                    .Take(take)
                    .ToListAsync();
            }
            // Reverse the logs in date order
            logs.Reverse();
            // Split logs into groups of 25
            var logGroups = logs.Select((x, i) => new { Index = i, Value = x })
                .GroupBy(x => x.Index / 25)
                .Select(x => x.Select(v => v.Value).ToList())
                .ToList();
            var embeds = new List<Embed>();
            foreach (var logGroup in logGroups)
            {
                // Create an embed with the logs in fields
                var embed = new EmbedBuilder
                {
                    Title = "Logs",
                    Color = Color.DarkBlue
                };

                foreach (var log in logGroup)
                {
                    // Parse properties as JSON
                    var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(log.Properties);

                    // Determine the icon based on the log level
                    string icon;
                    switch (log.Level)
                    {
                        case "Information":
                            icon = "ℹ️"; // or use a Unicode character
                            break;
                        case "Warning":
                            icon = "⚠️"; // or use a Unicode character
                            break;
                        case "Error":
                            icon = "🚨"; // or use a Unicode character
                            break;
                        default:
                            icon = ""; // default icon
                            break;
                    }
                    
                    var message = properties.ContainsKey("Message") ? properties["Message"] : "";
                    if (!string.IsNullOrEmpty(log.Exception))
                    {
                        message += $"\n\n**Exception:** ```{log.Message}```";
                    }
                    if(!string.IsNullOrEmpty(message))
                    {
                        embed.AddField($"{icon} {log.RaiseDate.Value:HH:mm}: {properties["Source"]}", message);
                    }
                }
                embeds.Add(embed.Build());
            }
            
            // Send the logs
            await RespondAsync(embeds: embeds.ToArray(), ephemeral: true);
        }
        catch (Exception e)
        {
            throw new Exception("An error occurred while fetching the logs.", e);
        }
    }
}