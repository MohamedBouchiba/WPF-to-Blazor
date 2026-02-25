using OpenAI;
using OpenAI.Chat;

namespace Api.Ai;

public class OpenAiClient : IAiClient
{
    private readonly ChatClient _chat;

    public OpenAiClient(string apiKey, string model)
    {
        var client = new OpenAIClient(apiKey);
        _chat = client.GetChatClient(model);
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
