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
                            var messages = await channel.GetMessagesAsync(100).FlattenAsync();
                            var removed_reactions = new List<Tuple<IMessage, IEmote>>();
                            foreach (var message in messages)
                            {
                                // Remove any not permitted reactions
                                foreach (var reaction in message.Reactions)
                                {
                                    if (!emote_whitelist.Contains(reaction.Key.Name))
                                    {
                                        try
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
                                            removed_reactions.Add(Tuple.Create(message, reaction.Key));
                                        }
                                        catch (Exception) { }
                                    }
                                }
                            }
                            // For each unique message, send a log
                            foreach (var message in removed_reactions.Select(x => x.Item1).Distinct())
                            {
                                response += $"\nMessage {message.GetJumpUrl()}\n{string.Join("\n", removed_reactions.Where(x => x.Item1 == message).Select(x => x.Item2))}";
                            }
                            if (removed_reactions.Count > 0)
                            {
                                Log.Information(response);
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error in ReactionFilter_ReadyHandler");
                    }
                    // Sleep for 10 minutes
                    await Task.Delay(600000);
                }
            });
        }
    }
}
