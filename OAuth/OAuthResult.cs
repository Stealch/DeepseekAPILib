using System;

// OAuth\OAuthResult.cs

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// Результат OAuth авторизации
    /// </summary>
    public class OAuthResult
    {
        /// <summary>
        /// Access token от OAuth провайдера
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Refresh token (если поддерживается)
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// Время истечения токена
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Имя провайдера (Google, GitHub и т.д.)
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// Email пользователя
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Идентификатор пользователя
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Временный API ключ Deepseek (получается через прокси)
        /// </summary>
        public string DeepseekApiKey { get; set; }
    }
}