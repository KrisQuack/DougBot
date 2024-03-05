using Discord;
using DougBot.Discord.Notifications;
using MediatR;
using Serilog;

namespace DougBot.Discord.Modules;

public class VerificationChecksReadyHandler : INotificationHandler<ReadyNotification>
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
                    var deletedMessages = 0;
                    // Get some values
                    var tenMinutesAgo = DateTime.UtcNow.AddMinutes(-10);
                    var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
                    var guild = notification.Client.Guilds.FirstOrDefault();
                    var onboardingChannel = guild.GetTextChannel(1041406855123042374);
                    var freshmanRole = guild.GetRole(935020318408462398);
                    var graduateRole = guild.GetRole(720807137319583804);
                    // Make sure the roles are not null
                    if (freshmanRole == null || graduateRole == null)
                    {
                        Log.Error("[{Source}] {Message}", "VerificationChecks_ReadyHandler", "Role not found");
                        return;
                    }

                    // Loop through all members in the server who do not have the member role
                    foreach (var member in guild.Users.Where(x => !x.IsBot))
                        // If they they do not have the freshman role
                        if (!member.Roles.Contains(freshmanRole))
                        {
                            // If the member has been in the server for more than 10 minutes
                            if (member.JoinedAt.Value.UtcDateTime < tenMinutesAgo)
                            {
                                // Kick the member for not being verified
                                await member.KickAsync("Not Verified");
                                kicked++;
                            }
                            else
                            {
                                // Send a reminder to the member to verify
                                await onboardingChannel.SendMessageAsync(
                                    $"{member.Mention} you have not yet verified and will be kicked in the next 5 minutes if not complete");
                            }
                        }
                        // If the member has been in the server for more than one week and does not have the member role
                        else if (member.JoinedAt.Value.UtcDateTime < oneWeekAgo && !member.Roles.Contains(graduateRole))
                        {
                            // Add the member role
                            await member.AddRoleAsync(graduateRole);
                            graduated++;
                        }

                    // Delete any messages in the onboarding channel older than 5 minutes
                    var messages = await onboardingChannel.GetMessagesAsync().FlattenAsync();
                    foreach (var message in messages)
                        if (message.Timestamp.UtcDateTime < tenMinutesAgo && message.Id != 1158902786524721212)
                            try
                            {
                                await message.DeleteAsync();
                                deletedMessages++;
                            }
                            catch
                            {
                            }

                    // Log the results
                    if (graduated > 0 || kicked > 0 || deletedMessages > 0)
                        Log.Information("[{Source}] {Message}", "Verification Checks",
                            $"Graduated: {graduated}\nKicked: {kicked}\nDeleted Messages: {deletedMessages}");
                }
                catch (Exception e)
                {
                    Log.Error(e, "[{Source}]",  "VerificationChecks_ReadyHandler");
                }

                // Delay 10 minutes
                await Task.Delay(600000);
            }
        });
    }
}