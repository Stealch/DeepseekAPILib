using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// Базовый класс для OAuth провайдеров
    /// </summary>
    public abstract class BaseOAuthProvider : IOAuthProvider
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _redirectUri;

        public abstract string Name { get; }
        public abstract string AuthorizationEndpoint { get; }
        public abstract string TokenEndpoint { get; }
        public abstract string ClientId { get; }
        public abstract string[] Scopes { get; }
        public virtual bool UsePkce => true;

        protected BaseOAuthProvider(string redirectUri)
        {
            _httpClient = new HttpClient();
            _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
        }

        /// <summary>
        /// Генерирует PKCE code verifier и challenge
        /// </summary>
        protected (string CodeVerifier, string CodeChallenge) GeneratePkceCodes()
        {
            var codeVerifier = GenerateRandomString(128);
            using var sha256 = SHA256.Create();
            var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            var codeChallenge = Base64UrlEncode(challengeBytes);

            return (codeVerifier, codeChallenge);
        }

        /// <summary>
        /// Создает URL для авторизации
        /// </summary>
        protected string BuildAuthorizationUrl(string codeChallenge = null)
        {
            var builder = new UriBuilder(AuthorizationEndpoint);
            var query = HttpUtility.ParseQueryString(builder.Query);

            query["client_id"] = ClientId;
            query["redirect_uri"] = _redirectUri;
            query["response_type"] = "code";
            query["scope"] = string.Join(" ", Scopes);

            if (UsePkce && !string.IsNullOrEmpty(codeChallenge))
            {
                query["code_challenge"] = codeChallenge;
                query["code_challenge_method"] = "S256";
            }

            query["state"] = GenerateRandomString(32);

            builder.Query = query.ToString();
            return builder.ToString();
        }

        /// <summary>
        /// Обменяет authorization code на access token
        /// </summary>
        protected async Task<OAuthResult> ExchangeCodeForTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken)
        {
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                new KeyValuePair<string, string>("grant_type", "authorization_code")
            });

            if (UsePkce)
            {
                requestContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", _redirectUri),
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code_verifier", codeVerifier)
                });
            }

            var response = await _httpClient.PostAsync(TokenEndpoint, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(json);
        }

        /// <summary>
        /// Парсит ответ с токеном
        /// </summary>
        protected virtual OAuthResult ParseTokenResponse(string json)
        {
            dynamic response = JsonConvert.DeserializeObject(json);

            return new OAuthResult
            {
                AccessToken = response.access_token,
                RefreshToken = response.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds((double)response.expires_in),
                Provider = Name
            };
        }

        /// <summary>
        /// Генерирует случайную строку
        /// </summary>
        private static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Base64Url кодирование
        /// </summary>
        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        public abstract Task<OAuthResult> AuthorizeAsync(CancellationToken cancellationToken = default);
        public abstract Task<OAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

        public virtual void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}