// Services\DeepSeekCurlClient.cs
using DeepseekAPILib.Curl;
using DeepseekAPILib.Models;
using DeepseekAPILib.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DeepseekAPILib
{
    public class DeepSeekCurlClient : BaseDeepSeekClient
    {
        private readonly CurlSession _session;
        private readonly string _tempCaBundlePath;

        static DeepSeekCurlClient()
        {
            try
            {
                Logger.Log("Static constructor: Loading libcurl...");
                LibCurlLoader.EnsureLoaded();
                Logger.Log("Static constructor: libcurl loaded");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "DeepSeekCurlClient static constructor");
                throw;
            }
        }

        public DeepSeekCurlClient(string apiKey) : base(apiKey)
        {
            try
            {
                Logger.Log("=== DeepSeekCurlClient constructor START ===");
                Logger.Log($"API key: {(string.IsNullOrEmpty(apiKey) ? "empty" : "provided")}");

                // Двойная проверка загрузки
                Logger.Log("Calling LibCurlLoader.EnsureLoaded()...");
                LibCurlLoader.EnsureLoaded();
                Logger.Log("LibCurlLoader.EnsureLoaded() OK");

                // Получаем путь к CA bundle
                Logger.Log("Getting CA bundle path...");
                _tempCaBundlePath = GetCaBundlePath();
                Logger.Log($"CA bundle path: {_tempCaBundlePath}");
                Logger.Log($"CA bundle exists: {File.Exists(_tempCaBundlePath)}");

                // Создаем сессию
                Logger.Log("Creating CurlSession...");
                _session = new CurlSession();
                Logger.Log("CurlSession created");

                // Настраиваем
                Logger.Log("Configuring session...");
                ConfigureCurlSession();

                Logger.Log("=== DeepSeekCurlClient constructor SUCCESS ===");
            }
            catch (DllNotFoundException dllEx)
            {
                Logger.LogError(dllEx, "DeepSeekCurlClient.DllNotFound");
                throw new Exception($"libcurl not found: {dllEx.Message}", dllEx);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "DeepSeekCurlClient.constructor");
                throw new Exception($"Failed to initialize Curl client: {ex.Message}", ex);
            }
        }

        private string GetCaBundlePath()
        {
            try
            {
                // Способ 1: Из временной папки LibCurlLoader через рефлексию
                Logger.Log("Trying to get CA bundle from LibCurlLoader...");

                var loaderType = typeof(LibCurlLoader);
                var tempFolderField = loaderType.GetField("_tempFolderPath",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (tempFolderField != null)
                {
                    var tempFolder = tempFolderField.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(tempFolder))
                    {
                        var caPath = Path.Combine(tempFolder, "curl-ca-bundle.crt");
                        Logger.Log($"Checking path from loader: {caPath}");

                        if (File.Exists(caPath))
                        {
                            Logger.Log($"Found CA bundle in temp folder");
                            return caPath;
                        }
                    }
                }

                // Способ 2: Рядом с DLL
                Logger.Log("Checking next to assembly...");
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var assemblyPath = Path.Combine(assemblyDir, "curl-ca-bundle.crt");

                if (File.Exists(assemblyPath))
                {
                    Logger.Log($"Found CA bundle next to assembly");
                    return assemblyPath;
                }

                // Способ 3: BaseDirectory
                Logger.Log("Checking BaseDirectory...");
                var baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "curl-ca-bundle.crt");

                if (File.Exists(baseDirPath))
                {
                    Logger.Log($"Found CA bundle in BaseDirectory");
                    return baseDirPath;
                }

                Logger.Log("WARNING: CA bundle not found anywhere");
                return baseDirPath; // Возвращаем путь даже если файла нет
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "GetCaBundlePath");
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "curl-ca-bundle.crt");
            }
        }

        private void ConfigureCurlSession()
        {
            try
            {
                Logger.Log("=== ConfigureCurlSession START ===");

                // TLS/HTTP2
                Logger.Log("Setting TLS 1.2 + HTTP/2...");
                _session.SetTls12WithHttp2();
                Logger.Log("TLS configured");

                // User Agent
                _session.SetUserAgent("DeepSeekAPILib/1.0");
                Logger.Log("UserAgent set");

                // Timeouts
                _session.SetTimeouts(15, 60);
                Logger.Log("Timeouts set: 15s connect, 60s transfer");

                // Headers
                _session.AddHeader("Content-Type: application/json");
                _session.AddHeader("Accept: application/json");
                Logger.Log("Content headers added");

                // Authorization
                if (!string.IsNullOrEmpty(ApiKey) && ApiKey != " ")
                {
                    _session.AddHeader($"Authorization: Bearer {ApiKey}");
                    Logger.Log("Authorization header added");
                }
                else
                {
                    Logger.Log("WARNING: No API key provided");
                }

                // CA Bundle - КРИТИЧЕСКИЙ МОМЕНТ
                Logger.Log("Configuring CA bundle...");
                if (!string.IsNullOrEmpty(_tempCaBundlePath))
                {
                    Logger.Log($"CA bundle path to check: {_tempCaBundlePath}");

                    if (File.Exists(_tempCaBundlePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(_tempCaBundlePath);
                            Logger.Log($"CA bundle file size: {fileInfo.Length} bytes, exists: {fileInfo.Exists}");

                            _session.SetCaBundle(_tempCaBundlePath);
                            Logger.Log($"CA bundle set successfully: {_tempCaBundlePath}");
                        }
                        catch (Exception caEx)
                        {
                            Logger.LogError(caEx, "Failed to set CA bundle");
                            Logger.Log("WARNING: Continuing without CA bundle");
                        }
                    }
                    else
                    {
                        Logger.Log($"WARNING: CA bundle file does not exist at: {_tempCaBundlePath}");
                        Logger.Log("WARNING: curl will use system certificates (if available)");
                    }
                }
                else
                {
                    Logger.Log("WARNING: No CA bundle path specified");
                }

                Logger.Log("=== ConfigureCurlSession COMPLETE ===");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "ConfigureCurlSession");
                throw;
            }
        }

        public override async Task<Models.ChatResponse> SendChatAsync(Models.ChatRequest request)
        {
            await Task.CompletedTask;

            Logger.Log($"SendChatAsync called with {request.Messages?.Count ?? 0} messages");

            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);
            Logger.Log($"Request JSON prepared, length: {jsonData.Length}");

            try
            {
                var url = $"{BaseUrl}/v1/chat/completions";
                Logger.Log($"Setting URL: {url}");
                _session.SetUrl(url);

                Logger.Log($"Setting POST data ({jsonData.Length} chars)");
                _session.SetPostData(jsonData);

                Logger.Log("Calling Perform()...");
                _session.Perform();
                Logger.Log("Perform() completed");

                var response = _session.Response;
                Logger.Log($"Response received, length: {response?.Length ?? 0}");

                if (string.IsNullOrEmpty(response))
                {
                    Logger.Log("ERROR: Empty response from server");
                    throw new Models.DeepseekApiException("Empty response from server", 0);
                }

                Logger.Log($"Response starts with: {response.Substring(0, Math.Min(100, response.Length))}...");
                return JsonConvert.DeserializeObject<Models.ChatResponse>(response);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SendChatAsync failed");
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

            Logger.Log($"SendCompletionAsync called");

            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);
            Logger.Log($"Completion request JSON length: {jsonData.Length}");

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
                Logger.LogError(ex, "SendCompletionAsync");
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
            Logger.Log("SendChatStreamingAsync starting...");

            request.Stream = true;
            var jsonData = JsonConvert.SerializeObject(request, _jsonSettings);
            Logger.Log($"Streaming request JSON length: {jsonData.Length}");

            _session.SetUrl($"{BaseUrl}/v1/chat/completions");
            _session.SetPostData(jsonData);

            try
            {
                Logger.Log("Starting streaming session...");
                await _session.StartStreamingAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "StartStreamingAsync failed");
                throw new Models.DeepseekApiException(
                    $"Failed to start streaming: {ex.Message}", ex, 0);
            }

            Logger.Log("Streaming started, reading chunks...");

            await foreach (var chunkData in _session.ReadAllChunksAsync())
            {
                if (string.IsNullOrEmpty(chunkData))
                    continue;

                var sseLines = Utilities.SseParser.ParseSseData(chunkData);
                foreach (var json in sseLines)
                {
                    if (json == "[DONE]")
                    {
                        Logger.Log("Received [DONE] signal, ending stream");
                        yield break;
                    }

                    if (Utilities.SseParser.TryParseJson<Models.StreamingChatChunk>(json, out var chunk))
                    {
                        yield return chunk;
                    }
                }
            }

            Logger.Log("Streaming completed");
        }

        public override async IAsyncEnumerable<Models.StreamingCompletionChunk> SendCompletionStreamingAsync(
            Models.CompletionRequest request)
        {
            Logger.Log("SendCompletionStreamingAsync starting...");

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
                Logger.LogError(ex, "StartStreamingAsync failed for completion");
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

                    if (Utilities.SseParser.TryParseJson<Models.StreamingCompletionChunk>(json, out var chunk))
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
                Logger.Log("Disposing DeepSeekCurlClient...");
                _session?.Dispose();
                Logger.Log("DeepSeekCurlClient disposed");
            }
            base.Dispose(disposing);
        }
    }
}