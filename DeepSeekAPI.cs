using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DeepseekAPILib.Models;

// DeepSeekAPI.cs

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
            // Устанавливаем TLS 1.2 и TLS 1.3 глобально
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        }

        /// <summary>
        /// Tests the API connection
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var testRequest = new ChatRequest
                {
                    Model = "deepseek-chat",
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage("user", "Hello")
                    },
                    MaxTokens = 1 // Минимальный запрос для теста
                };

                var response = await SendChatAsync(testRequest);
                return response?.Choices != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets detailed connection info
        /// </summary>
        public string GetConnectionInfo()
        {
            return $"BaseUrl: {BaseUrl}, " +
                   $"HasApiKey: {!string.IsNullOrEmpty(ApiKey)}, " +
                   $"KeyLength: {ApiKey?.Length ?? 0}, " +
                   $"Timeout: {TimeoutSeconds}s";
        }

        /// <summary>
        /// Creates a new instance of DeepSeekAPI
        /// </summary>
        public DeepSeekAPI(string apiKey) : base(apiKey)
        {
            if (apiKey == null)
                throw new ArgumentNullException(nameof(apiKey));

            // ========== НАЧАЛО: КРИПТОГРАФИЧЕСКИЕ НАСТРОЙКИ ==========
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            try
            {
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.CheckCertificateRevocationList = false;
                ServicePointManager.DefaultConnectionLimit = 9999;
            }
            catch { /* Игнорируем ошибки, если не поддерживается */ }
            // ========== КОНЕЦ: КРИПТОГРАФИЧЕСКИЕ НАСТРОЙКИ ==========

            // Настройка HttpClientHandler
            var handler = new HttpClientHandler
            {
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12 |
                              System.Security.Authentication.SslProtocols.Tls13,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SSL] Certificate error: {sslPolicyErrors}");
                    }
                    return true;
                },
                UseDefaultCredentials = false,
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                        System.Net.DecompressionMethods.Deflate
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
                BaseAddress = new Uri(BaseUrl)
            };

            // Добавляем Authorization header только если ключ не пустой
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");
            }

            // Accept header для JSON API
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
            );

            // User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DeepSeekAPILib/1.0");
        }

        /// <summary>
        /// Creates a new instance of DeepSeekAPI with custom base URL
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

            try
            {
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponse(response);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<Models.ChatResponse>(responseJson);
            }
            catch (HttpRequestException httpEx)
            {
                var errorDetails = new StringBuilder();
                errorDetails.AppendLine($"HTTP Request failed to: {url}");
                errorDetails.AppendLine($"TLS Protocols: {ServicePointManager.SecurityProtocol}");
                errorDetails.AppendLine($"Has API Key: {!string.IsNullOrEmpty(ApiKey)}");

                if (httpEx.InnerException is System.Net.WebException webEx)
                {
                    errorDetails.AppendLine($"WebException Status: {webEx.Status}");

                    if (webEx.Status == WebExceptionStatus.SecureChannelFailure)
                    {
                        errorDetails.AppendLine($"SECURE CHANNEL FAILURE - TLS 1.2 may not be enabled.");
                        errorDetails.AppendLine($"Current protocols: {ServicePointManager.SecurityProtocol}");
                    }
                }

                throw new HttpRequestException(errorDetails.ToString(), httpEx);
            }
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
            message.AppendLine($"Запрос к {response.RequestMessage.RequestUri} завершился с ошибкой {response.StatusCode}.");
            message.AppendLine($"Код статуса: {(int)response.StatusCode}");
            message.AppendLine($"Тело ответа: {errorJson}");
            message.Append($"Режим доступа: {(string.IsNullOrEmpty(ApiKey) ? "анонимный" : "с API ключом")}");

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