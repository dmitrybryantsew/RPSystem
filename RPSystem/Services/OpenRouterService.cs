using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ChemCalculationAndManagementApp.Services
{
    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;
        private const string OpenRouterApiUrl = "https://openrouter.ai/api/v1/chat/completions";
        // Z.AI Endpoints - Standard (Pay-As-You-Go) and Coding (Subscription)
        private const string ZaiStandardUrl = "https://api.z.ai/api/paas/v4/chat/completions";
        private const string ZaiCodingUrl = "https://api.z.ai/api/coding/paas/v4/chat/completions";
        // Chutes LLM API endpoint (OpenAI-compatible)
        // Reference: https://chutes.ai/docs/examples/llm-chat
        // https://docs.litellm.ai/docs/providers/chutes
        // The correct format is https://llm.chutes.ai/v1/chat/completions
        // NOT https://api.chutes.ai/chutes/{UUID}/v1/chat/completions
        private const string ChutesLlmApiUrl = "https://llm.chutes.ai/v1/chat/completions";

        public OpenRouterService(HttpClient httpClient, ISettingsService settings)
        {
            _httpClient = httpClient; // Use the injected client (which has SSL bypass)
            _settingsService = settings;
            // API key and headers are set dynamically in methods (AnalyzeImageAsync/GenerateTextAsync)
        }

        public async Task<string> AnalyzeImageAsync(string provider, string apiKey, string model, string prompt, string imageUrl, List<ChatApiMessage>? conversationHistory = null)
        {
            // 1. Determine URL and Model Name
            string targetUrl = OpenRouterApiUrl; // Default
            string actualModelName = model;      // Default

            if (provider == "ZAI")
            {
                // Switch URL based on Subscription Mode setting
                targetUrl = _settingsService.ZaiSubscriptionMode ? ZaiCodingUrl : ZaiStandardUrl;
            }
            else if (provider == "Chutes")
            {
                // Parse the "UUID|Name" format we created in AiModelService
                // For Chutes, we use the llm.chutes.ai endpoint (not api.chutes.ai/chutes/{UUID})
                // The model is specified in the payload, not the URL path
                if (model.Contains("|"))
                {
                    var parts = model.Split('|');
                    // parts[0] is the UUID (not used in URL anymore)
                    // parts[1] is the actual model name (e.g., "moonshotai/Kimi-K2.5-TEE")
                    // Use the model name directly - NO prefix needed for llm.chutes.ai
                    // The 'chutes/' prefix is only for LiteLLM proxy server, not direct API calls
                    actualModelName = parts[1];
                }

                // Use the LLM API endpoint (OpenAI-compatible)
                targetUrl = ChutesLlmApiUrl;
            }
            else if (provider == "OpenAIProxy")
            {
                targetUrl = BuildOpenAiProxyUrl("chat/completions");
            }

            // 2. Prepare Headers (Dynamic)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // OpenRouter specific headers (ignored by ZAI and Chutes)
            if (provider == "OpenRouter")
            {
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost/maui");
            }

            // 3. Prepare Payload (Use actualModelName)
            // Build messages list with history if provided
            var messages = new List<ChatApiMessage>();

            // Add conversation history
            // FIX: Include all text-only messages in history so the model has context
            if (conversationHistory != null)
            {
                Debug.WriteLine($"[AnalyzeImageAsync] Adding {conversationHistory.Count} history messages to payload");
                int histIdx = 0;
                foreach (var msg in conversationHistory)
                {
                    // Only include text messages in history (vision models have complex content)
                    if (msg.Content is string textContent && !string.IsNullOrEmpty(textContent))
                    {
                        messages.Add(new ChatApiMessage
                        {
                            Role = msg.Role,
                            Content = textContent
                        });
                        Debug.WriteLine($"[AnalyzeImageAsync] History[{histIdx}]: Role={msg.Role}, ContentLength={textContent.Length}");
                        histIdx++;
                    }
                }
            }

            // Add current message with image - FIX: Ensure proper role is set
            messages.Add(new ChatApiMessage
            {
                Role = "user",
                Content = new List<VisionContentPart>
                {
                    new TextContentPart(prompt),
                    new ImageUrlContentPart(imageUrl)
                }
            });

            var requestPayload = new ChatRequest
            {
                Model = actualModelName,
                Messages = messages,
                MaxTokens = 2000
            };

            Debug.WriteLine($"[AnalyzeImageAsync] ===== PAYLOAD SUMMARY =====");
            Debug.WriteLine($"[AnalyzeImageAsync] Total messages to send: {messages.Count}");
            Debug.WriteLine($"[AnalyzeImageAsync] Image URL length: {imageUrl?.Length ?? 0}");
            Debug.WriteLine($"[AnalyzeImageAsync] Current message: prompt='{prompt}', has_image=true");
            Debug.WriteLine($"[AnalyzeImageAsync] Message breakdown:");
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                string contentType = msg.Content.GetType().Name;
                string contentPreview = msg.Content is string s ? s.Substring(0, Math.Min(30, s.Length)) : "[complex content]";
                Debug.WriteLine($"[AnalyzeImageAsync]   [{i}] Role={msg.Role}, Type={contentType}, Content='{contentPreview}...'");
            }
            Debug.WriteLine($"[AnalyzeImageAsync] ===== END PAYLOAD =====");

            return await SendRequestAsync(targetUrl, requestPayload);
        }

        // Legacy method for backward compatibility (uses OpenRouter default)
        public async Task<string> AnalyzeImageAsync(string model, string prompt, string imageUrl)
        {
            var apiKey = _settingsService.OpenRouterApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: Please enter your OpenRouter API key in Settings.";
            }

            return await AnalyzeImageAsync("OpenRouter", apiKey, model, prompt, imageUrl);
        }

        private async Task<string> SendRequestAsync(string url, ChatRequest requestPayload)
        {
            HttpResponseMessage? response = null; // Declare response outside the try block
            try
            {
                var serializerOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                string jsonPayload = JsonSerializer.Serialize(requestPayload, serializerOptions);
                Debug.WriteLine($"[AI Request] URL: {url} | Payload: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(url, content); // Assign to the outer response

                // --- THIS IS THE FIX ---
                // We check for success *after* the request, but if it fails, the catch block will handle it.
                if (!response.IsSuccessStatusCode)
                {
                    // This will throw an exception that the catch block below will handle.
                    // This makes our logging logic cleaner.
                    response.EnsureSuccessStatusCode();
                }
                // --- END OF FIX ---

                ChatResponse? chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();

                if (chatResponse?.Choices?.Count > 0)
                {
                    return chatResponse.Choices[0].Message.Content.Trim();
                }

                return "Error: Received an empty response from the API.";
            }
            catch (HttpRequestException httpEx)
            {
                // Now, if an exception occurs, we can safely check if the 'response' object exists
                string errorContent = response != null ? await response.Content.ReadAsStringAsync() : "No response body.";
                Debug.WriteLine($"[HTTP Error] {httpEx.StatusCode}: {httpEx.Message}\nResponse Body: {errorContent}");
                return $"Error: Could not connect to the API. {httpEx.Message}";
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"[JSON Error] {jsonEx.Message}");
                return $"Error: Could not parse the API response. {jsonEx.Message}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Generic Error] {ex.Message}");
                return $"An unexpected error occurred: {ex.Message}";
            }
        }

        public async Task<string> GenerateTextAsync(string provider, string apiKey, string model, string prompt, List<ChatApiMessage>? conversationHistory = null)
        {
            // 1. Determine URL and Model Name
            string targetUrl = OpenRouterApiUrl;
            string actualModelName = model;

            if (provider == "ZAI")
            {
                // Switch URL based on Subscription Mode setting
                targetUrl = _settingsService.ZaiSubscriptionMode ? ZaiCodingUrl : ZaiStandardUrl;
            }
            else if (provider == "Chutes")
            {
                // Parse the "UUID|Name" format we created in AiModelService
                // For Chutes, we use the llm.chutes.ai endpoint (not api.chutes.ai/chutes/{UUID})
                // The model is specified in the payload, not the URL path
                if (model.Contains("|"))
                {
                    var parts = model.Split('|');
                    // parts[0] is the UUID (not used in URL anymore)
                    // parts[1] is the actual model name (e.g., "moonshotai/Kimi-K2.5-TEE")
                    // Use the model name directly - NO prefix needed for llm.chutes.ai
                    // The 'chutes/' prefix is only for LiteLLM proxy server, not direct API calls
                    actualModelName = parts[1];
                }

                // Use the LLM API endpoint (OpenAI-compatible)
                targetUrl = ChutesLlmApiUrl;
            }
            else if (provider == "OpenAIProxy")
            {
                targetUrl = BuildOpenAiProxyUrl("chat/completions");
            }

            // 2. Prepare Headers (Dynamic)
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // OpenRouter specific headers (ignored by ZAI and Chutes)
            if (provider == "OpenRouter")
            {
                _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost/maui");
            }

            // 3. Prepare Payload (Use actualModelName)
            // Build messages list with history if provided
            var messages = new List<ChatApiMessage>();

            // Add conversation history
            if (conversationHistory != null)
            {
                messages.AddRange(conversationHistory);
            }

            // Add current message
            messages.Add(new ChatApiMessage { Content = prompt });

            var requestPayload = new ChatRequest
            {
                Model = actualModelName,
                Messages = messages,
                MaxTokens = 2000
            };

            return await SendRequestAsync(targetUrl, requestPayload);
        }

        // Legacy method for backward compatibility (text-only with OpenRouter default)
        public async Task<string> GenerateTextAsync(string model, string prompt)
        {
            var apiKey = _settingsService.OpenRouterApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return "Error: Please enter your OpenRouter API key in Settings.";
            }

            return await GenerateTextAsync("OpenRouter", apiKey, model, prompt);
        }

        /// <summary>
        /// Helper method to safely add authorization headers.
        /// Returns false and provides an error result if the header is invalid.
        /// </summary>
        private bool TryAddAuthorizationHeader(string apiKey, string provider, out StreamResult? error)
        {
            try
            {
                Debug.WriteLine($"[TryAddAuthorizationHeader] Provider: {provider}");
                Debug.WriteLine($"[TryAddAuthorizationHeader] API Key length: {apiKey?.Length ?? 0}");
                Debug.WriteLine($"[TryAddAuthorizationHeader] API Key first 8 chars: {apiKey?.Substring(0, Math.Min(8, apiKey?.Length ?? 0))}...");

                // Sanitize the API key - remove newlines, carriage returns, and other control characters
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.WriteLine("[TryAddAuthorizationHeader] API Key is null or empty");
                    error = StreamResult.CreateError("API key is null or empty");
                    return false;
                }

                // Remove control characters (newlines, tabs, etc.)
                var sanitizedKey = apiKey
                    .Replace("\r", "")
                    .Replace("\n", "")
                    .Replace("\t", "")
                    .Trim();

                Debug.WriteLine($"[TryAddAuthorizationHeader] Sanitized key length: {sanitizedKey.Length}");

                if (sanitizedKey.Length != apiKey.Length)
                {
                    Debug.WriteLine($"[TryAddAuthorizationHeader] WARNING: API key contained {apiKey.Length - sanitizedKey.Length} control characters that were removed");
                }

                // This is where System.ArgumentException happens if key has invalid characters like \n
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {sanitizedKey}");

                if (provider == "OpenRouter")
                {
                    _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "http://localhost/maui");
                }

                Debug.WriteLine("[TryAddAuthorizationHeader] Headers added successfully");
                error = null;
                return true;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"[Authorization Header Error] {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[Authorization Header Error] ParamName: {ex.ParamName}");
                Debug.WriteLine($"[Authorization Header Error] StackTrace: {ex.StackTrace}");
                error = StreamResult.CreateError($"Invalid API key format: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Authorization Header Unexpected Error] {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[Authorization Header Unexpected Error] StackTrace: {ex.StackTrace}");
                error = StreamResult.CreateError($"Unexpected error adding authorization: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates text with streaming support.
        /// Returns an async enumerable of StreamResult objects.
        /// Each result contains either a content chunk or a completion/error status.
        /// </summary>
        public async IAsyncEnumerable<StreamResult> GenerateTextStreamAsync(
            string provider,
            string apiKey,
            string model,
            string prompt,
            List<ChatApiMessage>? conversationHistory = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 1. Determine URL and Model Name
            string targetUrl = OpenRouterApiUrl;
            string actualModelName = model;

            if (provider == "ZAI")
            {
                targetUrl = _settingsService.ZaiSubscriptionMode ? ZaiCodingUrl : ZaiStandardUrl;
            }
            else if (provider == "Chutes")
            {
                if (model.Contains("|"))
                {
                    var parts = model.Split('|');
                    actualModelName = parts[1];
                }
                targetUrl = ChutesLlmApiUrl;
            }
            else if (provider == "OpenAIProxy")
            {
                targetUrl = BuildOpenAiProxyUrl("chat/completions");
            }

            // 2. Prepare Headers (Dynamic)
            // FIX: Ensure no invalid characters are in the key
            var safeKey = apiKey?.Trim() ?? "";

            // Set up headers - use helper to catch ArgumentException
            _httpClient.DefaultRequestHeaders.Clear();
            if (!TryAddAuthorizationHeader(safeKey, provider, out var headerError))
            {
                yield return headerError ?? StreamResult.CreateError("Failed to add authorization header.");
                yield break;
            }

            // 3. Prepare Payload for streaming
            // Build messages list with history if provided
            var messages = new List<ChatApiMessage>();

            // Add conversation history
            if (conversationHistory != null)
            {
                Debug.WriteLine($"[GenerateTextStreamAsync] Adding {conversationHistory.Count} history messages");
                messages.AddRange(conversationHistory);
            }

            // Add current message
            messages.Add(new ChatApiMessage { Content = prompt });

            var requestPayload = new StreamChatRequest
            {
                Model = actualModelName,
                Messages = messages,
                MaxTokens = 2000,
                Stream = true
            };

            Debug.WriteLine($"[GenerateTextStreamAsync] ===== PAYLOAD SUMMARY =====");
            Debug.WriteLine($"[GenerateTextStreamAsync] Total messages: {messages.Count}");
            Debug.WriteLine($"[GenerateTextStreamAsync] Current prompt length: {prompt?.Length ?? 0}");
            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                string contentPreview = msg.Content is string s ? s.Substring(0, Math.Min(30, s.Length)) : "[non-string]";
                Debug.WriteLine($"[GenerateTextStreamAsync]   [{i}] Role={msg.Role}, Content='{contentPreview}...'");
            }
            Debug.WriteLine($"[GenerateTextStreamAsync] ===== END PAYLOAD =====");

            var serializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            Debug.WriteLine($"[GenerateTextStreamAsync] Starting serialization at {DateTime.UtcNow:HH:mm:ss.fff}");
            string jsonPayload;

            // Serialize outside of try-catch to avoid yield issues
            jsonPayload = JsonSerializer.Serialize(requestPayload, serializerOptions);
            Debug.WriteLine($"[GenerateTextStreamAsync] Serialization successful, payload length: {jsonPayload.Length}");

            Debug.WriteLine($"[AI Stream Request] URL: {targetUrl} | Payload: {jsonPayload}");

            // 4. Get the response stream (handle errors before entering iterator)
            var (responseResult, error) = await GetStreamResponseAsync(targetUrl, jsonPayload, cancellationToken);
            if (error != null)
            {
                yield return error;
                yield break;
            }
            if (responseResult is null)
            {
                yield return StreamResult.CreateError("Stream response was null.");
                yield break;
            }

            // 5. Process the stream (no try-catch allowed with yield)
            using (responseResult)
            {
                var stream = await responseResult.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.WriteLine("[AI Stream] Cancelled by user");
                        yield return StreamResult.CreateError("Stream cancelled");
                        yield break;
                    }

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // Check for [DONE] marker
                    if (SseParser.IsDoneMarker(line))
                    {
                        Debug.WriteLine("[AI Stream] Received [DONE] marker");
                        yield return StreamResult.Complete("stop");
                        yield break;
                    }

                    // Try to parse the chunk
                    var chunk = SseParser.TryParseChunk(line);
                    if (chunk != null)
                    {
                        // Extract content delta
                        var contentDelta = SseParser.ExtractContent(chunk);
                        if (!string.IsNullOrEmpty(contentDelta))
                        {
                            Debug.WriteLine($"[AI Stream] Chunk: {contentDelta}");
                            yield return StreamResult.ContentChunk(contentDelta);
                        }

                        // Check if this is the final chunk
                        var finishReason = SseParser.ExtractFinishReason(chunk);
                        if (finishReason != null)
                        {
                            Debug.WriteLine($"[AI Stream] Complete: {finishReason}");
                            yield return StreamResult.Complete(finishReason);
                            yield break;
                        }
                    }
                }

                // If we exit the loop without receiving [DONE], stream ended unexpectedly
                Debug.WriteLine("[AI Stream] Stream ended without [DONE] marker");
                yield return StreamResult.Complete("unknown");
            }
        }

        /// <summary>
        /// Helper method to get the HTTP response stream with error handling.
        /// Separated from the iterator to avoid yield return in try-catch issues.
        /// </summary>
        private async Task<(HttpResponseMessage? response, StreamResult? error)> GetStreamResponseAsync(
            string url,
            string jsonPayload,
            CancellationToken cancellationToken)
        {
            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Debug.WriteLine($"[AI Stream Error] {response.StatusCode}: {errorBody}");
                    return (null, StreamResult.CreateError($"HTTP {response.StatusCode}: {errorBody}"));
                }

                return (response, null);
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[AI Stream Error] {httpEx.Message}");
                return (null, StreamResult.CreateError($"Network error: {httpEx.Message}"));
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"[AI Stream Error] {ioEx.Message}");
                return (null, StreamResult.CreateError($"Connection error: {ioEx.Message}"));
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("[AI Stream] Timeout or cancellation");
                return (null, StreamResult.CreateError("Stream timeout or cancelled"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI Stream Error] {ex.GetType().Name}: {ex.Message}");
                return (null, StreamResult.CreateError($"Unexpected error: {ex.Message}"));
            }
        }

        /// <summary>
        /// Legacy streaming method for backward compatibility (uses OpenRouter default).
        /// </summary>
        public IAsyncEnumerable<StreamResult> GenerateTextStreamAsync(string model, string prompt)
        {
            var apiKey = _settingsService.OpenRouterApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return MissingOpenRouterApiKeyStream();
            }

            return GenerateTextStreamAsync("OpenRouter", apiKey, model, prompt);
        }

        private static async IAsyncEnumerable<StreamResult> MissingOpenRouterApiKeyStream()
        {
            await Task.Yield();
            yield return StreamResult.CreateError("Please enter your OpenRouter API key in Settings.");
        }

        private string BuildOpenAiProxyUrl(string path)
            => OpenAiProxyEndpoint.BuildUrl(_settingsService.OpenAiProxyBaseUrl, path);
    }
}
