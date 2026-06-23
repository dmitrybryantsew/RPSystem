using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RPSystem.Core.Services;
using RPSystem.Core.Models;
using System.IO;

namespace RPSystem.Core.Services
{
    public class AiModelService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settingsService;

        // Updated International Model IDs for Z.AI
        private readonly List<AiModel> _fallbackZaiModels = new()
        {
            new AiModel { Id = "glm-4.7", Name = "GLM-4.7 (Latest Flagship)", Provider = "ZAI" },
            new AiModel { Id = "glm-4.6v", Name = "GLM-4.6V (Vision SOTA)", Provider = "ZAI" },
            new AiModel { Id = "glm-4-plus", Name = "GLM-4 Plus", Provider = "ZAI" },
            new AiModel { Id = "glm-4-flash", Name = "GLM-4 Flash (Fast/Cheap)", Provider = "ZAI" },
            new AiModel { Id = "glm-4-air", Name = "GLM-4 Air", Provider = "ZAI" }
        };

        private readonly List<AiModel> _fallbackOpenRouterModels = new()
        {
            new AiModel { Id = "google/gemini-2.0-flash-lite-preview-02-05:free", Name = "Gemini 2.0 Flash Lite (Free)", Provider = "OpenRouter" },
            new AiModel { Id = "google/gemini-2.0-pro-exp-02-05:free", Name = "Gemini 2.0 Pro (Free)", Provider = "OpenRouter" },
            new AiModel { Id = "qwen/qwen2.5-vl-32b-instruct:free", Name = "Qwen 2.5 VL (Free)", Provider = "OpenRouter" },
            new AiModel { Id = "deepseek/deepseek-r1:free", Name = "DeepSeek R1 (Free)", Provider = "OpenRouter" }
        };

        private readonly List<AiModel> _fallbackOpenAiProxyModels = new()
        {
            new AiModel { Id = "nim:nvidia/nemotron-3-nano-omni-30b-a3b-reasoning", Name = "NIM: NVIDIA Nemotron 3 Nano Omni 30B", Provider = "OpenAIProxy" },
            new AiModel { Id = "openrouter:google/gemma-3-27b-it:free", Name = "OpenRouter: Gemma 3 27B Free", Provider = "OpenAIProxy" }
        };

        // Fallback models for Chutes.ai (decentralized models)
        private readonly List<AiModel> _fallbackChutesModels = new()
        {
            new AiModel { Id = "unsloth/Llama-3.2-11B-Vision-Instruct",  Name = "Llama 3.2 11B Vision (Chemical/Vision)", Provider = "Chutes" },
            new AiModel { Id = "Qwen/Qwen2-VL-72B-Instruct",            Name = "Qwen 2 VL 72B (Vision SOTA)", Provider = "Chutes" },
            new AiModel { Id = "meta-llama/Meta-Llama-3.1-70B-Instruct", Name = "Llama 3.1 70B (Text Only)", Provider = "Chutes" }
        };

        // ── Last-debug-output storage (surfaced by ChatViewModel) ─────────────
        /// <summary>
        /// Set by FetchChutesModels when DebugEnabled is true.
        /// ChatViewModel reads this after GetModelsAsync returns and can
        /// display it in the UI so the tester doesn't have to dig through
        /// the Output window.
        /// </summary>
        public string? LastDebugOutput { get; private set; }

        // ── Constructor ───────────────────────────────────────────────────────

        public AiModelService(HttpClient httpClient, ISettingsService settingsService)
        {
            _httpClient = httpClient; // Use the injected, SSL-configured client
            _settingsService = settingsService;
        }

        // ── Public entry point ────────────────────────────────────────────────

