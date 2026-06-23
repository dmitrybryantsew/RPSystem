using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPSystem.Core.Models
{
    public class CachedModels
    {
        [JsonPropertyName("cached_at_utc")]
        public DateTime CachedAtUtc { get; set; }

        [JsonPropertyName("models")]
        public List<AiModel> Models { get; set; } = new();
    }

    public class AiModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = string.Empty;

        [JsonPropertyName("provider_model_id")]
        public string ProviderModelId { get; set; } = string.Empty;

        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = string.Empty;

        [JsonPropertyName("modalities")]
        public AiModelModalities? Modalities { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Name) ? Id : Name;
    }

    public class AiModelModalities
    {
        [JsonPropertyName("input")]
        public List<string>? Input { get; set; }

        [JsonPropertyName("output")]
        public List<string>? Output { get; set; }
    }

    public static class AiModelCapabilities
    {
        public static bool IsMultimodalInputCandidate(AiModel model)
        {
            if (model.Modalities?.Input?.Any(value => string.Equals(value, "audio", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }

            if (model.Modalities?.Input?.Any(value => !string.Equals(value, "text", StringComparison.OrdinalIgnoreCase)) == true)
            {
                return true;
            }

            var text = $"{model.Id} {model.Name} {model.ProviderModelId}".ToLowerInvariant();
            return text.Contains("omni", StringComparison.Ordinal) ||
                text.Contains("audio", StringComparison.Ordinal) ||
                text.Contains("vision", StringComparison.Ordinal) ||
                text.Contains("-vl", StringComparison.Ordinal) ||
                text.Contains("_vl", StringComparison.Ordinal) ||
                text.Contains("/vl", StringComparison.Ordinal) ||
                text.Contains("multimodal", StringComparison.Ordinal);
        }
    }

    public class ModelPickerItem
    {
        public bool IsHeader { get; set; }
        public string GroupTitle { get; set; } = string.Empty;
        public AiModel? Model { get; set; }

        public string DisplayText => IsHeader ? GroupTitle : (Model?.DisplayName ?? string.Empty);

        public string SearchableText => IsHeader
            ? GroupTitle
            : $"{Model?.DisplayName} {Model?.Id} {Model?.Provider}".ToLowerInvariant();
    }

    public class ModelListResponse
    {
        [JsonPropertyName("data")]
        public List<AiModel> Data { get; set; } = new();
    }

    public class ChutesListResponse
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("items")]
        public List<ChuteItem> Items { get; set; } = new();
    }

    public class ChuteItem
    {
        [JsonPropertyName("chute_id")]
        public string ChuteId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("tagline")]
        public string Tagline { get; set; } = string.Empty;

        [JsonPropertyName("readme")]
        public string Readme { get; set; } = string.Empty;

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraFields { get; set; } = new();
    }
}
