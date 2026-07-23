using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpMemoryCompactionServiceTests
{
    [Fact]
    public void CompactCharacterMemory_NoOpBelowCap()
    {
        var character = new Character { Name = "Test" };
        for (var i = 0; i < 10; i++)
        {
            character.PerceivedLog.Add(new NarrativeEvent { Tick = i, ActorName = "X", Description = $"e{i}" });
        }

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.CompactCharacterMemory(character);

        character.PerceivedLog.Should().HaveCount(10);
        character.MemorySummaries.Should().BeEmpty();
    }

    [Fact]
    public void CompactCharacterMemory_MovesOverflowIntoSummary()
    {
        var character = new Character { Name = "Test" };
        for (var i = 0; i < 30; i++)
        {
            character.PerceivedLog.Add(new NarrativeEvent { Tick = i, ActorName = "X", Description = $"e{i}" });
        }

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.CompactCharacterMemory(character);

        character.PerceivedLog.Should().HaveCount(RpMemoryCompactionService.RawPerceivedLogCap);
        character.MemorySummaries.Should().ContainSingle();
    }

    [Fact]
    public void CompactCharacterMemory_SecondTierFoldingTriggersAtCap()
    {
        var character = new Character { Name = "Test" };
        for (var i = 0; i < RpMemoryCompactionService.MaxMemorySummaries; i++)
        {
            character.MemorySummaries.Add($"summary-{i}");
        }
        for (var i = 0; i < 30; i++)
        {
            character.PerceivedLog.Add(new NarrativeEvent { Tick = i, ActorName = "X", Description = $"e{i}" });
        }

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.CompactCharacterMemory(character);

        character.MemorySummaries.Count.Should().BeLessThanOrEqualTo(RpMemoryCompactionService.MaxMemorySummaries);
        character.MemorySummaries[0].Should().StartWith("[Older history]");
    }

    [Fact]
    public void RecordAndMaybeCompactWorldHistory_NoOpBeforeIntervalAndBelowSizeCap()
    {
        var world = new World
        {
            Clock = new WorldClock { TickCount = 10 }
        };
        var events = new List<NarrativeEvent>
        {
            new() { Tick = 10, ActorName = "A", Description = "did a thing" }
        };

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.RecordAndMaybeCompactWorldHistory(world, events);

        world.History.Should().BeEmpty();
        world.GlobalEventLog.Should().HaveCount(1);
    }

    [Fact]
    public void RecordAndMaybeCompactWorldHistory_CompactsAtTickInterval()
    {
        var world = new World
        {
            Clock = new WorldClock { TickCount = RpMemoryCompactionService.HistoryCompactionIntervalTicks },
            LastHistoryCompactionTick = 0
        };
        var events = new List<NarrativeEvent>
        {
            new() { Tick = 1, ActorName = "A", Description = "did a thing" },
            new() { Tick = 2, ActorName = "B", Description = "did another thing" }
        };

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.RecordAndMaybeCompactWorldHistory(world, events);

        world.History.Should().ContainSingle();
        world.History[0].Year.Should().Be(1);
        world.GlobalEventLog.Should().BeEmpty();
        world.LastHistoryCompactionTick.Should().Be(world.Clock.TickCount);
    }

    [Fact]
    public void RecordAndMaybeCompactWorldHistory_CompactsAtSizeCapEvenBeforeInterval()
    {
        var world = new World
        {
            Clock = new WorldClock { TickCount = 5 }
        };
        for (var i = 0; i < RpMemoryCompactionService.MaxRawGlobalEventLog; i++)
        {
            world.GlobalEventLog.Add(new NarrativeEvent { Tick = i, ActorName = "X", Description = $"e{i}" });
        }

        var service = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        service.RecordAndMaybeCompactWorldHistory(world, []);

        world.History.Should().ContainSingle();
        world.GlobalEventLog.Should().BeEmpty();
    }

    [Fact]
    public void RpRuleBasedTextSummarizer_KeepsFirstLastAndSalientLines()
    {
        var summarizer = new RpRuleBasedTextSummarizer();
        var lines = new List<string>
        {
            "first anchor",
            "second filler",
            "third filler",
            "fourth filler",
            "fifth betrayed someone",
            "sixth filler",
            "seventh filler",
            "eighth filler",
            "ninth filler",
            "last anchor"
        };

        var summary = summarizer.Summarize(lines, 4);

        summary.Should().Contain("first anchor");
        summary.Should().Contain("betrayed");
        summary.Should().Contain("last anchor");
        summary.Should().NotContain("eighth filler");
    }

    [Fact]
    public void RpRuleBasedTextSummarizer_ReturnsAllLinesIfAlreadyShort()
    {
        var summarizer = new RpRuleBasedTextSummarizer();
        var lines = new List<string> { "alpha", "beta" };

        var summary = summarizer.Summarize(lines, 3);

        summary.Should().Contain("alpha");
        summary.Should().Contain("beta");
    }
}
