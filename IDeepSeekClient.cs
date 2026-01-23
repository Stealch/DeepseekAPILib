using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeepseekAPILib
{
    /// <summary>
    /// Interface for Deepseek API client
    /// </summary>
    public interface IDeepSeekClient
    {
        /// <summary>
        /// Gets or sets the API key
        /// </summary>
        string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the base URL
        /// </summary>
        string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the default timeout for requests
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// Sends a completion request
        /// </summary>
        /// <param name="request">Completion request</param>
        /// <returns>Completion response</returns>
        Task<Models.CompletionResponse> SendCompletionAsync(Models.CompletionRequest request);

        /// <summary>
        /// Sends a chat completion request
        /// </summary>
        /// <param name="request">Chat request</param>
        /// <returns>Chat response</returns>
        Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request);

        /// <summary>
        /// Sends a simple completion request
        /// </summary>
        /// <param name="prompt">Prompt text</param>
        /// <param name="model">Model name</param>
        /// <param name="maxTokens">Maximum tokens</param>
        /// <param name="temperature">Temperature</param>
        /// <returns>Generated text</returns>
        Task<string> SendCompletionSimpleAsync(string prompt, string model = "deepseek-coder", int maxTokens = 100, double temperature = 0.7);

        /// <summary>
        /// Sends a simple chat request
        /// </summary>
        /// <param name="messages">Chat messages</param>
        /// <param name="model">Model name</param>
        /// <param name="maxTokens">Maximum tokens</param>
        /// <param name="temperature">Temperature</param>
        /// <returns>Assistant's response</returns>
        Task<string> SendChatSimpleAsync(List<ChatMessage> messages, string model = "deepseek-chat", int maxTokens = 500, double temperature = 0.7);
    }
}