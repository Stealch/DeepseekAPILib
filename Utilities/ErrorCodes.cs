using System;
using System.Collections.Generic;

namespace DeepseekAPILib.Utilities
{
    /// <summary>
    /// Типы ошибок DeepSeek API
    /// </summary>
    public enum ErrorType
    {
        Unknown,
        AuthenticationFailed,
        InsufficientBalance,
        InvalidApiKey,
        RateLimitExceeded,
        JsonParseError,
        ServerError,
        NetworkError,
        ModelNotFound,
        Timeout,
        TlsError,
        PermissionDenied
    }

    /// <summary>
    /// Структурированная информация об ошибке
    /// </summary>
    public class ErrorInfo
    {
        public ErrorType Type { get; set; }
        public string FriendlyMessage { get; set; }
        public string Recommendations { get; set; }
        public bool ShowToUser { get; set; }
        public int HttpStatusCode { get; set; }

        public ErrorInfo(ErrorType type, string friendlyMessage, string recommendations, bool showToUser = true, int httpStatusCode = 0)
        {
            Type = type;
            FriendlyMessage = friendlyMessage;
            Recommendations = recommendations;
            ShowToUser = showToUser;
            HttpStatusCode = httpStatusCode;
        }
    }

    /// <summary>
    /// Коды и описания ошибок DeepSeek API
    /// </summary>
    public static class ErrorCodes
    {
        private static readonly Dictionary<ErrorType, ErrorInfo> _errorRegistry = new Dictionary<ErrorType, ErrorInfo>
        {
            [ErrorType.AuthenticationFailed] = new ErrorInfo(
                ErrorType.AuthenticationFailed,
                "Ошибка аутентификации",
                "1. Проверьте API ключ в настройках (Tools → Options → DeepSeek)\n2. Убедитесь, что ключ не истек\n3. Сгенерируйте новый ключ на platform.deepseek.com",
                true,
                401
            ),

            [ErrorType.InsufficientBalance] = new ErrorInfo(
                ErrorType.InsufficientBalance,
                "Недостаточно средств на счету",
                "1. Перейдите на platform.deepseek.com\n2. Нажмите 'Billing' или 'Пополнить'\n3. Добавьте средства на баланс",
                true,
                402
            ),

            [ErrorType.InvalidApiKey] = new ErrorInfo(
                ErrorType.InvalidApiKey,
                "Недействительный API ключ",
                "1. Сгенерируйте новый ключ на platform.deepseek.com/api_keys\n2. Вставьте его в настройки плагина\n3. Нажмите 'Test Connection' для проверки",
                true,
                401
            ),

            [ErrorType.RateLimitExceeded] = new ErrorInfo(
                ErrorType.RateLimitExceeded,
                "Превышен лимит запросов",
                "1. Подождите 1-2 минуты\n2. Уменьшите частоту запросов\n3. Проверьте лимиты на platform.deepseek.com",
                true,
                429
            ),

            [ErrorType.JsonParseError] = new ErrorInfo(
                ErrorType.JsonParseError,
                "Ошибка формата данных",
                "Перезапустите Visual Studio. Если ошибка повторяется, сообщите разработчику.",
                false, // Техническая ошибка - не показывать пользователю
                400
            ),

            [ErrorType.ServerError] = new ErrorInfo(
                ErrorType.ServerError,
                "Ошибка сервера DeepSeek",
                "1. Подождите несколько минут\n2. Проверьте status.deepseek.com\n3. Попробуйте позже",
                true,
                500
            ),

            [ErrorType.NetworkError] = new ErrorInfo(
                ErrorType.NetworkError,
                "Проблема с подключением",
                "1. Проверьте интернет-соединение\n2. Отключите VPN/прокси если используются\n3. Проверьте файрвол",
                true,
                0
            ),

            [ErrorType.Timeout] = new ErrorInfo(
                ErrorType.Timeout,
                "Таймаут соединения",
                "1. Увеличьте таймаут в настройках\n2. Проверьте скорость интернета\n3. Попробуйте позже",
                true,
                408
            ),

            [ErrorType.TlsError] = new ErrorInfo(
                ErrorType.TlsError,
                "Проблема с безопасным соединением",
                "Для Windows 7/8 используется встроенный обходной путь. Если ошибка повторяется:\n1. Обновите Windows\n2. Установите корневые сертификаты",
                true,
                0
            ),

            [ErrorType.PermissionDenied] = new ErrorInfo(
                ErrorType.PermissionDenied,
                "Недостаточно прав",
                "1. Проверьте разрешения API ключа\n2. Сгенерируйте ключ с нужными правами\n3. Обратитесь в поддержку DeepSeek",
                true,
                403
            )
        };

