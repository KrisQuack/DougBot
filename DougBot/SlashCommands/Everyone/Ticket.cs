using Discord;
using Discord.Interactions;
using Serilog;

namespace DougBot.Discord.SlashCommands.Everyone;

[Group("ticket", "Ticket commands")]
[EnabledInDm(false)]
public class Ticket : InteractionModuleBase
{
    [SlashCommand("open", "Open a new ticket")]
    public async Task CreateTicket()
    {
        var modal = new TicketModal();
        await RespondWithModalAsync<TicketModal>("openticket");
    }

    [ModalInteraction("openticket", true)]
    public async Task TicketGenerate(TicketModal modal)
    {
        // Deffer the response as this may take a while
        await DeferAsync(true);
        // Get the base channel (staff-support)
        var baseChamnnel = await Context.Guild.GetTextChannelAsync(750154449275846666);
        // Create the ticket as a private thread
        var ticket = await baseChamnnel.CreateThreadAsync(modal.Name, ThreadType.PrivateThread,
            ThreadArchiveDuration.OneWeek, invitable: false);
        // Invite the user to the ticket
        var user = await Context.Guild.GetUserAsync(Context.User.Id);
        await ticket.AddUserAsync(user);
        // Send the ticket description
        var embed = new EmbedBuilder()
            .WithTitle($"Welcome to {ticket.Name}")
            .WithDescription(
                "Thanks for opening a ticket, one of the team will be with you as soon as possible, we are however a small team spanning many timezones so please be patient. Thank you for understanding.")
            .WithCurrentTimestamp()
            .Build();
        await ticket.SendMessageAsync(Context.User.Mention, embed: embed);
        // Send the ticket description
        await ticket.SendMessageAsync($".\n{user.DisplayName}: {modal.Description}");
        await ticket.SendMessageAsync("<@&750458206773444668>");
        // Respond to the user
        await FollowupAsync("Ticket created successfully", ephemeral: true);
        // Send a message to the staff channel with a button to join the ticket
        var staffChannel = await Context.Guild.GetTextChannelAsync(755155718214123600);
        var button = new ComponentBuilder().WithButton("Join Ticket", $"join_ticket:{ticket.Id}").Build();
        var staffEmbed = new EmbedBuilder()
            .WithTitle($"New ticket from {user.DisplayName}")
            .WithDescription("Click the button below to join the ticket")
            .WithCurrentTimestamp();
        staffEmbed.AddField("Title", modal.Name);
        staffEmbed.AddField("Description", modal.Description);
        await staffChannel.SendMessageAsync(embed: staffEmbed.Build(), components: button);
    }

    [ComponentInteraction("join_ticket:*", true)]
    public async Task JoinTicket(string ticketId)
    {
        // Get the ticket
        var ticket = await Context.Guild.GetThreadChannelAsync(ulong.Parse(ticketId));
        // Add the user to the ticket
        var guildUser = await Context.Guild.GetUserAsync(Context.User.Id);
        await ticket.AddUserAsync(guildUser);
        // Respond to the user
        await RespondAsync("You have been added to the ticket", ephemeral: true);
    }

    [SlashCommand("add_user", "Add a user to a ticket")]
    public async Task add_user([Summary(description: "The user to add to the ticket")] IUser user)
    {
        // Get the base channel (staff-support)
        var baseChannel = await Context.Guild.GetTextChannelAsync(750154449275846666);
        var thread = await Context.Guild.GetThreadChannelAsync(Context.Channel.Id);
        // Check this has been run in a thread and that it is of the base channel
        if (thread != null && thread.CategoryId == baseChannel.Id)
        {
            // Get guild user
            var guildUser = await Context.Guild.GetUserAsync(user.Id);
            // Add the user to the ticket
            await thread.AddUserAsync(guildUser);
            // Respond to the user
            await RespondAsync($"User {user.Mention} added to the ticket");
        }
        else
        {
            // Respond to the user
            await RespondAsync("This command can only be run in a ticket");
        }
    }

    [SlashCommand("remove_user", "Remove a user from a ticket")]
    public async Task remove_user([Summary(description: "The user to remove from the ticket")] IUser user)
    {
        // Get the base channel (staff-support)
        var baseChannel = await Context.Guild.GetTextChannelAsync(750154449275846666);
        var thread = await Context.Guild.GetThreadChannelAsync(Context.Channel.Id);
        // Check this has been run in a thread and that it is of the base channel
        if (thread != null && thread.CategoryId == baseChannel.Id)
        {
            // Get guild user
            var guildUser = await Context.Guild.GetUserAsync(user.Id);
            // Remove the user from the ticket
            await thread.RemoveUserAsync(guildUser);
            // Respond to the user
            await RespondAsync($"User {guildUser.DisplayName} removed from the ticket");
        }
        else
        {
            // Respond to the user
            await RespondAsync("This command can only be run in a ticket");
        }
    }

    [SlashCommand("close", "Close a ticket")]
    public async Task Close()
    {
        try
        {
            // Get the base channel (staff-support)
            var baseChannel = await Context.Guild.GetTextChannelAsync(750154449275846666);
            var thread = await Context.Guild.GetThreadChannelAsync(Context.Channel.Id);
            // Check this has been run in a thread and that it is of the base channel
            if (thread != null && thread.CategoryId == baseChannel.Id)
            {
                // Respond before closing
                await RespondAsync("Ticket closed", ephemeral: true);
                // Close the ticket
                await thread.ModifyAsync(properties => properties.Archived = true);
            }
            else
            {
                // Respond to the user
                await RespondAsync("This command can only be run in a ticket", ephemeral: true);
            }
        }
        catch (Exception e)
        {
            // Respond to the user
            await RespondAsync("An error occurred");
            Log.Error(e, "Error in close ticket");
        }
    }
}

public class TicketModal : IModal
{
    [ModalTextInput("name", TextInputStyle.Short, "Enter a title for the ticket", maxLength: 32)]
    public string Name { get; set; }

    [ModalTextInput("description", TextInputStyle.Paragraph, "Enter a description for the ticket", maxLength: 1000)]
    public string Description { get; set; }

    public string Title => "Ticket";
}