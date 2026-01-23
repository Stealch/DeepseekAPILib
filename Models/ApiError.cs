using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


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

        public DeepseekApiException(string message, int statusCode, string errorType = null, string errorCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorType = errorType;
            ErrorCode = errorCode;
        }

        public DeepseekApiException(string message, Exception innerException, int statusCode)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
