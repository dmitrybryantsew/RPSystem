namespace RPSystem.Core.Services;

/// <summary>
/// Compresses a list of narrative lines into a shorter list of "salient" lines.
/// Kept as an interface so a smarter (LLM-backed) implementation can be swapped
/// in later without touching RpMemoryCompactionService. The default
/// implementation is deliberately simple and deterministic (no LLM call) so
/// memory compaction never adds latency or cost to a tick.
/// </summary>
public interface IRpTextSummarizer
{
    /// <param name="lines">Lines to compress, in chronological order.</param>
    /// <param name="targetSentences">Approximate number of output lines to aim for.</param>
    /// <returns>A single string, lines joined with ". " or similar — never empty
    /// unless the input was empty.</returns>
    string Summarize(IReadOnlyList<string> lines, int targetSentences);
}
