// DeepSeekClientFactory.cs
using System;

namespace DeepseekAPILib
{
    public static class DeepSeekClientFactory
    {
        /// <summary>
        /// Создает оптимальный клиент для текущей системы
        /// </summary>
        public static IDeepSeekClient CreateClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = " ";

            // 1. Проверяем, может ли система использовать HttpClient
            if (ProtocolDetector.CanUseHttpClient())
            {
                try
                {
                    // Пробуем создать HttpClient клиент
                    var client = new DeepSeekAPI(apiKey);

                    // Можно выполнить быстрый тест (опционально)
                    // await client.TestConnectionAsync().ConfigureAwait(false);

                    return client;
                }
                catch (Exception ex) when (IsNetworkException(ex))
                {
                    // Если HttpClient не смог инициализироваться из-за сетевых/TLS ошибок,
                    // пробуем использовать Curl как fallback
                    System.Diagnostics.Debug.WriteLine($"[DeepSeekFactory] HttpClient failed, falling back to Curl: {ex.Message}");
                    return CreateCurlClient(apiKey);
                }
            }

            // 2. Система не поддерживает HttpClient - используем Curl
            return CreateCurlClient(apiKey);
        }

        /// <summary>
        /// Создает Curl клиент с обработкой ошибок
        /// </summary>
        private static IDeepSeekClient CreateCurlClient(string apiKey)
        {
            try
            {
                return new DeepSeekCurlClient(apiKey);
            }
            catch (Exception ex) when (ex is System.DllNotFoundException ||
                                      ex is System.EntryPointNotFoundException)
            {
                // Если libcurl не загрузился, пробуем HttpClient как последнюю надежду
                System.Diagnostics.Debug.WriteLine($"[DeepSeekFactory] Curl failed, trying HttpClient: {ex.Message}");

                try
                {
                    return new DeepSeekAPI(apiKey);
                }
                catch
                {
                    // Если и HttpClient не работает - бросаем исходную ошибку Curl
                    throw new NotSupportedException(
                        $"Не удалось инициализировать HTTP-клиент.\n" +
                        $"libcurl error: {ex.Message}\n" +
                        $"Проверьте:\n" +
                        $"1. Наличие libcurl-x64.dll в папке плагина\n" +
                        $"2. Включен ли TLS 1.2 в Windows (для Windows 8.1/Server 2012 R2)", ex);
                }
            }
        }

        /// <summary>
        /// Проверяет, является ли исключение сетевой/TLS ошибкой
        /// </summary>
        private static bool IsNetworkException(Exception ex)
        {
            return ex is System.Net.Http.HttpRequestException ||
                   ex is System.Net.WebException ||
                   ex is System.Security.Authentication.AuthenticationException ||
                   (ex.InnerException != null && IsNetworkException(ex.InnerException));
        }

        /// <summary>
        /// Создает клиент с принудительным выбором типа (для тестов/отладки)
        /// </summary>
        public static IDeepSeekClient CreateClient(string apiKey, bool forceCurl)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = " ";

            if (forceCurl)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepSeekFactory] Forcing Curl client");
                return new DeepSeekCurlClient(apiKey);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DeepSeekFactory] Forcing HttpClient");
                return new DeepSeekAPI(apiKey);
            }
        }

        /// <summary>
        /// Получает информацию о выбранном клиенте (для логов)
        /// </summary>
        public static string GetClientSelectionInfo(string apiKey)
        {
            bool canUseHttpClient = ProtocolDetector.CanUseHttpClient();
            var systemInfo = ProtocolDetector.GetSystemInfo();
            string clientType = canUseHttpClient ? "HttpClient" : "libcurl";

            return $"System: {systemInfo}\n" +
                   $"Selected client: {clientType}\n" +
                   $"API key: {(string.IsNullOrEmpty(apiKey) || apiKey == " " ? "Anonymous" : "Provided")}";
        }
    }
}