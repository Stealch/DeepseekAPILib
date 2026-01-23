using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// Google OAuth провайдер
    /// </summary>
    public class GoogleOAuthProvider : BaseOAuthProvider
    {
        private const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";

        public override string Name => "Google";
        public override string AuthorizationEndpoint => GoogleAuthEndpoint;
        public override string TokenEndpoint => GoogleTokenEndpoint;
        public override string ClientId { get; }
        public override string[] Scopes => new[] { "openid", "email", "profile" };

        public GoogleOAuthProvider(string clientId, string redirectUri)
            : base(redirectUri)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        }

        public override async Task<OAuthResult> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            // В реальном приложении здесь будет:
            // 1. Открытие браузера для авторизации
            // 2. Перехват redirect с кодом
            // 3. Обмен кода на токен

            // Заглушка для демонстрации
            throw new NotImplementedException("Реализация авторизации требует UI для браузера");
        }

        public override async Task<OAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var requestContent = new System.Net.Http.FormUrlEncodedContent(new[]
            {
                new System.Collections.Generic.KeyValuePair<string, string>("client_id", ClientId),
                new System.Collections.Generic.KeyValuePair<string, string>("refresh_token", refreshToken),
                new System.Collections.Generic.KeyValuePair<string, string>("grant_type", "refresh_token")
            });

            var response = await _httpClient.PostAsync(TokenEndpoint, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(json);
        }

        protected override OAuthResult ParseTokenResponse(string json)
        {
            dynamic response = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

            var result = base.ParseTokenResponse(json);

            // Дополнительно получаем email из id_token
            if (response.id_token != null)
            {
                var idToken = (string)response.id_token;
                var parts = idToken.Split('.');
                if (parts.Length == 3)
                {
                    try
                    {
                        var payload = parts[1];
                        // Добавить padding если нужно
                        while (payload.Length % 4 != 0)
                            payload += "=";

                        var jsonBytes = Convert.FromBase64String(payload);
                        var payloadJson = System.Text.Encoding.UTF8.GetString(jsonBytes);
                        dynamic payloadData = Newtonsoft.Json.JsonConvert.DeserializeObject(payloadJson);

                        result.Email = payloadData.email;
                        result.UserId = payloadData.sub;
                    }
                    catch
                    {
                        // Если не удалось распарсить id_token, игнорируем
                    }
                }
            }

            return result;
        }
    }
}