using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using DeepseekAPILib.Models;

namespace DeepseekAPILib
{
    /// <summary>
    /// Client for Deepseek API
    /// </summary>
    public class DeepSeekAPI : IDeepSeekClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>
        /// Gets or sets the API key
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the base URL
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.deepseek.com";

        /// <summary>
        /// Gets or sets the default timeout for requests in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Creates a new instance of DeepSeekAPI
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <exception cref="ArgumentException">Thrown when apiKey is null or empty</exception>
        public DeepSeekAPI(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

            ApiKey = apiKey;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };
        }

        /// <summary>
        /// Creates a new instance of DeepSeekAPI with custom base URL
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="baseUrl">Custom base URL</param>
        public DeepSeekAPI(string apiKey, string baseUrl) : this(apiKey)
        {
            if (!string.IsNullOrEmpty(baseUrl))
                BaseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Sends a completion request
        /// </summary>
        public async Task<Models.CompletionResponse> SendCompletionAsync(Models.CompletionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.Prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(request.Prompt));

            var jsonContent = JsonConvert.SerializeObject(request, _jsonSettings);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/v1/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Models.CompletionResponse>(responseJson);
        }

        /// <summary>
        /// Sends a chat completion request
        /// </summary>
        public async Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Messages == null || request.Messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty", nameof(request.Messages));

            var jsonContent = JsonConvert.SerializeObject(request, _jsonSettings);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{BaseUrl}/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Models.ChatResponse>(responseJson);
        }

        /// <summary>
        /// Sends a simple completion request
        /// </summary>
        public async Task<string> SendCompletionSimpleAsync(string prompt, string model = "deepseek-coder", int maxTokens = 100, double temperature = 0.7)
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

        /// <summary>
        /// Sends a simple chat request
        /// </summary>
        public async Task<string> SendChatSimpleAsync(List<ChatMessage> messages, string model = "deepseek-chat", int maxTokens = 500, double temperature = 0.7)
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

        /// <summary>
        /// Handles error responses from API
        /// </summary>
        private async Task HandleErrorResponse(HttpResponseMessage response)
        {
            var errorJson = await response.Content.ReadAsStringAsync();
            Models.ApiError apiError = null;

            try
            {
                apiError = JsonConvert.DeserializeObject<Models.ApiError>(errorJson);
            }
            catch
            {
                // If we can't parse the error JSON, use generic error
            }

            var errorMessage = apiError?.Error?.Message ?? $"API request failed with status code: {(int)response.StatusCode}";
            var errorType = apiError?.Error?.Type;
            var errorCode = apiError?.Error?.Code;

            throw new Models.DeepseekApiException(errorMessage, (int)response.StatusCode, errorType, errorCode);
        }

        /// <summary>
        /// Disposes the HttpClient
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}