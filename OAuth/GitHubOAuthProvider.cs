using System;
using System.Collections.Generic;
using System.Web
using System.Threading;
using System.Threading.Tasks;

namespace DeepseekAPILib.OAuth
{
    /// <summary>
    /// GitHub OAuth провайдер
    /// </summary>
    public class GitHubOAuthProvider : BaseOAuthProvider
    {
        private const string GitHubAuthEndpoint = "https://github.com/login/oauth/authorize";
        private const string GitHubTokenEndpoint = "https://github.com/login/oauth/access_token";

        private readonly string _clientSecret;

        public override string Name => "GitHub";
        public override string AuthorizationEndpoint => GitHubAuthEndpoint;
        public override string TokenEndpoint => GitHubTokenEndpoint;
        public override string ClientId { get; }
        public override string[] Scopes => new[] { "user:email" };
        public override bool UsePkce => false; // GitHub пока не требует PKCE

        public GitHubOAuthProvider(string clientId, string clientSecret, string redirectUri)
            : base(redirectUri)
        {
            ClientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        }

        public override async Task<OAuthResult> AuthorizeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Реализация авторизации требует UI для браузера");
        }

        public override async Task<OAuthResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            // GitHub не поддерживает refresh tokens для OAuth Apps
            throw new NotSupportedException("GitHub OAuth Apps не поддерживают refresh tokens");
        }

        protected override async Task<OAuthResult> ExchangeCodeForTokenAsync(string code, string codeVerifier, CancellationToken cancellationToken)
        {
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", _redirectUri)
            });

            var response = await _httpClient.PostAsync(TokenEndpoint, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return ParseTokenResponse(json);
        }

        protected override OAuthResult ParseTokenResponse(string json)
        {
            dynamic response = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

            return new OAuthResult
            {
                AccessToken = response.access_token,
                // GitHub не возвращает refresh_token для OAuth Apps
                ExpiresAt = DateTime.UtcNow.AddDays(365), // GitHub tokens не expire
                Provider = Name
            };
        }
    }
}