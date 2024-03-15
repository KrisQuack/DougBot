using Azure;
using Azure.AI.OpenAI;
using DougBot.Shared.Database;

namespace DougBot.Shared.OpenAI;

public class OpenAI
{
    private readonly OpenAIClient _client;
    public OpenAI()
    {
        // Get the settings
        using var db = new DougBotContext();
        var settings = db.Botsettings.FirstOrDefault();
        
        _client = new OpenAIClient(
            new Uri(settings.AiAzureEndpoint),
            new AzureKeyCredential(settings.AiApiKey));
    }
    
    public async Task<string> TicketSummary(string ticketString)
    {
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = "gpt-4-32k",
            Messages =
            {
                new ChatRequestSystemMessage("You are a bot who is designed to take in a chat history from a discord mod ticket and provide a summary of the ticket. Please ensure the summary is brief, a maximum of 1000 characters"),
                new ChatRequestUserMessage(ticketString),
            },
            MaxTokens = 1000,
        };
        var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
        var responseMessage = response.Value.Choices[0].Message;
        return responseMessage.Content;
    }
}