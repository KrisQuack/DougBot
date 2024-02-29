using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Shared;

namespace DougBot.Discord.SlashCommands.Everyone;

public class ReportCmd : InteractionModuleBase
{
    [MessageCommand("Report Message")]
    [EnabledInDm(false)]
    public async Task ReportMessage(IMessage message)
    {
        var channel = message.Channel as SocketGuildChannel;
        await RespondWithModalAsync<ReportModal>($"report:{message.Id}:0");
    }

    [UserCommand("Report User")]
    [EnabledInDm(false)]
    public async Task ReportUser(IGuildUser user)
    {
        await RespondWithModalAsync<ReportModal>($"report:0:{user.Id}");
    }

    [ModalInteraction("report:*:*", true)]
    public async Task ReportProcess(string messageId, string userId, ReportModal modal)
    {
        await RespondAsync("Submitting...", ephemeral: true);
        try
        {
            //Get the report
            var embeds = new List<EmbedBuilder>();
            var attachments = new List<string>();
            //If the message is a user report
            if (userId != "0")
            {
                var reportedUser = await Context.Guild.GetUserAsync(ulong.Parse(userId));
                embeds.Add(new EmbedBuilder()
                    .WithTitle("User Reported")
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("User Info")
                            .WithValue(
                                $"\nMention: {reportedUser.Mention}\nUsername: {reportedUser.Username}\nID: {reportedUser.Id}"),
                        new EmbedFieldBuilder()
                            .WithName("Reason")
                            .WithValue(modal.Reason)
                    )
                    .WithColor(Color.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"{Context.User.Username} ({Context.User.Id})")
                        .WithIconUrl(Context.User.GetAvatarUrl()))
                    .WithCurrentTimestamp());
            }
            //If the message is a message report
            else
            {
                var channel = Context.Channel as SocketTextChannel;
                var message = await channel.GetMessageAsync(ulong.Parse(messageId));
                embeds.Add(new EmbedBuilder()
                    .WithTitle("Message Reported")
                    .WithUrl(message.GetJumpUrl())
                    .WithFields(
                        new EmbedFieldBuilder()
                            .WithName("User Info")
                            .WithValue(
                                $"\nMention: {message.Author.Mention}\nUsername: {message.Author.Username}\nID: {message.Author.Id}"),
                        new EmbedFieldBuilder()
                            .WithName("Message Info")
                            .WithValue($"\nChannel: {channel.Mention}\nMessage: {message.Content}"),
                        new EmbedFieldBuilder()
                            .WithName("Reason")
                            .WithValue(modal.Reason)
                    )
                    .WithColor(Color.Red)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"{Context.User.Username} ({Context.User.Id})")
                        .WithIconUrl(Context.User.GetAvatarUrl()))
                    .WithCurrentTimestamp());
                //Attachments
                attachments = message.Attachments.Select(a => a.Url).ToList();
            }

            foreach (var attachment in attachments)
                embeds.Add(new EmbedBuilder()
                    .WithTitle("Attachment")
                    .WithImageUrl(attachment)
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp());
            // Get the channels to send the receipt to
            var settings = await new Mongo().GetBotSettings();
            var modChannel = await Context.Guild.GetTextChannelAsync(Convert.ToUInt64(settings[""]));
            await modChannel.SendMessageAsync(embeds: embeds.Select(e => e.Build()).ToArray());
            await ModifyOriginalResponseAsync(m => m.Content = "Your report has been sent to the mods.");
        }
        catch (Exception e)
        {
            await ModifyOriginalResponseAsync(m =>
                m.Content = "An error occured while submitting this request, Please try again or open a ticket.");
            throw;
        }
    }
}

public class ReportModal : IModal
{
    [ModalTextInput("reason", TextInputStyle.Paragraph,
        "Please explain what it is you are reporting and any details that may help us.")]
    public string Reason { get; set; }

    public string Title => "Generate Report";
}