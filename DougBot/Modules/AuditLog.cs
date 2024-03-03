using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using DougBot.Discord.Notifications;
using DougBot.Shared;
using MediatR;
using MongoDB.Bson;
using Serilog;

namespace DougBot.Discord.Modules;

public class AuditLogUserJoined : INotificationHandler<UserJoinedNotification>
{
    public async Task Handle(UserJoinedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Print an embed
                var embed = new EmbedBuilder()
                    .WithTitle("User Joined")
                    .WithColor(Color.Green)
                    .WithAuthor($"{notification.User.Username} ({notification.User.Id})",
                        notification.User.GetAvatarUrl())
                    .AddField("Account Age",
                        (DateTime.UtcNow - notification.User.CreatedAt.UtcDateTime).TotalDays.ToString("0.00") +
                        " days")
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();
                var settings = await new Mongo().GetBotSettings();
                await notification.User.Guild.GetTextChannel(Convert.ToUInt64(settings["log_channel_id"]))
                    .SendMessageAsync(embed: embed);
                // Save to database
                var member = await new Mongo().GetMember(notification.User.Id);
                if (member == null)
                {
                    var guildMember = notification.User;
                    var newMember = new BsonDocument
                    {
                        { "_id", guildMember.Id.ToString() },
                        { "name", guildMember.Username != null ? guildMember.Username : BsonNull.Value },
                        { "global_name", guildMember.GlobalName != null ? guildMember.GlobalName : BsonNull.Value },
                        { "nick", guildMember.Nickname != null ? guildMember.Nickname : BsonNull.Value },
                        { "roles", new BsonArray(guildMember.Roles.Select(role => role.Id.ToString())) },
                        {
                            "joined_at",
                            guildMember.JoinedAt.HasValue ? guildMember.JoinedAt.Value.UtcDateTime : BsonNull.Value
                        },
                        { "created_at", guildMember.CreatedAt.UtcDateTime },
                        { "edits", new BsonArray() }
                    };
                    await new Mongo().InsertMember(newMember);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_UserJoined");
            }
        });
    }
}

public class AuditLogUserLeft : INotificationHandler<UserLeftNotification>
{
    public async Task Handle(UserLeftNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Print an embed
                var embed = new EmbedBuilder()
                    .WithTitle("User Left")
                    .WithColor(Color.Red)
                    .WithAuthor($"{notification.User.Username} ({notification.User.Id})",
                        notification.User.GetAvatarUrl())
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();
                var settings = await new Mongo().GetBotSettings();
                await notification.Guild.GetTextChannel(Convert.ToUInt64(settings["log_channel_id"]))
                    .SendMessageAsync(embed: embed);
                // Update the database
                var member = await new Mongo().GetMember(notification.User.Id);
                if (member != null)
                {
                    member["left_at"] = DateTime.UtcNow;
                    await new Mongo().UpdateMember(member);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_UserLeft");
            }
        });
    }
}

