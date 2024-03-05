using Discord;
using DougBot.Discord.Notifications;
using DougBot.Shared;
using MediatR;
using MongoDB.Bson;
using Serilog;

namespace DougBot.Discord.Modules;

public class ReactionFilterReadyHandler : INotificationHandler<ReadyNotification>
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
                    var guildEmotes = guild.Emotes;
                    var emoteWhitelist = (BsonArray)settings["reaction_filter_emotes"];
                    var filterChannelIds = (BsonArray)settings["reaction_filter_channels"];
                    var filterChannels = guild.TextChannels.Where(x => filterChannelIds.Contains(x.Id.ToString()));
                    // Add the guild's emotes to the whitelist
                    foreach (var emote in guildEmotes)
                        if (!emoteWhitelist.Contains(emote.Name))
                            emoteWhitelist.Add(emote.Name);
                    // For each channel, get the last 100 messages
                    foreach (var channel in filterChannels)
                    {
                        var response = $"**{channel.Name}**\n";
                        var messages = await channel.GetMessagesAsync().FlattenAsync();
                        var removedReactions = new List<Tuple<IMessage, IEmote>>();
                        foreach (var message in messages)
                            // Remove any not permitted reactions
                        foreach (var reaction in message.Reactions)
                            if (!emoteWhitelist.Contains(reaction.Key.Name))
                                try
                                {
                                    // Get users who reacted with this emote
                                    var users = await message.GetReactionUsersAsync(reaction.Key, int.MaxValue)
                                        .FlattenAsync();
                                    var guildUsers = users.OfType<IGuildUser>().ToList();
                                    // If any of the users are mods, skip
                                    if (guildUsers.Any(
                                            x => x.IsBot || x.IsWebhook || x.GuildPermissions.ModerateMembers))
                                        continue;
                                    await message.RemoveAllReactionsForEmoteAsync(reaction.Key);
                                    removedReactions.Add(Tuple.Create(message, reaction.Key));
                                }
                                catch (Exception)
                                {
                                }

                        // For each unique message, send a log
                        foreach (var message in removedReactions.Select(x => x.Item1).Distinct())
                            response +=
                                $"\nMessage {message.GetJumpUrl()}\n{string.Join("\n", removedReactions.Where(x => x.Item1 == message).Select(x => x.Item2))}";
                        if (removedReactions.Count > 0) Log.Information("[{Source}] {Message}", "Reaction Filter", response);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[{Source}]",  "ReactionFilter_ReadyHandler");
                }

                // Sleep for 10 minutes
                await Task.Delay(600000);
            }
        });
    }
}