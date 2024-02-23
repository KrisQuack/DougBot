using Discord;
using DougBot.Shared;
using DougBot.Discord.Notifications;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;

namespace DougBot.Discord.Modules
{
    public class ReactionFilter_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var guild = notification.Client.Guilds.FirstOrDefault();
                        var settings = await new Mongo().GetBotSettings();
                        var guild_emotes = guild.Emotes;
                        var emote_whitelist = (BsonArray)settings["reaction_filter_emotes"];
                        var filter_channel_ids = (BsonArray)settings["reaction_filter_channels"];
                        var filter_channels = guild.TextChannels.Where(x => filter_channel_ids.Contains(x.Id.ToString()));
                        // Add the guild's emotes to the whitelist
                        foreach (var emote in guild_emotes)
                        {
                            if (!emote_whitelist.Contains(emote.Name))
                            {
                                emote_whitelist.Add(emote.Name);
                            }
                        }
                        // For each channel, get the last 100 messages
                        foreach (var channel in filter_channels)
                        {
                            var response = $"**Filtering reactions in {channel.Name}**\n";
                            var any_removed = false;
                            var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                            foreach (var message in messages)
                            {
                                response += $"**Message {message.GetJumpUrl()}**\n";
                                // Remove any not permitted reactions
                                foreach (var reaction in message.Reactions)
                                {
                                    if (!emote_whitelist.Contains(reaction.Key.Name))
                                    {
                                        // Get users who reacted with this emote
                                        var users = await message.GetReactionUsersAsync(reaction.Key, int.MaxValue).FlattenAsync();
                                        var guild_users = users.OfType<IGuildUser>().ToList();
                                        // If any of the users are mods, skip
                                        if (guild_users.Any(x => x.IsBot || x.IsWebhook || x.GuildPermissions.ModerateMembers))
                                        {
                                            continue;
                                        }
                                        await message.RemoveAllReactionsForEmoteAsync(reaction.Key);
                                        response += $"{reaction.Key.Name} {users.Count()}\n";
                                        any_removed = true;
                                    }
                                }
                            }
                            if (any_removed)
                            {
                                Log.Information(response);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!ex.Message.Contains("10014"))
                        {
                            Log.Error(ex, "Error in ReactionFilter_ReadyHandler");
                        }
                    }
                    // Sleep for 10 minutes
                    await Task.Delay(600000);
                }
            });
        }
    }
}
