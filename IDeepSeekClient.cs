using System.Collections.Generic;
using System.Threading.Tasks;
using DeepseekAPILib.Models;

namespace DeepseekAPILib
{
    public interface IDeepSeekClient
    {
        string ApiKey { get; set; }
        string BaseUrl { get; set; }
        int TimeoutSeconds { get; set; }

        Task<Models.CompletionResponse> SendCompletionAsync(Models.CompletionRequest request);
        Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request);

        // Streaming методы - возвращают IAsyncEnumerable для постепенного получения данных
        System.Collections.Generic.IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync(Models.ChatRequest request);
        System.Collections.Generic.IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(Models.CompletionRequest request);

        Task<string> SendCompletionSimpleAsync(string prompt, string model = "deepseek-coder", int maxTokens = 100, double temperature = 0.7);
        Task<string> SendChatSimpleAsync(List<ChatMessage> messages, string model = "deepseek-chat", int maxTokens = 500, double temperature = 0.7);
    }
}