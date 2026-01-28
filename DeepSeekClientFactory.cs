// DeepSeekClientFactory.cs
using System;
using DeepseekAPILib.Curl;

namespace DeepseekAPILib
{
    public static class DeepSeekClientFactory
    {
        public static IDeepSeekClient CreateClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = " ";

            // Простая логика: Windows 8.1+ → HttpClient, иначе → libcurl
            if (ProtocolDetector.IsWindows8_1OrNewer())
            {
                return new DeepSeekAPI(apiKey);
            }

            return new DeepSeekCurlClient(apiKey);
        }
    }
}