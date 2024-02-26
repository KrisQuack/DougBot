using Discord;
using DougBot.Discord.Notifications;
using DougBot.Discord.SlashCommands.Everyone;
using MediatR;
using Serilog;

namespace DougBot.Discord.Modules
{
    public class VerificationChecks_ReadyHandler : INotificationHandler<ReadyNotification>
    {
        public async Task Handle(ReadyNotification notification, CancellationToken cancellationToken)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // Counters
                        var graduated = 0;
                        var kicked = 0;
                        var deleted_messages = 0;
                        // Get some values
                        var ten_minutes_ago = DateTime.UtcNow.AddMinutes(-10);
                        var one_week_ago = DateTime.UtcNow.AddDays(-7);
                        var guild = notification.Client.Guilds.FirstOrDefault();
                        var onboarding_channel = guild.GetTextChannel(1041406855123042374);
                        var freshman_role = guild.GetRole(935020318408462398);
                        var graduate_role = guild.GetRole(720807137319583804);
                        // Make sure the roles are not null
                        if(freshman_role == null || graduate_role == null)
                        {
                            Log.Error("VerificationChecks_ReadyHandler: Roles not found");
                            return;
                        }
                        // Loop through all members in the server who do not have the member role
                        foreach (var member in guild.Users.Where(x => !x.IsBot))
                        {
                            // If they they do not have the freshman role
                            if(!member.Roles.Contains(freshman_role))
                            {
                                // If the member has been in the server for more than 10 minutes
                                if(member.JoinedAt.Value.UtcDateTime < ten_minutes_ago)
                                {
                                    // Kick the member for not being verified
                                    await member.KickAsync("Not Verified");
                                    kicked++;
                                }
                                else
                                {
                                    // Send a reminder to the member to verify
                                    await onboarding_channel.SendMessageAsync($"{member.Mention} you have not yet verified and will be kicked in the next 5 minutes if not complete");
                                }
                            }
                            // If the member has been in the server for more than one week and does not have the member role
                            else if (member.JoinedAt.Value.UtcDateTime < one_week_ago && !member.Roles.Contains(graduate_role))
                            {
                                // Add the member role
                                await member.AddRoleAsync(graduate_role);
                                graduated++;
                            }
                        }
                        // Delete any messages in the onboarding channel older than 5 minutes
                        var messages = await onboarding_channel.GetMessagesAsync(100).FlattenAsync();
                        foreach (var message in messages)
                        {
                            if(message.Timestamp.UtcDateTime < ten_minutes_ago && message.Id != 1158902786524721212)
                            {
                                try
                                {
                                    await message.DeleteAsync();
                                    deleted_messages++;
                                }
                                catch { }
                            }
                        }
                        // Log the results
                        if(graduated > 0 || kicked > 0 || deleted_messages > 0)
                        {
                            Log.Information($"**Verification Checks:**\nGraduated: {graduated}\nKicked: {kicked}\nDeleted Messages: {deleted_messages}");
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "VerificationChecks_ReadyHandler");
                    }
                    // Delay 10 minutes
                    await Task.Delay(600000);
                }
            });
        }
    }
}
