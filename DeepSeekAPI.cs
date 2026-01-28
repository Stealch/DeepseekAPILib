// DeepSeekAPI.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DeepseekAPILib.Models;

namespace DeepseekAPILib
{
    /// <summary>
    /// Client for Deepseek API using HttpClient
    /// </summary>
    public class DeepSeekAPI : BaseDeepSeekClient
    {
        private readonly HttpClient _httpClient;

        static DeepSeekAPI()
        {
            // Минимальная настройка TLS
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        /// <summary>
        /// Creates a new instance of DeepSeekAPI
        /// </summary>
        public DeepSeekAPI(string apiKey) : base(apiKey)
        {
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            // Простой HttpClientHandler - доверяем системе
            var handler = new HttpClientHandler
            {
                // Используем настройки системы по умолчанию
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    // Базовая проверка, доверяем системным сертификатам
                    return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                }
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds)
            };

            // Не устанавливаем BaseAddress, используем полные URL

            // Authorization header
            if (!string.IsNullOrEmpty(ApiKey) && ApiKey != " ")
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            }

            // Accept header
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );

            // User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeepSeekAPILib/1.0");
        }

        /// <summary>
        /// Creates a new instance with custom base URL
        /// </summary>
        public DeepSeekAPI(string apiKey, string baseUrl) : this(apiKey)
        {
            if (!string.IsNullOrEmpty(baseUrl))
                BaseUrl = baseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Sends a completion request
        /// </summary>
        public override async Task<Models.CompletionResponse> SendCompletionAsync(Models.CompletionRequest request)
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
        public override async Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (request.Messages == null || request.Messages.Count == 0)
                throw new ArgumentException("Messages cannot be null or empty", nameof(request.Messages));

            var jsonContent = JsonConvert.SerializeObject(request, _jsonSettings);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            string url = $"{BaseUrl}/v1/chat/completions";

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                await HandleErrorResponse(response);
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Models.ChatResponse>(responseJson);
        }

        /// <summary>
        /// Handles error responses from API
        /// </summary>
        private async Task HandleErrorResponse(HttpResponseMessage response)
        {
            var errorJson = await response.Content.ReadAsStringAsync();

            string errorMessage;
            string errorType = null;
            string errorCode = null;

            try
            {
                var apiError = JsonConvert.DeserializeObject<ApiError>(errorJson);
                if (apiError?.Error != null)
                {
                    errorMessage = apiError.Error.Message;
                    errorType = apiError.Error.Type;
                    errorCode = apiError.Error.Code;
                }
                else
                {
                    errorMessage = BuildDetailedErrorMessage(response, errorJson);
                }
            }
            catch
            {
                errorMessage = BuildDetailedErrorMessage(response, errorJson);
            }

            throw new DeepseekApiException(errorMessage, (int)response.StatusCode, errorType, errorCode);
        }

        private string BuildDetailedErrorMessage(HttpResponseMessage response, string errorJson)
        {
            var message = new StringBuilder();
            message.AppendLine($"Request to {response.RequestMessage.RequestUri} failed with {response.StatusCode}.");
            message.AppendLine($"Status code: {(int)response.StatusCode}");
            message.AppendLine($"Response: {errorJson}");
            message.Append($"Access mode: {(string.IsNullOrEmpty(ApiKey) ? "anonymous" : "with API key")}");

            return message.ToString();
        }

        /// <summary>
        /// Sends a streaming chat completion request
        /// </summary>
        public override async IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync(
            Models.ChatRequest request)
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
                        chunk = JsonConvert.DeserializeObject<StreamingChatChunk>(json);
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

        /// <summary>
        /// Sends a streaming completion request
        /// </summary>
        public override async IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(
            Models.CompletionRequest request)
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
                        chunk = JsonConvert.DeserializeObject<StreamingCompletionChunk>(json);
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

        /// <summary>
        /// Disposes the HttpClient
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}