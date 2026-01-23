using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DeepseekAPILib.Services
{
    /// <summary>
    /// Сервис для получения Deepseek API ключа через OAuth
    /// </summary>
    public class DeepseekAuthService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _authProxyUrl;

        public DeepseekAuthService(string authProxyUrl = "https://your-auth-proxy.com")
        {
            _httpClient = new HttpClient();
            _authProxyUrl = authProxyUrl?.TrimEnd('/');
        }

        /// <summary>
        /// Получить временный Deepseek API ключ через OAuth токен
        /// </summary>
        public async Task<string> GetDeepseekApiKeyAsync(OAuth.OAuthResult oauthResult, CancellationToken cancellationToken = default)
        {
            if (oauthResult == null)
                throw new ArgumentNullException(nameof(oauthResult));

            if (string.IsNullOrEmpty(oauthResult.AccessToken))
                throw new ArgumentException("Access token is required", nameof(oauthResult));

            var request = new
            {
                provider = oauthResult.Provider,
                access_token = oauthResult.AccessToken,
                email = oauthResult.Email,
                user_id = oauthResult.UserId
            };

            var jsonContent = JsonConvert.SerializeObject(request);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_authProxyUrl}/api/auth/deepseek-token", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            dynamic responseData = JsonConvert.DeserializeObject(responseJson);

            return responseData.api_key;
        }

        /// <summary>
        /// Проверить валидность API ключа
        /// </summary>
        public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(apiKey))
                return false;

            try
            {
                var request = new { api_key = apiKey };
                var jsonContent = JsonConvert.SerializeObject(request);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_authProxyUrl}/api/auth/validate", content, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Обновить истекший API ключ
        /// </summary>
        public async Task<string> RefreshApiKeyAsync(string oldApiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(oldApiKey))
                throw new ArgumentException("API key is required", nameof(oldApiKey));

            var request = new { api_key = oldApiKey };
            var jsonContent = JsonConvert.SerializeObject(request);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_authProxyUrl}/api/auth/refresh", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            dynamic responseData = JsonConvert.DeserializeObject(responseJson);

            return responseData.api_key;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}