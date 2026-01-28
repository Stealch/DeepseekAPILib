// Models\ApiError.cs
using System;

namespace DeepseekAPILib.Models
{
    /// <summary>
    /// API error response
    /// </summary>
    public class ApiError
    {
        /// <summary>
        /// Error message
        /// </summary>
        public ErrorDetail Error { get; set; }
    }

    /// <summary>
    /// Error detail
    /// </summary>
    public class ErrorDetail
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Error type
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        public string Code { get; set; }
    }

    /// <summary>
    /// Тип HTTP клиента
    /// </summary>
    public enum HttpClientType
    {
        Unknown = 0,
        HttpClient = 1,
        Curl = 2
    }

    /// <summary>
    /// Детали ошибки для curl
    /// </summary>
    public class CurlErrorDetails
    {
        /// <summary>
        /// Код ошибки curl
        /// </summary>
        public int CurlErrorCode { get; set; }

        /// <summary>
        /// Описание ошибки curl
        /// </summary>
        public string CurlErrorMessage { get; set; }

        /// <summary>
        /// URL запроса
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Была ли попытка retry
        /// </summary>
        public bool WasRetried { get; set; }
    }

    /// <summary>
    /// Exception thrown when API request fails
    /// </summary>
    public class DeepseekApiException : Exception
    {
        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Error type
        /// </summary>
        public string ErrorType { get; }

        /// <summary>
        /// Error code
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Тип HTTP клиента, вызвавшего ошибку
        /// </summary>
        public HttpClientType ClientType { get; }

        /// <summary>
        /// Детали ошибки для curl (если применимо)
        /// </summary>
        public CurlErrorDetails CurlDetails { get; }

        // ============ КОНСТРУКТОРЫ ДЛЯ HttpClient ============

        public DeepseekApiException(string message, int statusCode, string errorType = null, string errorCode = null)
            : this(message, statusCode, errorType, errorCode, HttpClientType.HttpClient, null)
        {
        }

        public DeepseekApiException(string message, Exception innerException, int statusCode)
            : this(message, innerException, statusCode, HttpClientType.HttpClient, null)
        {
        }

        // ============ КОНСТРУКТОРЫ ДЛЯ CURL ============

        public DeepseekApiException(string message, int statusCode, CurlErrorDetails curlDetails)
            : this(message, statusCode, "CurlError", null, HttpClientType.Curl, curlDetails)
        {
        }

        public DeepseekApiException(string message, Exception innerException, int statusCode,
            CurlErrorDetails curlDetails)
            : this(message, innerException, statusCode, HttpClientType.Curl, curlDetails)
        {
        }

        // ============ ОСНОВНЫЕ КОНСТРУКТОРЫ ============

        private DeepseekApiException(string message, int statusCode, string errorType, string errorCode,
            HttpClientType clientType, CurlErrorDetails curlDetails)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
            ErrorCode = errorCode;
            ClientType = clientType;
            CurlDetails = curlDetails;
        }

        private DeepseekApiException(string message, Exception innerException, int statusCode,
            HttpClientType clientType, CurlErrorDetails curlDetails)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ClientType = clientType;
            CurlDetails = curlDetails;
        }

        // ============ БАЗОВЫЕ КОНСТРУКТОРЫ Exception ============

        public DeepseekApiException() : base()
        {
            ClientType = HttpClientType.Unknown;
        }

        public DeepseekApiException(string message) : base(message)
        {
            ClientType = HttpClientType.Unknown;
        }

        public DeepseekApiException(string message, Exception innerException) : base(message, innerException)
        {
            ClientType = HttpClientType.Unknown;
        }

        // ============ ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ============

        /// <summary>
        /// Получает детальное описание ошибки
        /// </summary>
        public string GetDetailedMessage()
        {
            var details = new System.Text.StringBuilder();
            details.AppendLine($"Message: {Message}");
            details.AppendLine($"Status Code: {StatusCode}");
            details.AppendLine($"Client Type: {ClientType}");

            if (!string.IsNullOrEmpty(ErrorType))
                details.AppendLine($"Error Type: {ErrorType}");

            if (!string.IsNullOrEmpty(ErrorCode))
                details.AppendLine($"Error Code: {ErrorCode}");

            if (CurlDetails != null)
            {
                details.AppendLine($"CURL Error Code: {CurlDetails.CurlErrorCode}");
                details.AppendLine($"CURL Error: {CurlDetails.CurlErrorMessage}");
                details.AppendLine($"URL: {CurlDetails.Url}");
            }

            if (InnerException != null)
                details.AppendLine($"Inner Exception: {InnerException.Message}");

            return details.ToString();
        }

        /// <summary>
        /// Создает CurlErrorDetails из кода ошибки curl
        /// </summary>
        public static CurlErrorDetails CreateCurlError(int curlErrorCode, string url, bool wasRetried = false)
        {
            return new CurlErrorDetails
            {
                CurlErrorCode = curlErrorCode,
                CurlErrorMessage = GetCurlErrorMessage(curlErrorCode),
                Url = url,
                WasRetried = wasRetried
            };
        }

        private static string GetCurlErrorMessage(int curlErrorCode)
        {
            // Базовые коды ошибок curl
            return curlErrorCode switch
            {
                6 => "Couldn't resolve host",
                7 => "Failed to connect to host",
                28 => "Operation timeout",
                35 => "SSL connect error",
                56 => "Failure in receiving network data",
                _ => $"CURL error code: {curlErrorCode}"
            };
        }
    }
}