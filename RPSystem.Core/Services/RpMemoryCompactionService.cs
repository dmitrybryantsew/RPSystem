using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpMemoryCompactionService(IRpTextSummarizer summarizer)
{
    public const int RawPerceivedLogCap = 25; // MUST match RpSimulationService.MaxPerceivedLogSize
    public const int SummaryTargetSentences = 3;
    public const int MaxMemorySummaries = 40;
    public const int MaxRawGlobalEventLog = 1000;
    public const long HistoryCompactionIntervalTicks = 2000;

    /// <summary>
    /// Call once per character, once per tick, after AppendEventsToPerceivedLogs
    /// has already added this tick's events to character.PerceivedLog. If
    /// PerceivedLog is within its cap, this is a no-op.
    /// </summary>
    public void CompactCharacterMemory(Character character)
    {
        if (character.PerceivedLog.Count <= RawPerceivedLogCap) return;

        var overflowCount = character.PerceivedLog.Count - RawPerceivedLogCap;
        var overflow = character.PerceivedLog.Take(overflowCount).ToList();
        var lines = overflow.Select(e => $"T{e.Tick} {e.ActorName}: {e.Description}").ToList();
        var summary = summarizer.Summarize(lines, SummaryTargetSentences);

        if (!string.IsNullOrWhiteSpace(summary))
        {
            character.MemorySummaries.Add($"[T{overflow[0].Tick}-T{overflow[^1].Tick}] {summary}");
            CompactSummariesIfNeeded(character);
        }

        character.PerceivedLog = character.PerceivedLog.Skip(overflowCount).ToList();
    }

    private void CompactSummariesIfNeeded(Character character)
    {
        if (character.MemorySummaries.Count <= MaxMemorySummaries) return;

        var foldCount = MaxMemorySummaries / 2;
        var toFold = character.MemorySummaries.Take(foldCount).ToList();
        var folded = summarizer.Summarize(toFold, SummaryTargetSentences);

        character.MemorySummaries = character.MemorySummaries.Skip(foldCount).ToList();
        character.MemorySummaries.Insert(0, $"[Older history] {folded}");
    }

    /// <summary>
    /// Call once per tick (not per character) after all characters' tick
    /// processing is done. Appends this tick's events to World.GlobalEventLog,
    /// then compacts into World.History if either the tick interval has
    /// elapsed or the raw log has grown past MaxRawGlobalEventLog.
    /// </summary>
    public void RecordAndMaybeCompactWorldHistory(World world, IReadOnlyList<NarrativeEvent> tickEvents)
    {
        world.GlobalEventLog.AddRange(tickEvents);

        var dueByInterval = world.Clock.TickCount - world.LastHistoryCompactionTick >= HistoryCompactionIntervalTicks;
        var dueBySize = world.GlobalEventLog.Count >= MaxRawGlobalEventLog;
        if (!dueByInterval && !dueBySize) return;
        if (world.GlobalEventLog.Count == 0) return;

        var lines = world.GlobalEventLog
            .Select(e => $"T{e.Tick} {e.ActorName}: {e.Description}")
            .ToList();
        var summary = summarizer.Summarize(lines, 5);

        // NOTE: Year here is a sequential epoch index (world.History.Count + 1),
        // NOT a calendar year — AdvanceClock does not currently roll Day into
        // Month/Year. Replace this with world.Clock.Year once that is fixed
        // in a separate change.
        world.History.Add(new HistoryYear
        {
            Year = world.History.Count + 1,
            Events = lines.Take(20).ToList(),
            Summary = summary
        });

        world.GlobalEventLog.Clear();
        world.LastHistoryCompactionTick = world.Clock.TickCount;
    }
}