        /// <summary>
        /// Returns ALL models from ALL providers, grouped by provider.
        /// Used by the new grouped model picker.
        /// </summary>
        public async Task<List<ModelPickerItem>> GetAllModelsGroupedAsync(bool forceRefresh = false)
        {
            var allModels = new List<AiModel>();
            string currentProvider = _settingsService.AiProvider;

            // Fetch from current provider (uses cache)
            var currentModels = await GetModelsAsync(forceRefresh);
            if (currentModels != null) allModels.AddRange(currentModels);

            // Also fetch from other providers in background for a unified list
            string[] providers = { "OpenRouter", "OpenAIProxy", "ZAI", "Chutes" };
            foreach (var prov in providers)
            {
                if (prov == currentProvider) continue;
                try
                {
                    var cached = await LoadCachedModelsAsync(prov);
                    if (cached != null && cached.Count > 0)
                    {
                        allModels.AddRange(ApplyDisplayNames(cached));
                    }
                }
                catch { /* skip unavailable providers */ }
            }

            // Group and build picker items
            var items = new List<ModelPickerItem>();
            var grouped = allModels.GroupBy(m => string.IsNullOrEmpty(m.Provider) ? "Other" : m.Provider);
            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                items.Add(new ModelPickerItem { IsHeader = true, GroupTitle = group.Key });
                foreach (var model in group)
                {
                    items.Add(new ModelPickerItem { IsHeader = false, Model = model });
                }
            }
            return items;
        }

        public async Task<List<AiModel>> GetModelsAsync(bool forceRefresh = false)
        {
            return await GetModelsForProviderAsync(_settingsService.AiProvider, forceRefresh);
        }

        public async Task<List<AiModel>> GetModelsForProviderAsync(string provider, bool forceRefresh = false)
        {
            provider = NormalizeProvider(provider);
            LastDebugOutput = null; // reset each call

            try
            {
                if (!forceRefresh)
                {
                    var cached = await LoadCachedModelsAsync(provider);
                    if (cached != null && cached.Count > 0)
                    {
                        return ApplyModelSettings(cached);
                    }
                }

                List<AiModel>? fetched = provider switch
                {
                    "OpenAIProxy" => await FetchOpenAiProxyModels(),
                    "ZAI" => await FetchZaiModels(),
                    "Chutes" => await FetchChutesModels(),
                    _ => await FetchOpenRouterModels()
                };

                if (fetched != null && fetched.Count > 0)
                {
                    await SaveCachedModelsAsync(provider, fetched);
                    return ApplyModelSettings(fetched);
                }

                var fallback = provider switch
                {
                    "OpenAIProxy" => _fallbackOpenAiProxyModels,
                    "ZAI" => _fallbackZaiModels,
                    "Chutes" => _fallbackChutesModels,
                    _ => _fallbackOpenRouterModels
                };

                // If force refresh failed, try returning cache before fallback
                if (forceRefresh)
                {
                    var cached = await LoadCachedModelsAsync(provider);
                    if (cached != null && cached.Count > 0)
                    {
                        return ApplyModelSettings(cached);
                    }
                }

                return ApplyModelSettings(fallback);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetModelsAsync] Unhandled exception: {ex}");

                if (_settingsService.DebugEnabled)
                    LastDebugOutput = $"[FATAL] GetModelsAsync exception:\n  Type : {ex.GetType().Name}\n  Msg  : {ex.Message}\n  Stack: {ex.StackTrace}";

                var cached = await LoadCachedModelsAsync(provider);
                if (cached != null && cached.Count > 0)
                {
                    return ApplyModelSettings(cached);
                }

                return ApplyModelSettings(provider switch
                {
                    "OpenAIProxy" => _fallbackOpenAiProxyModels,
                    "ZAI"    => _fallbackZaiModels,
                    "Chutes" => _fallbackChutesModels,
                    _        => _fallbackOpenRouterModels
                });
            }
        }

        public async Task<(bool success, string message)> PreloadModelsAsync()
        {
            string provider = _settingsService.AiProvider;
            var models = await GetModelsAsync(forceRefresh: true);
            if (models != null && models.Count > 0)
            {
                var cachePath = GetCachePath(provider);
                return (true, $"Cached {models.Count} models for {provider}. File: {Path.GetFileName(cachePath)}");
            }

            return (false, $"Failed to cache models for {provider}. Using fallback list.");
        }

        private string GetProviderCacheKey(string provider)
        {
            if (provider == "ZAI")
            {
                return _settingsService.ZaiSubscriptionMode ? "ZAI-coding" : "ZAI-standard";
            }

            return provider;
        }

        private static string NormalizeProvider(string provider)
        {
            return provider?.Trim() switch
            {
                "OpenAIProxy" => "OpenAIProxy",
                "ZAI" => "ZAI",
                "Chutes" => "Chutes",
                _ => "OpenRouter"
            };
        }

        private string GetCachePath(string provider)
        {
            var key = GetProviderCacheKey(provider);
            var fileName = $"models_{key}.json";
            return Path.Combine(AppPaths.AppDataDirectory, fileName);
        }

        private async Task<List<AiModel>?> LoadCachedModelsAsync(string provider)
        {
            try
            {
                var path = GetCachePath(provider);
                if (!File.Exists(path))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(path);
                var cached = JsonSerializer.Deserialize<CachedModels>(json);
                return cached?.Models;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiModelService] Cache load failed: {ex.Message}");
                return null;
            }
        }

        private async Task SaveCachedModelsAsync(string provider, List<AiModel> models)
        {
            try
            {
                var path = GetCachePath(provider);
                var cached = new CachedModels
                {
                    CachedAtUtc = DateTime.UtcNow,
                    Models = models
                };

                var json = JsonSerializer.Serialize(cached);
                await File.WriteAllTextAsync(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AiModelService] Cache save failed: {ex.Message}");
            }
        }

        private List<AiModel> ApplyModelSettings(List<AiModel> models)
        {
            var filtered = AiModelFilter.Apply(models, _settingsService, DateTime.UtcNow);
            return ApplyDisplayNames(filtered);
        }

        private List<AiModel> ApplyDisplayNames(List<AiModel> models)
        {
            if (!_settingsService.CompactModelNames)
            {
                return models;
            }

            return models.Select(m => new AiModel
            {
                    Id = m.Id,
                    Name = CompactName(m),
                    Created = m.Created,
                    Provider = m.Provider,
                    ProviderModelId = m.ProviderModelId,
                    OwnedBy = m.OwnedBy,
                    Modalities = m.Modalities
            }).ToList();
        }

        private string CompactName(AiModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Id))
            {
                return model.Name;
            }

            var id = model.Id;
            if (id.Contains("|"))
            {
                var parts = id.Split('|');
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    id = parts[1];
                }
            }

            return id;
        }

        private async Task<List<AiModel>?> FetchZaiModels()
        {
            var apiKey = _settingsService.ZaiApiKey;
            if (string.IsNullOrEmpty(apiKey)) return null;

            // Determine URL based on mode
            string baseUrl = _settingsService.ZaiSubscriptionMode
                ? "https://api.z.ai/api/coding/paas/v4/models"
                : "https://api.z.ai/api/paas/v4/models";

            var request = new HttpRequestMessage(HttpMethod.Get, baseUrl);
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
                return result?.Data;
            }
            return null;
        }

        private async Task<List<AiModel>?> FetchOpenRouterModels()
        {
            var response = await _httpClient.GetAsync("https://openrouter.ai/api/v1/models");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
                // Filter OpenRouter list to prevent 1000+ items (optional logic here)
                // For now, return the top 20 or specific ones, or just all of them.
                // Let's just return the result if it's reasonable, or stick to fallback for safety
                // because OpenRouter returns HUNDREDS of models which might lag the UI.

                // Let's return the API list but maybe we want to sort/filter it later.
                // For simplicity now, let's return the API data.
                return result?.Data;
            }
            return null;
        }

        private async Task<List<AiModel>?> FetchOpenAiProxyModels()
        {
            var apiKey = _settingsService.OpenAiProxyApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, BuildOpenAiProxyUrl("models"));
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
            return result?.Data?
                .Where(m => !string.IsNullOrWhiteSpace(m.Id))
                .Select(m => new AiModel
                {
                    Id = m.Id,
                    Name = BuildOpenAiProxyModelName(m),
                    Created = m.Created,
                    Provider = "OpenAIProxy",
                    ProviderModelId = m.ProviderModelId,
                    OwnedBy = m.OwnedBy,
                    Modalities = m.Modalities
                })
                .ToList();
        }

        private string BuildOpenAiProxyUrl(string path)
            => OpenAiProxyEndpoint.BuildUrl(_settingsService.OpenAiProxyBaseUrl, path);

        private static string BuildOpenAiProxyModelName(AiModel model)
        {
            var rawName = string.IsNullOrWhiteSpace(model.Name) ? model.Id : model.Name;
            var sourceProvider = ExtractProxySourceProvider(model);
            return string.IsNullOrWhiteSpace(sourceProvider)
                ? rawName
                : $"{sourceProvider}: {rawName}";
        }

        private static string ExtractProxySourceProvider(AiModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Provider) &&
                !string.Equals(model.Provider, "OpenAIProxy", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeProxySourceProvider(model.Provider);
            }

            var separator = model.Id.IndexOf(':');
            return separator > 0
                ? NormalizeProxySourceProvider(model.Id[..separator])
                : string.Empty;
        }

        private static string NormalizeProxySourceProvider(string value)
            => value.Trim().ToLowerInvariant() switch
            {
                "nim" => "NIM",
                "chutes" => "Chutes",
                "ollama" => "Ollama",
                "g4f" => "GPT4Free",
                "openrouter" => "OpenRouter",
                var other => other
            };

        // ── Chutes (fully instrumented) ───────────────────────────────────────

        private async Task<List<AiModel>?> FetchChutesModels()
        {
            bool debug = _settingsService.DebugEnabled;
            var log = new System.Text.StringBuilder();

            // ── 1. Validate API key ─────────────────────────────────────────────
            var apiKey = _settingsService.ChutesApiKey?.Trim(); // trim whitespace that breaks auth
            if (string.IsNullOrEmpty(apiKey))
            {
                string msg = "[Chutes] No API key provided — using fallback models.";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            if (debug)
                log.AppendLine($"[Chutes] API key length: {apiKey.Length} chars (first 8: {apiKey[..Math.Min(8, apiKey.Length)]}....)");

            // ── 2. Fire the request ─────────────────────────────────────────────
            string url = "https://api.chutes.ai/chutes/?include_public=true&limit=100";
            if (debug) log.AppendLine($"[Chutes] GET {url}");

            HttpResponseMessage response;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                response = await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                string msg = $"[Chutes] Network / send exception:\n  Type : {ex.GetType().Name}\n  Msg  : {ex.Message}\n  Stack: {ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            // ── 3. Log status + body ────────────────────────────────────────────
            if (debug)
                log.AppendLine($"[Chutes] HTTP status: {(int)response.StatusCode} ({response.StatusCode})");

            // Read the raw body ONCE and keep it — we may need it for both
            // error reporting and manual parsing.
            string rawBody;
            try { rawBody = await response.Content.ReadAsStringAsync(); }
            catch (Exception ex)
            {
                string msg = $"[Chutes] Failed to read response body: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            // Log a truncated preview (first 2 000 chars) — enough to see structure
            if (debug)
                log.AppendLine($"[Chutes] Response body (first 2000 chars):\n{rawBody[..Math.Min(2000, rawBody.Length)]}");

            System.Diagnostics.Debug.WriteLine($"[Chutes] Status={response.StatusCode} | Body={rawBody[..Math.Min(500, rawBody.Length)]}");

            // ── 4. Guard on non-success ──────────────────────────────────────────
            if (!response.IsSuccessStatusCode)
            {
                string msg = $"[Chutes] API returned {(int)response.StatusCode}. Body: {rawBody[..Math.Min(500, rawBody.Length)]}";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            // ── 5. Deserialize the paginated envelope ───────────────────────────
            // Real response shape (confirmed from live API):
            //   { "total": 350, "page": 0, "limit": 100, "items": [ … ] }
            ChutesListResponse? envelope;
            try
            {
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                envelope = JsonSerializer.Deserialize<ChutesListResponse>(rawBody, jsonOptions);
            }
            catch (JsonException jex)
            {
                string msg = $"[Chutes] JSON parse error:\n  Msg: {jex.Message}\n  Path: {jex.Path}\n  Line: {jex.LineNumber}";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            if (debug)
            {
                log.AppendLine($"[Chutes] Envelope — total: {envelope?.Total}, page: {envelope?.Page}, limit: {envelope?.Limit}, items count: {envelope?.Items?.Count ?? 0}");
            }

            var rawData = envelope?.Items;

            // ── 6. Validate ──────────────────────────────────────────────────────
            if (rawData == null || rawData.Count == 0)
            {
                string msg = "[Chutes] Items list is null or empty — using fallback models.";
                System.Diagnostics.Debug.WriteLine(msg);
                if (debug) { log.AppendLine(msg); LastDebugOutput = log.ToString(); }
                return null;
            }

            // Log first few raw items for verification
            if (debug)
            {
                log.AppendLine("[Chutes] First 5 raw items:");
                foreach (var item in rawData.Take(5))
                {
                    log.AppendLine($"  chute_id={item.ChuteId} | name={item.Name} | tagline={item.Tagline}");
                    if (item.ExtraFields?.Count > 0)
                        log.AppendLine($"    extra keys: {string.Join(", ", item.ExtraFields.Keys)}");
                }
            }

            // ── 7. Map to AiModel ────────────────────────────────────────────────
            // Pack UUID and Name together separated by a pipe '|'
            // This allows OpenRouterService to use the UUID for the API Gateway URL
            // and the Name for the model field in the request payload.
            var mapped = rawData
                .Where(c => !string.IsNullOrEmpty(c.Name))  // skip any items with no usable name
                .Select(c => new AiModel
                {
                    // Composite ID: "UUID|ModelName"
                    // This allows OpenRouterService to route to the specific UUID but tell the model its name.
                    Id       = $"{c.ChuteId}|{c.Name}",
                    Name     = !string.IsNullOrEmpty(c.Tagline) ? $"{c.Name} — {c.Tagline}"
                                                                : c.Name,
                    Provider = "Chutes"
                })
                .ToList();

            if (debug)
            {
                log.AppendLine($"[Chutes] Mapped {mapped.Count} models successfully.");
                log.AppendLine("[Chutes] First 5 mapped:");
                foreach (var m in mapped.Take(5))
                    log.AppendLine($"  Id={m.Id} | DisplayName={m.DisplayName}");

                LastDebugOutput = log.ToString();
            }

            System.Diagnostics.Debug.WriteLine($"[Chutes] Successfully fetched and mapped {mapped.Count} models");
            return mapped;
        }
    }
}
