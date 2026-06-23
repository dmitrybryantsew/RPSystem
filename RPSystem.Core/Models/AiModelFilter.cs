using System;
using System.Collections.Generic;
using System.Linq;
using RPSystem.Core.Services;

namespace RPSystem.Core.Models
{
    public static class AiModelFilter
    {
        private static readonly DateTime EarliestSaneModelCreatedUtc = new(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static List<AiModel> Apply(IEnumerable<AiModel> models, ISettingsService settings, DateTime utcNow)
        {
            var hiddenPatterns = ParseHiddenPatterns(settings.HiddenModelIds);
            var cutoff = utcNow.AddDays(-Math.Max(1, settings.ModelMaxAgeDays));

            return models
                .Where(model => ShouldShow(model, settings, cutoff, utcNow, hiddenPatterns))
                .ToList();
        }

        private static bool ShouldShow(
            AiModel model,
            ISettingsService settings,
            DateTime cutoff,
            DateTime utcNow,
            IReadOnlyList<string> hiddenPatterns)
        {
            if (!settings.ShowG4fProxyModels && IsG4fModel(model))
            {
                return false;
            }

            if (settings.HideModelsOlderThanMaxAge && IsOlderThan(model, cutoff, utcNow))
            {
                return false;
            }

            return !hiddenPatterns.Any(pattern => MatchesPattern(model, pattern));
        }

        private static bool IsOlderThan(AiModel model, DateTime cutoff, DateTime utcNow)
        {
            if (model.Created <= 0)
            {
                return false;
            }

            var createdAt = DateTimeOffset.FromUnixTimeSeconds(model.Created).UtcDateTime;
            if (!IsSaneModelCreatedAt(createdAt, utcNow))
            {
                return false;
            }

            return createdAt < cutoff;
        }

        private static bool IsSaneModelCreatedAt(DateTime createdAtUtc, DateTime utcNow)
        {
            return createdAtUtc >= EarliestSaneModelCreatedUtc &&
                createdAtUtc <= utcNow.AddYears(1);
        }

        private static bool IsG4fModel(AiModel model)
        {
            return model.Id.StartsWith("g4f:", StringComparison.OrdinalIgnoreCase) ||
                model.ProviderModelId.StartsWith("g4f:", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(model.Provider, "g4f", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> ParseHiddenPatterns(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('#')[0].Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        private static bool MatchesPattern(AiModel model, string pattern)
        {
            var candidates = new[]
            {
                model.Id,
                model.Name,
                model.DisplayName,
                model.ProviderModelId,
                model.OwnedBy
            };

            return candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Select(candidate => candidate.ToLowerInvariant())
                .Any(candidate => MatchesText(candidate, pattern));
        }

        private static bool MatchesText(string candidate, string pattern)
        {
            if (pattern.StartsWith("*", StringComparison.Ordinal) &&
                pattern.EndsWith("*", StringComparison.Ordinal) &&
                pattern.Length > 2)
            {
                return candidate.Contains(pattern[1..^1], StringComparison.Ordinal);
            }

            if (pattern.StartsWith("*", StringComparison.Ordinal) && pattern.Length > 1)
            {
                return candidate.EndsWith(pattern[1..], StringComparison.Ordinal);
            }

            if (pattern.EndsWith("*", StringComparison.Ordinal) && pattern.Length > 1)
            {
                return candidate.StartsWith(pattern[..^1], StringComparison.Ordinal);
            }

            return string.Equals(candidate, pattern, StringComparison.Ordinal);
        }
    }
}
