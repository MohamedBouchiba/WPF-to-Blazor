using System.ClientModel;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace Api.Ai;

public class AzureOpenAiClient : IAiClient
{
    private readonly ChatClient _chat;

    public AzureOpenAiClient(string endpoint, string apiKey, string deployment)
    {
        var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
        _chat = client.GetChatClient(deployment);
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var response = await _chat.CompleteChatAsync(messages, cancellationToken: ct);
        return response.Value.Content[0].Text;
    }
}
