using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using DougBot.Discord.Notifications;
using DougBot.Shared.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
                // Declare database context
                await using var db = new DougBotContext();
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
                var settings = await db.Botsettings.FirstOrDefaultAsync();
                await notification.User.Guild.GetTextChannel(Convert.ToUInt64(settings.LogChannelId))
                    .SendMessageAsync(embed: embed);
                // Save to database
                var member = await db.Members.FirstOrDefaultAsync(m => m.Id == notification.User.Id);
                if (member == null)
                {
                    var guildMember = notification.User;
                    var rolesList = guildMember.Roles.Select(role => role.Id).ToList();
                    var roleDecimals = rolesList.ConvertAll(x => (decimal)x);
                    await db.Members.AddAsync(new Member
                    {
                        Id = guildMember.Id,
                        Username = guildMember.Username,
                        GlobalName = guildMember.GlobalName,
                        Nickname = guildMember.Nickname,
                        Roles = roleDecimals,
                        JoinedAt = guildMember.JoinedAt.HasValue
                            ? guildMember.JoinedAt.Value.UtcDateTime
                            : DateTime.UtcNow,
                        CreatedAt = guildMember.CreatedAt.UtcDateTime
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_UserJoined");
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
                // declare database context
                await using var db = new DougBotContext();
                // Print an embed
                var embed = new EmbedBuilder()
                    .WithTitle("User Left")
                    .WithColor(Color.Red)
                    .WithAuthor($"{notification.User.Username} ({notification.User.Id})",
                        notification.User.GetAvatarUrl())
                    .WithTimestamp(DateTime.UtcNow)
                    .Build();
                var settings = await db.Botsettings.FirstOrDefaultAsync();
                await notification.Guild.GetTextChannel(Convert.ToUInt64(settings.LogChannelId))
                    .SendMessageAsync(embed: embed);
                // Update the database
                var memberUpdate = new MemberUpdate
                {
                    MemberId = notification.User.Id,
                    ColumnName = "left",
                    UpdateTimestamp = DateTime.UtcNow
                };
                await db.MemberUpdates.AddAsync(memberUpdate);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_UserLeft");
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
                // declare database context
                await using var db = new DougBotContext();
                // get the before and after states
                var after = notification.NewUser;
                // Load the user's dictionary
                var dbUser = await db.Members.FirstOrDefaultAsync(m => m.Id == after.Id);
                if (dbUser == null) return;
                // Load the embed
                var embed = new EmbedBuilder()
                    .WithTitle("Member Updated")
                    .WithAuthor($"{after.Username} ({after.Id})", after.GetAvatarUrl())
                    .WithColor(Color.Orange);
                // Get the roles
                var beforeRoles = dbUser.Roles.Select(r => r);
                var afterRoles = after.Roles.Select(role => (decimal)role.Id).ToHashSet();
                var addedRoles = afterRoles.Except(beforeRoles).ToList();
                var removedRoles = beforeRoles.Except(afterRoles).ToList();

                // Check each value, if it is changed add to embed and update database
                if (dbUser.Nickname != after.Nickname)
                {
                    embed.AddField("Old Nickname", string.IsNullOrEmpty(dbUser.Nickname) ? "None" : dbUser.Nickname);
                    embed.AddField("New Nickname", string.IsNullOrEmpty(after.Nickname) ? "None" : after.Nickname);
                    await db.MemberUpdates.AddAsync(new MemberUpdate
                    {
                        MemberId = after.Id,
                        ColumnName = "nickname",
                        PreviousValue = dbUser.Nickname,
                        NewValue = after.Nickname,
                        UpdateTimestamp = DateTime.UtcNow
                    });
                    dbUser.Nickname = after.Nickname;
                }

                if (dbUser.Username != after.Username)
                {
                    embed.AddField("Old Username", string.IsNullOrEmpty(dbUser.Username) ? "None" : dbUser.Username);
                    embed.AddField("New Username", string.IsNullOrEmpty(after.Username) ? "None" : after.Username);
                    dbUser.Username = after.Username;
                    await db.MemberUpdates.AddAsync(new MemberUpdate
                    {
                        MemberId = after.Id,
                        ColumnName = "username",
                        PreviousValue = dbUser.Username,
                        NewValue = after.Username,
                        UpdateTimestamp = DateTime.UtcNow
                    });
                }

                if (dbUser.GlobalName != after.GlobalName)
                {
                    embed.AddField("Old Global Name", string.IsNullOrEmpty(dbUser.GlobalName) ? "None" : dbUser.GlobalName);
                    embed.AddField("New Global Name", string.IsNullOrEmpty(after.GlobalName) ? "None" : after.GlobalName);
                    dbUser.GlobalName = after.GlobalName;
                    await db.MemberUpdates.AddAsync(new MemberUpdate
                    {
                        MemberId = after.Id,
                        ColumnName = "global_name",
                        PreviousValue = dbUser.GlobalName,
                        NewValue = after.GlobalName,
                        UpdateTimestamp = DateTime.UtcNow
                    });
                }

                if (addedRoles.Any() || removedRoles.Any())
                {
                    embed.AddField("Added Roles", addedRoles.Any() ? string.Join(" ", addedRoles.Select(r => $"<@&{r}>")) : "None");
                    embed.AddField("Removed Roles", removedRoles.Any() ? string.Join(" ", removedRoles.Select(r => $"<@&{r}>")) : "None");
                    dbUser.Roles = afterRoles.Select(x => Convert.ToDecimal(x)).ToList();
                    await db.MemberUpdates.AddAsync(new MemberUpdate
                    {
                        MemberId = after.Id,
                        ColumnName = "roles",
                        PreviousValue = string.Join(", ", beforeRoles),
                        NewValue = string.Join(", ", afterRoles),
                        UpdateTimestamp = DateTime.UtcNow
                    });
                }

                // Send the embed
                if (embed.Fields.Any())
                {
                    var settings = await db.Botsettings.FirstOrDefaultAsync();
                    await notification.NewUser.Guild.GetTextChannel(Convert.ToUInt64(settings.LogChannelId))
                        .SendMessageAsync(embed: embed.Build(), allowedMentions: AllowedMentions.None);
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_GuildMemberUpdated");
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
                // declare database context
                await using var db = new DougBotContext();
                // Add to database
                var dbMessage = await db.Messages.FirstOrDefaultAsync(m => m.Id == notification.Message.Id);
                if (dbMessage == null)
                {
                    var guildMessage = notification.Message;
                    await db.Messages.AddAsync(new Message
                    {
                        Id = guildMessage.Id,
                        ChannelId = guildMessage.Channel.Id,
                        MemberId = guildMessage.Author.Id,
                        Content = guildMessage.Content,
                        Attachments = guildMessage.Attachments.Select(attachment => attachment.Url).ToList(),
                        CreatedAt = guildMessage.CreatedAt.UtcDateTime
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_MessageReceived");
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
                // declare database context
                await using var db = new DougBotContext();
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
                var settings = await db.Botsettings.FirstOrDefaultAsync();
                await channel.Guild.GetTextChannel(Convert.ToUInt64(settings.LogChannelId))
                    .SendMessageAsync(embeds: embeds.ToArray());
                // Update the database
                await db.MessageUpdates.AddAsync(new MessageUpdate
                {
                    MessageId = message.Id,
                    ColumnName = "deleted",
                    UpdateTimestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_MessageDeleted");
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
                // declare database context
                await using var db = new DougBotContext();
                // Get the before and after states
                var after = notification.NewMessage;
                // Get the message from the database
                var dbMessage = await db.Messages.FirstOrDefaultAsync(m => m.Id == after.Id);
                if (dbMessage == null) return;
                // Print an embed
                var embed = new EmbedBuilder()
                    .WithTitle($"Message Updated in {after.Channel.Name}")
                    .WithUrl(after.GetJumpUrl())
                    .WithColor(Color.Orange)
                    .WithAuthor($"{after.Author.Username} ({after.Author.Id})", after.Author.GetAvatarUrl())
                    .WithTimestamp(DateTime.UtcNow);

                if (dbMessage.Content != after.Content)
                    if (!string.IsNullOrEmpty(dbMessage.Content) && !string.IsNullOrEmpty(after.Content))
                    {
                        embed.AddField("Content", dbMessage.Content);
                        embed.AddField("Updated Content", after.Content);

                        await db.MessageUpdates.AddAsync(new MessageUpdate
                        {
                            MessageId = after.Id,
                            ColumnName = "content",
                            PreviousValue = dbMessage.Content,
                            NewValue = after.Content,
                            UpdateTimestamp = DateTime.UtcNow
                        });
                        dbMessage.Content = after.Content;
                    }

                // Send the embed
                if (embed.Fields.Any())
                {
                    var channel = (SocketGuildChannel)notification.Channel;
                    var settings = await db.Botsettings.FirstOrDefaultAsync();
                    await channel.Guild.GetTextChannel(Convert.ToUInt64(settings.LogChannelId))
                        .SendMessageAsync(embed: embed.Build());
                }

                // Update the database
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Source}]", "AuditLog_MessageUpdated");
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
                    // declare database context
                    await using var db = new DougBotContext();
                    var response = "";
                    var timer = new Stopwatch();
                    timer.Start();
                    //Get values
                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    var guild = notification.Client.Guilds.FirstOrDefault();
                    var channels = guild.TextChannels.ToList();
                    response +=
                        $"**{timer.Elapsed.TotalSeconds}**\nMembers: {guild.Users.Count}\nChannels: {channels.Count}\n";
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
                        var dbMessages = await db.Messages.Where(m => m.ChannelId == channel.Id && m.CreatedAt > cutoff)
                            .ToListAsync();
                        messages = messages.Where(x => !dbMessages.Any(y => y.Id == x.Id));
                        foreach (var message in messages)
                            try
                            {
                                if (!message.Author.IsBot)
                                {
                                    await db.Messages.AddAsync(new Message
                                    {
                                        Id = message.Id,
                                        ChannelId = message.Channel.Id,
                                        MemberId = message.Author.Id,
                                        Content = message.Content,
                                        Attachments = message.Attachments.Select(attachment => attachment.Url).ToList(),
                                        CreatedAt = message.CreatedAt.UtcDateTime
                                    });
                                    messageCount++;
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Error(e, "[{Source}]", "Database Sync");
                            }
                    }

                    await db.SaveChangesAsync();

                    response += $"**{timer.Elapsed.TotalSeconds}**\nMessages added to Database: {messageCount}\n";

                    // For each member, check if they are in the database, if not add them
                    var memberCount = 0;
                    var dbMembers = await db.Members.ToListAsync();
                    foreach (var member in guild.Users)
                        try
                        {
                            var dbMember = dbMembers.FirstOrDefault(x => x.Id == member.Id);
                            if (dbMember == null)
                            {
                                var rolesList = member.Roles.Select(role => role.Id).ToList();
                                var roleDecimals = rolesList.ConvertAll(x => (decimal)x);
                                await db.Members.AddAsync(new Member
                                {
                                    Id = member.Id,
                                    Username = member.Username,
                                    GlobalName = member.GlobalName,
                                    Nickname = member.Nickname,
                                    Roles = roleDecimals,
                                    JoinedAt = member.JoinedAt.HasValue
                                        ? member.JoinedAt.Value.UtcDateTime
                                        : DateTime.UtcNow,
                                    CreatedAt = member.CreatedAt.UtcDateTime
                                });
                                memberCount++;
                            }
                            else
                            {
                                // Check if any value has changed
                                if (dbMember.Nickname != member.Nickname)
                                {
                                    dbMember.Nickname = member.Nickname;
                                    await db.MemberUpdates.AddAsync(new MemberUpdate
                                    {
                                        MemberId = member.Id,
                                        ColumnName = "nickname",
                                        PreviousValue = dbMember.Nickname,
                                        NewValue = member.Nickname,
                                        UpdateTimestamp = DateTime.UtcNow
                                    });
                                    memberCount++;
                                }

                                if (dbMember.Username != member.Username)
                                {
                                    dbMember.Username = member.Username;
                                    await db.MemberUpdates.AddAsync(new MemberUpdate
                                    {
                                        MemberId = member.Id,
                                        ColumnName = "username",
                                        PreviousValue = dbMember.Username,
                                        NewValue = member.Username,
                                        UpdateTimestamp = DateTime.UtcNow
                                    });
                                    memberCount++;
                                }

                                if (dbMember.GlobalName != member.GlobalName)
                                {
                                    dbMember.GlobalName = member.GlobalName;
                                    await db.MemberUpdates.AddAsync(new MemberUpdate
                                    {
                                        MemberId = member.Id,
                                        ColumnName = "global_name",
                                        PreviousValue = dbMember.GlobalName,
                                        NewValue = member.GlobalName,
                                        UpdateTimestamp = DateTime.UtcNow
                                    });
                                    memberCount++;
                                }

                                var beforeRoles = dbMember.Roles.Select(r => r);
                                var afterRoles = member.Roles.Select(role => (decimal)role.Id).ToHashSet();
                                var addedRoles = afterRoles.Except(beforeRoles).ToList();
                                var removedRoles = beforeRoles.Except(afterRoles).ToList();
                                if (addedRoles.Any() || removedRoles.Any())
                                {
                                    dbMember.Roles = afterRoles.Select(x => Convert.ToDecimal(x)).ToList();
                                    await db.MemberUpdates.AddAsync(new MemberUpdate
                                    {
                                        MemberId = member.Id,
                                        ColumnName = "roles",
                                        PreviousValue = string.Join(", ", beforeRoles),
                                        NewValue = string.Join(", ", afterRoles),
                                        UpdateTimestamp = DateTime.UtcNow
                                    });
                                    memberCount++;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e, "[{Source}]", "Database Sync");
                        }

                    await db.SaveChangesAsync();

                    timer.Stop();
                    response += $"**{timer.Elapsed.TotalSeconds}**\nMembers added/updated to Database: {memberCount}\n";

                    // Log the response
                    if (memberCount > 0 || messageCount > 0)
                        Log.Information("[{Source}] {Message}", "Database Sync", response);
                }
                catch (Exception e)
                {
                    Log.Error(e, "[{Source}]", "Database Sync");
                }

                // Wait 30 minutes
                await Task.Delay(1800000);
            }
        });
    }
}