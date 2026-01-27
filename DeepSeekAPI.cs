using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

// DeepSeekAPI.cs

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
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            ApiKey = apiKey.Trim();

            _jsonSettings = new JsonSerializerSettings // ИНИЦИАЛИЗИРУЕМ здесь
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            };

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };

            // Добавляем Authorization header только если ключ не пустой
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            }

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Creates a new instance of DeepSeekAPI with custom base URL
        /// </summary>
        /// <param name="apiKey">API key for authentication</param>
        /// <param name="baseUrl">Custom base URL</param>
        public DeepSeekAPI(string apiKey, string baseUrl) : this(apiKey) // Вызываем основной конструктор
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

            // Проверяем наличие ответа
            if (response?.Choices == null || response.Choices.Count == 0)
                return string.Empty;

            // Безопасное получение контента
            var choice = response.Choices.FirstOrDefault();
            return choice?.Message?.Content?.Trim() ?? string.Empty;
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

            // Добавляем информацию о наличии ключа
            var authInfo = string.IsNullOrEmpty(ApiKey)
                ? " (anonymous access)"
                : " (with API key)";

            errorMessage += authInfo;

            throw new Models.DeepseekApiException(errorMessage, (int)response.StatusCode, errorType, errorCode);
        }
        /// <summary>
        /// Sends a streaming chat completion request
        /// </summary>
        public IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync(
            Models.ChatRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return SendChatStreamingAsync2();

            async IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync2()
            {
                request.Stream = true;

                var jsonContent = JsonConvert.SerializeObject(request, _jsonSettings);
                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync($"{BaseUrl}/v1/chat/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponse(response);
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var json = line.Substring(6);
                        if (json == "[DONE]")
                            yield break;

                        Models.StreamingChatChunk chunk;
                        try
                        {
                            chunk = JsonConvert.DeserializeObject<Models.StreamingChatChunk>(json);
                        }
                        catch (JsonException)
                        {
                            // Игнорируем некорректные JSON чанки
                            continue;
                        }

                        if (chunk != null)
                            yield return chunk;
                    }
                }
            }
        }

        /// <summary>
        /// Sends a streaming completion request
        /// </summary>
        public IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(
            Models.CompletionRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            return SendCompletionStreamingAsync2();

            async IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync2()
            {
                request.Stream = true;

                var jsonContent = JsonConvert.SerializeObject(request, _jsonSettings);
                using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync($"{BaseUrl}/v1/completions", content);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponse(response);
                    yield break;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    if (line.StartsWith("data: "))
                    {
                        var json = line.Substring(6);
                        if (json == "[DONE]")
                            yield break;

                        Models.StreamingCompletionChunk chunk;
                        try
                        {
                            chunk = JsonConvert.DeserializeObject<Models.StreamingCompletionChunk>(json);
                        }
                        catch (JsonException)
                        {
                            continue;
                        }

                        if (chunk != null)
                            yield return chunk;
                    }
                }
            }
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