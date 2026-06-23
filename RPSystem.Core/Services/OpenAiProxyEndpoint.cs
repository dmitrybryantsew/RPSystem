using System;

namespace RPSystem.Core.Services
{
    public static class OpenAiProxyEndpoint
    {
        public const string DefaultBaseUrl = "";

        public static string BuildUrl(string? configuredBaseUrl, string path)
        {
            var baseUrl = (configuredBaseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new InvalidOperationException(
                    "OpenAI-compatible proxy base URL is not configured. Set it in Settings.");
            }

            baseUrl = baseUrl.TrimEnd('/');
            if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = baseUrl[..^"/chat/completions".Length].TrimEnd('/');
            }

            if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl += "/v1";
            }

            return $"{baseUrl}/{path.TrimStart('/')}";
        }
    }
}
