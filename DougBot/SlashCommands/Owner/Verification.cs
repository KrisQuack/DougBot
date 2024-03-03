using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougBot.Shared;
using Serilog;

namespace DougBot.Discord.SlashCommands.Owner;

public class Verification : InteractionModuleBase
{
    [SlashCommand("verificationsetup", "Owner command to setup Verification")]
    [EnabledInDm(false)]
    [RequireOwner]
    public async Task Task()
    {
        // Generate the embed
        var embed = new EmbedBuilder()
            .WithDescription("""
                             # Welcome to The Doug District!
                             Welcome abroad! Before you dive in, we need to make sure you're a human. It's as easy as 1-2!
                             ## Step 1: Verification
                             Click the button below to embark on a mini quest where you'll be shown an image. Your mission is to **count the number of bell peppers in the image**. <:pepperCute:826963195242610700> Select the correct number below the image and you're one step closer to being *one of us!*
                             ## Step 2: Enrollment
                             Now that you're verified (and probably craving some stuffed peppers), it’s time to personalize your journey. Select the **Channels & Roles** menu atop the channels list or click right here: <id:customize> to pick the roles that tickle your fancy.
                             ## Encountering Issues?
                             If you encounter any issues, fear not! Direct Message the {self.client.user.mention} bot with any details or screenshots of the issue at hand. We'll assist as soon as we can!

                             Once you're all set, the realm of The Doug District is yours to explore.
                             """)
            .WithColor(Color.DarkPurple)
            .Build();
        // Create a button component
        var button = new ButtonBuilder()
            .WithLabel("Verify")
            .WithStyle(ButtonStyle.Primary)
            .WithEmote(Emote.Parse("✔️"))
            .WithCustomId("verify");
        // Send the message
        await Context.Channel.SendMessageAsync(embed: embed,
            components: new ComponentBuilder().WithButton(button).Build());
        await RespondAsync("Verification setup complete!", ephemeral: true);
    }

    [ComponentInteraction("verify", true)]
    public async Task Verify()
    {
        try
        {
            // Get all jpegs
            var jpegFiles = Directory.GetFiles("Data/Verify", "*.jpg");
            // Select a random jpeg
            var random = new Random();
            var jpeg = jpegFiles[random.Next(jpegFiles.Length)];
            // Get the number of bell peppers
            var peppers = jpeg.Split("_")[2].Split(".")[0];
            // Store the number of bell peppers in the user's data
            var user = await new Mongo().GetMember(Context.User.Id);
            if (user == null)
            {
                await RespondAsync(
                    $"An error occurred. Please try again later or message {Context.Client.CurrentUser.Mention}",
                    ephemeral: true);
                return;
            }

            user["verification"] = peppers;
            await new Mongo().UpdateMember(user);
            // Generate buttons 1-10
            var buttons = new List<ButtonBuilder>();
            for (var i = 1; i <= 10; i++)
                buttons.Add(new ButtonBuilder().WithLabel(i.ToString()).WithStyle(ButtonStyle.Primary)
                    .WithCustomId($"verify_answer_{i}"));
            // create the component
            var component = new ComponentBuilder();
            foreach (var button in buttons) component.WithButton(button);
            // reply with the image
            await RespondWithFileAsync(jpeg, $"{Context.User.Id}.jpg",
                "Count the number of bell peppers in the image below!", ephemeral: true, components: component.Build());
        }
        catch (Exception e)
        {
            Log.Error(e, "Error in Verification.verify");
        }
    }

    [ComponentInteraction("verify_answer_*", true)]
    public async Task verify_answer(string option)
    {
        await DeferAsync(true);
        // Get the number from the custom id
        var number = int.Parse(option);
        // Get the user's data
        var user = await new Mongo().GetMember(Context.User.Id);
        // If the number is correct
        if (user["verification"].AsString == number.ToString())
        {
            // Add the role
            var guild = Context.Guild;
            var role = guild.GetRole(935020318408462398);
            var guildUser = (SocketGuildUser)Context.User;
            await guildUser.AddRoleAsync(role);
            // If the user account is created less than one week, time out for the remainder of the week
            if (DateTime.Now - guildUser.CreatedAt < TimeSpan.FromDays(7))
            {
                var remaining = TimeSpan.FromDays(7) - (DateTime.Now - guildUser.CreatedAt);
                await FollowupAsync(
                    "You've been verified! Welcome to The Doug District! You're account is less than a week old, so you'll be able to chat in a week. In the meantime, feel free to explore the channels and pick your roles!",
                    ephemeral: true);
                await guildUser.SetTimeOutAsync(remaining);
            }
            else
            {
                await FollowupAsync(
                    "You've been verified! Welcome to The Doug District! Feel free to explore the channels and pick your roles!",
                    ephemeral: true);
            }
        }
        else
        {
            // Send the message
            await FollowupAsync("That's not the correct number of bell peppers. Try again!", ephemeral: true);
        }
    }
}