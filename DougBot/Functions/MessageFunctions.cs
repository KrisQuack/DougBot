using Discord;

namespace DougBot.Discord.Functions;

public class MessageFunctions
{
    public static string MessageToString(IEnumerable<IMessage> messages)
    {
        messages = messages.OrderBy(m => m.CreatedAt);
        var chatString = "";
        // Loop through the messages and add them to the string
        foreach (var msg in messages)
        {
            var authorName = msg.Author.Username;
            if (msg.Author is IGuildUser guildUser && guildUser.GuildPermissions.ManageMessages)
                authorName += " (mod)";
            if (msg.Author.IsBot)
                authorName += " (bot)";
            var content = msg.CleanContent;
            foreach (var embed in msg.Embeds)
            {
                if (embed.Title == "Chat Summary")
                    continue;
                content += $"\n# Embed\nAuthor: {embed.Author?.Name}\nTitle: {embed.Title}\nDescription: {embed.Description}\n";
                foreach (var field in embed.Fields)
                    content += $"Field: {field.Name}\nValue: {field.Value}\n";
            }
            chatString += $"{authorName}: {content}\n";
        }

        return chatString;
    }
}