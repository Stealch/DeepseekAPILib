// Services\BaseDeepSeekClient.cs
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeepseekAPILib
{
    public abstract class BaseDeepSeekClient : IDeepSeekClient, IDisposable
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.deepseek.com";
        public int TimeoutSeconds { get; set; } = 30;

        protected readonly JsonSerializerSettings _jsonSettings;

        protected BaseDeepSeekClient(string apiKey)
        {
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            };
        }

        // Абстрактные методы (реализация в наследниках)
        public abstract Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request);
        public abstract Task<Models.CompletionResponse> SendCompletionAsync(Models.CompletionRequest request);
        public abstract IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync(Models.ChatRequest request);
        public abstract IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(Models.CompletionRequest request);

        // Общие методы (реализация в базовом классе)
        public virtual async Task<string> SendChatSimpleAsync(
            List<ChatMessage> messages,
            string model = "deepseek-chat",
            int maxTokens = 500,
            double temperature = 0.7)
        {
            var request = new Models.ChatRequest
            {
                Model = model,
                Messages = messages,
                MaxTokens = maxTokens,
                Temperature = temperature
            };

            var response = await SendChatAsync(request);
            return response?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;
        }

        public virtual async Task<string> SendCompletionSimpleAsync(
            string prompt,
            string model = "deepseek-coder",
            int maxTokens = 100,
            double temperature = 0.7)
        {
            var request = new Models.CompletionRequest
            {
                Model = model,
                Prompt = prompt,
                MaxTokens = maxTokens,
                Temperature = temperature
            };

            var response = await SendCompletionAsync(request);
            return response?.Choices?.FirstOrDefault()?.Text?.Trim() ?? string.Empty;
        }

        public virtual void Dispose()
        {
            // Базовая реализация
        }
        protected virtual void Dispose(bool disposing)
        {
            // Базовая реализация ничего не делает
            // Наследники переопределяют при необходимости
        }
    }
}