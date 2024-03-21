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
    
    public async Task<string> SummarizeChat(string chatString)
    {
        var chatCompletionsOptions = new ChatCompletionsOptions()
        {
            DeploymentName = "gpt-4-8k",
            Messages =
            {
                new ChatRequestSystemMessage("""
                                             You are a bot who is designed to take in a chat history from a discord channel and provide a summary of what people are discussing.
                                             Please ensure the summary is brief and bullet pointed by topic.
                                             Also end with an analysis of the sentiment of the conversation (If it is fine/aggressive/sexual/rude and so on.
                                             Try to keep a maximum of 1000 characters
                                             
                                             Format:
                                             - **Topic 1:** This is what people are discussing
                                             - **Topic 2:** This is what people are discussing
                                             
                                             **Sentiment:**
                                             This is the sentiment of the conversation
                                             """),
                new ChatRequestUserMessage(chatString),
            },
            MaxTokens = 1000,
        };
        var response = await _client.GetChatCompletionsAsync(chatCompletionsOptions);
        var responseMessage = response.Value.Choices[0].Message;
        return responseMessage.Content;
    }
}