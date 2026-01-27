using System.Threading;
using System.Threading.Tasks;

// OAuth\IOAuthProvider.cs

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// Интерфейс OAuth провайдера
    /// </summary>
    public interface IOAuthProvider
    {
        /// <summary>
        /// Имя провайдера
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Endpoint для авторизации
        /// </summary>
        string AuthorizationEndpoint { get; }

        /// <summary>
        /// Endpoint для получения токена
        /// </summary>
        string TokenEndpoint { get; }

        /// <summary>
        /// Client ID приложения
        /// </summary>
        string ClientId { get; }

        /// <summary>
        /// Скоупы (разрешения)
        /// </summary>
        string[] Scopes { get; }

        /// <summary>
        /// Использовать PKCE (Proof Key for Code Exchange)
        /// </summary>
        bool UsePkce { get; }

        /// <summary>
        /// Выполнить авторизацию
        /// </summary>
        Task<OAuthResult> AuthorizeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Обновить токен
        /// </summary>
        Task<OAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}