public class AuditLogGuildMemberUpdated : INotificationHandler<GuildMemberUpdatedNotification>
{
    public async Task Handle(GuildMemberUpdatedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // get the before and after states
                var after = notification.NewUser;
                // Load the user's dictionary
                var userDict = await new Mongo().GetMember(after.Id);
                if (userDict == null) return;
                // Load the embed
                var embed = new EmbedBuilder()
                    .WithTitle("Member Updated")
                    .WithAuthor($"{after.Username} ({after.Id})", after.GetAvatarUrl())
                    .WithColor(Color.Orange);
                // Get the roles
                var beforeRoles = userDict["roles"].AsBsonArray.Select(role => role.ToString()).ToHashSet();
                var afterRoles = after.Roles.Select(role => role.Id.ToString()).ToHashSet();
                var addedRoles = afterRoles.Except(beforeRoles).ToList();
                var removedRoles = beforeRoles.Except(afterRoles).ToList();

                var updateLog = new BsonDocument
                {
                    { "timestamp", DateTime.UtcNow },
                    { "changes", new BsonDocument() }
                };
                // Check each value, if it is changed add to embed and update database
                if ((userDict["nick"] == BsonNull.Value ? null : userDict["nick"]) != after.Nickname)
                {
                    embed.AddField("Nickname", $"{userDict["nick"]} -> {after.Nickname}");
                    updateLog["changes"]["nick"] = after.Nickname == null ? BsonNull.Value : after.Nickname;
                    userDict["nick"] = after.Nickname == null ? BsonNull.Value : after.Nickname;
                }

                if ((userDict["name"] == BsonNull.Value ? null : userDict["name"]) != after.Username)
                {
                    embed.AddField("Name", $"{userDict["name"]} -> {after.Username}");
                    updateLog["changes"]["name"] = after.Username == null ? BsonNull.Value : after.Username;
                    userDict["name"] = after.Username == null ? BsonNull.Value : after.Username;
                }

                if ((userDict["global_name"] == BsonNull.Value ? null : userDict["global_name"]) != after.GlobalName)
                {
                    embed.AddField("Global Name", $"{userDict["global_name"]} -> {after.GlobalName}");
                    updateLog["changes"]["global_name"] = after.GlobalName == null ? BsonNull.Value : after.GlobalName;
                    userDict["global_name"] = after.GlobalName == null ? BsonNull.Value : after.GlobalName;
                }

                if (addedRoles.Any() || removedRoles.Any())
                {
                    if (addedRoles.Any())
                        embed.AddField("Roles Added", string.Join(", ", addedRoles.Select(role => $"<@&{role}>")));
                    if (removedRoles.Any())
                        embed.AddField("Roles Removed", string.Join(", ", removedRoles.Select(role => $"<@&{role}>")));
                    updateLog["changes"]["roles"] = new BsonDocument
                    {
                        { "added", new BsonArray(addedRoles) },
                        { "removed", new BsonArray(removedRoles) }
                    };
                    userDict["roles"] = new BsonArray(after.Roles.Select(role => role.Id.ToString()));
                }

                // Send the embed
                if (embed.Fields.Any())
                {
                    var settings = await new Mongo().GetBotSettings();
                    await notification.NewUser.Guild.GetTextChannel(Convert.ToUInt64(settings["log_channel_id"]))
                        .SendMessageAsync(embed: embed.Build());
                }

                // Update the database
                if (updateLog["changes"].AsBsonDocument.ElementCount > 0)
                {
                    // Append the update log to the member's edits array
                    if (!userDict.Contains("edits")) userDict["edits"] = new BsonArray();
                    userDict["edits"].AsBsonArray.Add(updateLog);
                    await new Mongo().UpdateMember(userDict);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_GuildMemberUpdated");
            }
        });
    }
}

public class AuditLogMessageReceived : INotificationHandler<MessageReceivedNotification>
{
    public async Task Handle(MessageReceivedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (notification.Message.Author.IsBot || notification.Message.Author.IsWebhook) return;
                // Add to database
                var message = await new Mongo().GetMessage(notification.Message.Id);
                if (message == null)
                {
                    var guildMessage = notification.Message;
                    var newMessage = new BsonDocument
                    {
                        { "_id", guildMessage.Id.ToString() },
                        { "channel_id", guildMessage.Channel.Id.ToString() },
                        { "user_id", guildMessage.Author.Id.ToString() },
                        { "content", guildMessage.Content },
                        { "attachments", new BsonArray(guildMessage.Attachments.Select(attachment => attachment.Url)) },
                        { "created_at", notification.Message.CreatedAt.UtcDateTime },
                        { "edits", new BsonArray() }
                    };
                    await new Mongo().InsertMessage(newMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_MessageReceived");
            }
        });
    }
}

public class AuditLogMessageDeleted : INotificationHandler<MessageDeletedNotification>
{
    public async Task Handle(MessageDeletedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (!notification.Message.HasValue) return;
                if (notification.Message.Value.Author.IsBot || notification.Message.Value.Author.IsWebhook) return;
                var message = notification.Message.Value;
                // Print an embed
                var embeds = new List<Embed>();
                var embed = new EmbedBuilder()
                    .WithTitle($"Message Deleted in {message.Channel.Name}")
                    .WithUrl(message.GetJumpUrl())
                    .WithColor(Color.Red)
                    .WithAuthor($"{message.Author.Username} ({message.Author.Id})", message.Author.GetAvatarUrl())
                    .WithDescription(message.Content)
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();
                embeds.Add(embed);
                if (message.Attachments.Any())
                    foreach (var attachment in message.Attachments)
                        embeds.Add(new EmbedBuilder()
                            .WithTitle("Attachment")
                            .WithColor(Color.Red)
                            .WithAuthor($"{message.Author.Username} ({message.Author.Id})",
                                message.Author.GetAvatarUrl())
                            .WithDescription(attachment.Url)
                            .WithTimestamp(DateTime.UtcNow)
                            .Build()
                        );
                // Send the embeds
                var channel = (SocketGuildChannel)notification.Channel.Value;
                var settings = await new Mongo().GetBotSettings();
                await channel.Guild.GetTextChannel(Convert.ToUInt64(settings["log_channel_id"]))
                    .SendMessageAsync(embeds: embeds.ToArray());
                // Update the database
                var dbMessage = await new Mongo().GetMessage(notification.Message.Id);
                if (dbMessage != null)
                {
                    dbMessage["deleted_at"] = DateTime.UtcNow;
                    await new Mongo().UpdateMessage(dbMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_MessageDeleted");
            }
        });
    }
}

