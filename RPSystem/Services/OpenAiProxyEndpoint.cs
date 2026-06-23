using System;

namespace ChemCalculationAndManagementApp.Services
{
    public static class OpenAiProxyEndpoint
    {
        public const string DefaultBaseUrl = "http://obsidianvault.duckdns.org:3000/v1";

        public static string BuildUrl(string? configuredBaseUrl, string path)
        {
            var baseUrl = (configuredBaseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = DefaultBaseUrl;
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
