using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// OAuth сервис с использованием системного браузера
    /// </summary>
    public class SystemBrowserOAuthService : IOAuthProvider, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientSecret;
        private readonly string _redirectUri;
        private HttpListener _httpListener;
        private int _localPort;

        // Реализация интерфейса IOAuthProvider
        public string Name { get; private set; }
        public string AuthorizationEndpoint { get; private set; }
        public string TokenEndpoint { get; private set; }
        public string ClientId { get; } // Добавить эту строку
        public string[] Scopes { get; private set; }
        public bool UsePkce { get; private set; }

        public SystemBrowserOAuthService(string provider, string clientId, string redirectUri, string clientSecret = null)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret;
            _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
            _httpClient = new HttpClient();

            InitializeProvider(provider);
        }

        private void InitializeProvider(string provider)
        {
            switch (provider.ToLowerInvariant())
            {
                case "google":
                    Name = "Google";
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
                    TokenEndpoint = "https://oauth2.googleapis.com/token";
                    Scopes = new[] { "openid", "email", "profile" };
                    UsePkce = true;
                    break;

                case "github":
                    Name = "GitHub";
                    AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                    TokenEndpoint = "https://github.com/login/oauth/access_token";
                    Scopes = new[] { "user:email" };
                    UsePkce = false;
                    break;

                default:
                    throw new ArgumentException($"Unsupported provider: {provider}", nameof(provider));
            }
        }

        public async Task<OAuthResult> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            // Генерируем PKCE codes если нужно
            string codeVerifier = null;
            string codeChallenge = null;

            if (UsePkce)
            {
                codeVerifier = GenerateRandomString(128);
                codeChallenge = GenerateCodeChallenge(codeVerifier);
            }

            // Слушаем redirect на localhost
            _localPort = GetAvailablePort();
            var redirectUrl = $"http://localhost:{_localPort}/oauth-callback";
            var state = GenerateRandomString(32);

            // Строим URL авторизации
            var authUrl = BuildAuthorizationUrl(redirectUrl, state, codeChallenge);

            // Запускаем локальный HTTP сервер для перехвата redirect
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"{redirectUrl}/");
            _httpListener.Start();

            // Запускаем браузер
            Process.Start(new ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            // Ждем callback
            var authCode = await WaitForAuthorizationCodeAsync(cancellationToken);

            // Обмениваем код на токен
            return await ExchangeCodeForTokenAsync(authCode, codeVerifier, redirectUrl, cancellationToken);
        }

        private async Task<string> WaitForAuthorizationCodeAsync(CancellationToken cancellationToken)
        {
            var context = await _httpListener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            string authCode = null;
            string error = null;

            if (request.QueryString["code"] != null)
            {
                authCode = request.QueryString["code"];
            }
            else if (request.QueryString["error"] != null)
            {
                error = request.QueryString["error"];
            }

            // Отправляем ответ пользователю
            var responseString = error == null
                ? "<html><body><h3>Authorization successful! You can close this window.</h3></body></html>"
                : $"<html><body><h3>Authorization failed: {error}</h3></body></html>";

            var buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
            response.OutputStream.Close();

            _httpListener.Stop();
            _httpListener = null;

            if (error != null)
                throw new OAuthException($"OAuth authorization failed: {error}");

            return authCode;
        }

        private async Task<OAuthResult> ExchangeCodeForTokenAsync(
            string code,
            string codeVerifier,
            string redirectUrl,
            CancellationToken cancellationToken)
        {
            var formData = new System.Collections.Generic.Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUrl,
                ["grant_type"] = "authorization_code"
            };

            if (!string.IsNullOrEmpty(_clientSecret))
            {
                formData["client_secret"] = _clientSecret;
            }

            if (UsePkce && !string.IsNullOrEmpty(codeVerifier))
            {
                formData["code_verifier"] = codeVerifier;
            }

            var requestContent = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(TokenEndpoint, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(json);
        }

        private string BuildAuthorizationUrl(string redirectUrl, string state, string codeChallenge)
        {
            var builder = new UriBuilder(AuthorizationEndpoint);
            var query = HttpUtility.ParseQueryString(builder.Query);

            query["client_id"] = ClientId;
            query["redirect_uri"] = redirectUrl;
            query["response_type"] = "code";
            query["scope"] = string.Join(" ", Scopes);
            query["state"] = state;

            if (UsePkce && !string.IsNullOrEmpty(codeChallenge))
            {
                query["code_challenge"] = codeChallenge;
                query["code_challenge_method"] = "S256";
            }

            query["access_type"] = "offline"; // Для Google refresh token
            query["prompt"] = "consent";

            builder.Query = query.ToString();
            return builder.ToString();
        }

        private OAuthResult ParseTokenResponse(string json)
        {
            dynamic response = JsonConvert.DeserializeObject(json);

            var result = new OAuthResult
            {
                AccessToken = response.access_token,
                RefreshToken = response.refresh_token,
                ExpiresAt = DateTime.UtcNow.AddSeconds((double)response.expires_in),
                Provider = Name
            };

            // Парсим дополнительные данные из id_token если есть
            if (response.id_token != null)
            {
                try
                {
                    var idToken = (string)response.id_token;
                    var parts = idToken.Split('.');
                    if (parts.Length == 3)
                    {
                        var payload = parts[1];
                        // Добавляем padding если нужно
                        while (payload.Length % 4 != 0)
                            payload += "=";

                        var jsonBytes = Convert.FromBase64String(payload);
                        var payloadJson = Encoding.UTF8.GetString(jsonBytes);
                        dynamic payloadData = JsonConvert.DeserializeObject(payloadJson);

                        result.Email = payloadData.email;
                        result.UserId = payloadData.sub;
                    }
                }
                catch
                {
                    // Игнорируем ошибки парсинга id_token
                }
            }

            return result;
        }

        private static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~";
            var random = new Random();
            var result = new char[length];

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }

            return new string(result);
        }

        private static string GenerateCodeChallenge(string codeVerifier)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
            return Convert.ToBase64String(challengeBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }

        private static int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<OAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));

            var formData = new System.Collections.Generic.Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            };

            if (!string.IsNullOrEmpty(_clientSecret))
            {
                formData["client_secret"] = _clientSecret;
            }

            var requestContent = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync(TokenEndpoint, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(json);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _httpListener?.Stop();
            _httpListener?.Close();
        }
    }

    public class OAuthException : Exception
    {
        public OAuthException(string message) : base(message) { }
        public OAuthException(string message, Exception innerException) : base(message, innerException) { }

        public OAuthException() : base()
        {
        }
    }
}