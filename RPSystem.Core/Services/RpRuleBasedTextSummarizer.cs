namespace RPSystem.Core.Services;

/// <summary>
/// Deterministic, keyword-based summarizer. Keeps the first line (continuity
/// anchor), the last line (recency anchor), and any line containing a salient
/// keyword, up to targetSentences total. This mirrors the keyword-matching
/// style already used throughout RpCodedAiService (see ContainsAny) rather
/// than introducing a new dependency.
///
/// A future LLM-backed implementation of IRpTextSummarizer could be swapped in
/// here without touching RpMemoryCompactionService, but doing so would add
/// per-tick API cost — Phase 2 keeps ticks fast by staying rule-based.
/// </summary>
public sealed class RpRuleBasedTextSummarizer : IRpTextSummarizer
{
    private static readonly string[] SalientKeywords =
    [
        "attack", "attacked", "died", "death", "killed", "learned", "secret",
        "promise", "promised", "betray", "betrayed", "ally", "allied", "enemy",
        "wounded", "escaped", "captured", "born", "built", "destroyed", "fled"
    ];

    public string Summarize(IReadOnlyList<string> lines, int targetSentences)
    {
        if (lines.Count == 0) return string.Empty;
        if (lines.Count <= targetSentences) return string.Join(" ", lines);

        var picked = new List<string> { lines[0] };
        picked.AddRange(lines.Skip(1).Take(lines.Count - 2)
            .Where(line => SalientKeywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase))));
        picked.Add(lines[^1]);

        return string.Join(" ", picked.Distinct().Take(Math.Max(2, targetSentences)));
    }
}
