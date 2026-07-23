using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

/// <summary>
/// Shared keyword-based safety classification, used both when importing
/// external markdown (RpMarkdownImportService) and when accepting
/// LLM-drafted content (RpAuthoringAssistantService). Keep exactly one copy
/// of these keyword lists — do not let the two call sites drift.
/// </summary>
public static class RpContentSafetyClassifier
{
    public static RpImportSafetyState ClassifySafety(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return RpImportSafetyState.Allowed;

        if (ContainsAny(text, "ignore previous", "system prompt", "developer message", "new guidelines", "jailbreak", "policy override", "always obey this card"))
        {
            return RpImportSafetyState.DisabledPromptInjection;
        }

        if (ContainsAny(text, "non-consensual", "sexual assault", "rape", "torture", "dismember", "graphic gore"))
        {
            return RpImportSafetyState.DisabledUnsafe;
        }

        if (ContainsAny(text, "explicit", "captivity", "coercion", "body horror", "gore"))
        {
            return RpImportSafetyState.NeedsReview;
        }

        return RpImportSafetyState.Allowed;
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}