public class AuditLogMessageUpdated : INotificationHandler<MessageUpdatedNotification>
{
    public async Task Handle(MessageUpdatedNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (notification.NewMessage == null || notification.NewMessage.Author == null ||
                    notification.NewMessage.Author.IsBot || notification.NewMessage.Author.IsWebhook) return;
                if (notification.Channel == null) return;
                // Get the before and after states
                var after = notification.NewMessage;
                // Get the message from the database
                var dbMessage = await new Mongo().GetMessage(notification.NewMessage.Id);
                if (dbMessage == null) return;
                var updateLog = new BsonDocument
                {
                    { "timestamp", DateTime.UtcNow },
                    { "changes", new BsonDocument() }
                };
                // Print an embed
                var embed = new EmbedBuilder()
                    .WithTitle($"Message Updated in {after.Channel.Name}")
                    .WithUrl(after.GetJumpUrl())
                    .WithColor(Color.Orange)
                    .WithAuthor($"{after.Author.Username} ({after.Author.Id})", after.Author.GetAvatarUrl())
                    .WithTimestamp(DateTime.UtcNow);
                if (dbMessage["content"] != after.Content)
                    if (!string.IsNullOrEmpty(dbMessage["content"].ToString()) && !string.IsNullOrEmpty(after.Content))
                    {
                        embed.AddField("Content", dbMessage["content"]);
                        embed.AddField("Updated Content", after.Content);
                        updateLog["changes"]["content"] = after.Content;
                        dbMessage["content"] = after.Content;
                    }

                // Send the embed
                if (embed.Fields.Any())
                {
                    var channel = (SocketGuildChannel)notification.Channel;
                    var settings = await new Mongo().GetBotSettings();
                    await channel.Guild.GetTextChannel(Convert.ToUInt64(settings["log_channel_id"]))
                        .SendMessageAsync(embed: embed.Build());
                }

                // Update the database
                if (updateLog["changes"].AsBsonDocument.ElementCount > 0)
                {
                    if (!dbMessage.Contains("edits")) dbMessage["edits"] = new BsonArray();
                    dbMessage["edits"].AsBsonArray.Add(updateLog);
                    await new Mongo().UpdateMessage(dbMessage);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in AuditLog_MessageUpdated");
            }
        });
    }
}

//////////////////////////////////////////////////
// Perform a database sync once every 30 minutes//
//////////////////////////////////////////////////
public class AuditLogReadyHandler : INotificationHandler<ReadyNotification>
{
    public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var response = "Database Sync Started\n";
                    var timer = new Stopwatch();
                    timer.Start();
                    //Get values
                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    var guild = notification.Client.Guilds.FirstOrDefault();
                    var members = guild.Users;
                    var channels = guild.TextChannels.ToList();
                    var mongo = new Mongo();
                    response +=
                        $"**{timer.Elapsed.TotalSeconds}**\nMembers: {members.Count}\nChannels: {channels.Count}\n";
                    // Remove inactive channels
                    var activeChannels = new List<SocketTextChannel>();
                    foreach (var channel in channels)
                    {
                        //Get most recent message
                        var recentMessage = await channel.GetMessagesAsync(1).FirstOrDefaultAsync();
                        // If this is not within the cutoff, remove it
                        if (recentMessage.Any() && recentMessage.FirstOrDefault().CreatedAt > cutoff)
                            activeChannels.Add(channel);
                    }

