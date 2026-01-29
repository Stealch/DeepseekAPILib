// Services\DeepSeekCurlClient.cs
using DeepseekAPILib.Curl;
using DeepseekAPILib.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DeepseekAPILib
{
    public class DeepSeekCurlClient : BaseDeepSeekClient
    {
        private readonly CurlSession _session;

        // Статический конструктор для гарантированной загрузки libcurl
        static DeepSeekCurlClient()
        {
            LibCurlLoader.EnsureLoaded();
        }

        public DeepSeekCurlClient(string apiKey) : base(apiKey)
        {
            try
            {
                LibCurlLoader.EnsureLoaded(); // Может бросить DllNotFoundException

                _session = new CurlSession();
                ConfigureCurlSession();
            }
            catch (DllNotFoundException dllEx)
            {
                throw new Exception($"libcurl-x86.dll not found or failed to load: {dllEx.Message}", dllEx);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Curl client: {ex.Message}", ex);
            }
        }

        private void ConfigureCurlSession()
        {
            // Базовые настройки
            _session.SetTls12WithHttp2();
            _session.SetUserAgent("DeepSeekAPILib/1.0");
            _session.SetTimeouts(15, 60);

            // Заголовки
            _session.AddHeader("Content-Type: application/json");
            _session.AddHeader("Accept: application/json");

            if (!string.IsNullOrEmpty(ApiKey) && ApiKey != " ")
            {
                _session.AddHeader($"Authorization: Bearer {ApiKey}");
            }

            // CA bundle если найден
            var caBundlePath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "curl-ca-bundle.crt");

            if (System.IO.File.Exists(caBundlePath))
            {
                _session.SetCaBundle(caBundlePath);
            }
        }

        public override async Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request)
        {
            await Task.CompletedTask; // для async контекста

            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);

            try
            {
                _session.SetUrl($"{BaseUrl}/v1/chat/completions");
                _session.SetPostData(jsonData);
                _session.Perform();

                var response = _session.Response;

                if (string.IsNullOrEmpty(response))
                    throw new Models.DeepseekApiException("Empty response from server", 0);

                return JsonConvert.DeserializeObject<Models.ChatResponse>(response);
            }
            catch (Exception ex)
            {
                throw new Models.DeepseekApiException(
                    $"CURL request failed: {ex.Message}",
                    ex,
                    0);
            }
        }

        public override async Task<Models.CompletionResponse> SendCompletionAsync(
            Models.CompletionRequest request)
        {
            await Task.CompletedTask;

            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);

            try
            {
                _session.SetUrl($"{BaseUrl}/v1/completions");
                _session.SetPostData(jsonData);
                _session.Perform();

                var response = _session.Response;

                if (string.IsNullOrEmpty(response))
                    throw new Models.DeepseekApiException("Empty response from server", 0);

                return JsonConvert.DeserializeObject<Models.CompletionResponse>(response);
            }
            catch (Exception ex)
            {
                throw new Models.DeepseekApiException(
                    $"CURL request failed: {ex.Message}",
                    ex,
                    0);
            }
        }

        // ============ STREAMING METHODS ============
        #region STREAMING METHODS
        public override async IAsyncEnumerable<Models.StreamingChatChunk> SendChatStreamingAsync(
            Models.ChatRequest request)
        {
            request.Stream = true;
            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);

            // Инициализируем перед циклом
            _session.SetUrl($"{BaseUrl}/v1/chat/completions");
            _session.SetPostData(jsonData);

            // Запускаем streaming (ошибки здесь обрабатываются)
            try
            {
                await _session.StartStreamingAsync();
            }
            catch (Exception ex)
            {
                throw new Models.DeepseekApiException(
                    $"Failed to start streaming: {ex.Message}", ex, 0);
            }

            // Основной цикл БЕЗ try-catch
            await foreach (var chunkData in _session.ReadAllChunksAsync())
            {
                if (string.IsNullOrEmpty(chunkData))
                    continue;

                // Парсим SSE данные
                var sseLines = Utilities.SseParser.ParseSseData(chunkData);
                foreach (var json in sseLines)
                {
                    if (json == "[DONE]")
                        yield break;

                    if (Utilities.SseParser.TryParseJson<Models.StreamingChatChunk>(json,
                        out var chunk))
                    {
                        yield return chunk;
                    }
                }
            }
        }

        public override async IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(
            Models.CompletionRequest request)
        {
            request.Stream = true;
            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);

            _session.SetUrl($"{BaseUrl}/v1/completions");
            _session.SetPostData(jsonData);

            try
            {
                await _session.StartStreamingAsync();
            }
            catch (Exception ex)
            {
                throw new Models.DeepseekApiException(
                    $"Failed to start streaming: {ex.Message}", ex, 0);
            }

            await foreach (var chunkData in _session.ReadAllChunksAsync())
            {
                if (string.IsNullOrEmpty(chunkData))
                    continue;

                var sseLines = Utilities.SseParser.ParseSseData(chunkData);
                foreach (var json in sseLines)
                {
                    if (json == "[DONE]")
                        yield break;

                    if (Utilities.SseParser.TryParseJson<Models.StreamingCompletionChunk>(json,
                        out var chunk))
                    {
                        yield return chunk;
                    }
                }
            }
        }
        #endregion
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _session?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}