        /// <summary>
        /// Определить тип ошибки по сообщению
        /// </summary>
        public static ErrorType DetectErrorType(string errorMessage, int statusCode = 0)
        {
            if (string.IsNullOrEmpty(errorMessage))
                return ErrorType.Unknown;

            string lowerError = errorMessage.ToLowerInvariant();

            // Определяем по статус коду
            switch (statusCode)
            {
                case 401:
                case 403:
                    return ErrorType.AuthenticationFailed;
                case 402:
                    return ErrorType.InsufficientBalance;
                case 429:
                    return ErrorType.RateLimitExceeded;
                case 500:
                case 502:
                case 503:
                    return ErrorType.ServerError;
                case 408:
                    return ErrorType.Timeout;
                case 400:
                    if (lowerError.Contains("json") || lowerError.Contains("parse"))
                        return ErrorType.JsonParseError;
                    break;
            }

            // Определяем по тексту ошибки
            if (lowerError.Contains("authentication") || lowerError.Contains("auth") ||
                lowerError.Contains("governor") || lowerError.Contains("invalid api key"))
                return ErrorType.AuthenticationFailed;

            if (lowerError.Contains("balance") || lowerError.Contains("insufficient"))
                return ErrorType.InsufficientBalance;

            if (lowerError.Contains("rate limit") || lowerError.Contains("too many requests"))
                return ErrorType.RateLimitExceeded;

            if (lowerError.Contains("json") || lowerError.Contains("parse") ||
                lowerError.Contains("deserialize"))
                return ErrorType.JsonParseError;

            if (lowerError.Contains("server") || lowerError.Contains("internal error"))
                return ErrorType.ServerError;

            if (lowerError.Contains("timeout") || lowerError.Contains("timed out"))
                return ErrorType.Timeout;

            if (lowerError.Contains("tls") || lowerError.Contains("ssl") ||
                lowerError.Contains("certificate"))
                return ErrorType.TlsError;

            if (lowerError.Contains("permission") || lowerError.Contains("access denied"))
                return ErrorType.PermissionDenied;

            if (lowerError.Contains("network") || lowerError.Contains("connection") ||
                lowerError.Contains("socket"))
                return ErrorType.NetworkError;

            return ErrorType.Unknown;
        }

        /// <summary>
        /// Получить структурированную информацию об ошибке
        /// </summary>
        public static ErrorInfo GetErrorInfo(ErrorType errorType)
        {
            if (_errorRegistry.TryGetValue(errorType, out var info))
                return info;

            // Возвращаем информацию для неизвестной ошибки
            return new ErrorInfo(
                ErrorType.Unknown,
                $"Неизвестная ошибка",
                "1. Проверьте подключение к интернету\n2. Перезапустите Visual Studio\n3. Если ошибка повторяется, сообщите разработчику",
                true,
                0
            );
        }

        /// <summary>
        /// Получить информацию об ошибке по сообщению
        /// </summary>
        public static ErrorInfo GetErrorInfo(string errorMessage, int statusCode = 0)
        {
            var errorType = DetectErrorType(errorMessage, statusCode);
            return GetErrorInfo(errorType);
        }

        /// <summary>
        /// Получить информацию об ошибке из исключения
        /// </summary>
        public static ErrorInfo GetErrorInfo(Exception exception)
        {
            if (exception == null)
                return GetErrorInfo(ErrorType.Unknown);

            string errorMessage = exception.Message;
            int statusCode = 0;

            // Если это DeepseekApiException, получаем статус код
            if (exception is Models.DeepseekApiException apiEx)
            {
                statusCode = apiEx.StatusCode;
                errorMessage = apiEx.Message;
            }

            return GetErrorInfo(errorMessage, statusCode);
        }
    }
}