                    response += $"**{timer.Elapsed.TotalSeconds}**\nActive Channels: {activeChannels.Count}\n";
                    // For each channel, get the last 1000 messages
                    var messageCount = 0;
                    foreach (var channel in activeChannels)
                    {
                        var messages = await channel.GetMessagesAsync(1000).FlattenAsync();
                        messages = messages.Where(x => !x.Author.IsBot && x.CreatedAt > cutoff);
                        var dbMessages = await mongo.GetMessagesByQuery(channel.Id.ToString(), cutoff);
                        messages = messages.Where(x => !dbMessages.Any(y => y["_id"].AsString == x.Id.ToString()));
                        foreach (var message in messages)
                            try
                            {
                                if (!message.Author.IsBot)
                                {
                                    // Check if the message is in the database, if not add it
                                    var dbMessage =
                                        dbMessages.FirstOrDefault(x => x["_id"].AsString == message.Id.ToString());
                                    var newMessage = new BsonDocument
                                    {
                                        { "_id", message.Id.ToString() },
                                        { "channel_id", message.Channel.Id.ToString() },
                                        { "user_id", message.Author.Id.ToString() },
                                        { "content", message.Content },
                                        {
                                            "attachments",
                                            new BsonArray(message.Attachments.Select(attachment => attachment.Url))
                                        },
                                        { "created_at", message.CreatedAt.UtcDateTime },
                                        { "edits", new BsonArray() }
                                    };
                                    await mongo.InsertMessage(newMessage);
                                    messageCount++;
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "Error in database sync");
                            }
                    }

                    response += $"**{timer.Elapsed.TotalSeconds}**\nMessages added to Database: {messageCount}\n";

                    // For each member, check if they are in the database, if not add them
                    var memberCount = 0;
                    var dbMembers = await mongo.GetAllMembers();
                    foreach (var member in members)
                        try
                        {
                            var dbMember = dbMembers.FirstOrDefault(x => x["_id"].AsString == member.Id.ToString());
                            if (dbMember == null)
                            {
                                var newMember = new BsonDocument
                                {
                                    { "_id", member.Id.ToString() },
                                    { "name", member.Username != null ? member.Username : BsonNull.Value },
                                    { "global_name", member.GlobalName != null ? member.GlobalName : BsonNull.Value },
                                    { "nick", member.Nickname != null ? member.Nickname : BsonNull.Value },
                                    { "roles", new BsonArray(member.Roles.Select(role => role.Id.ToString())) },
                                    {
                                        "joined_at",
                                        member.JoinedAt.HasValue ? member.JoinedAt.Value.UtcDateTime : BsonNull.Value
                                    },
                                    { "created_at", member.CreatedAt.UtcDateTime },
                                    { "edits", new BsonArray() }
                                };
                                await mongo.InsertMember(newMember);
                                memberCount++;
                            }
                            else
                            {
                                // Check if any value has changed
                                var hasChanged = false;
                                if ((dbMember["name"] == BsonNull.Value ? null : dbMember["name"]) != member.Username)
                                {
                                    dbMember["name"] = member.Username != null ? member.Username : BsonNull.Value;
                                    hasChanged = true;
                                }

                                if ((dbMember["global_name"] == BsonNull.Value ? null : dbMember["global_name"]) !=
                                    member.GlobalName)
                                {
                                    dbMember["global_name"] =
                                        member.GlobalName != null ? member.GlobalName : BsonNull.Value;
                                    hasChanged = true;
                                }

                                if ((dbMember["nick"] == BsonNull.Value ? null : dbMember["nick"]) != member.Nickname)
                                {
                                    dbMember["nick"] = member.Nickname != null ? member.Nickname : BsonNull.Value;
                                    hasChanged = true;
                                }

                                var newRoles = new BsonArray(member.Roles.Select(role => role.Id.ToString()));
                                if (!newRoles.SequenceEqual(dbMember["roles"].AsBsonArray))
                                {
                                    dbMember["roles"] = newRoles;
                                    hasChanged = true;
                                }

                                // If any value has changed, update the member in the database
                                if (hasChanged)
                                {
                                    await mongo.UpdateMember(dbMember);
                                    memberCount++;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "Error in database sync");
                        }

                    timer.Stop();
                    response += $"**{timer.Elapsed.TotalSeconds}**\nMembers added/updated to Database: {memberCount}\n";

                    // Log the response
                    Log.Information(response);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error in database sync");
                }

                // Wait 30 minutes
                await Task.Delay(1800000);
            }
        });
    }
}