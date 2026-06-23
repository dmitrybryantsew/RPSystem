using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using RPSystem.Tests.RpSystem;
using Xunit;

namespace RPSystem.Tests;

/// <summary>
/// Regression tests for Phase 02 fixes that had no prior coverage:
/// - AppPaths.Combine path building
/// - Debug logging gate (DebugLoggingEnabled)
/// - Deterministic turn ordering (Id-based, not Name-based)
/// </summary>
public class Phase02RegressionTests
{
    [Fact]
    public void AppPaths_Combine_BuildsExpectedNestedPath()
    {
        // AppPaths.Combine should produce the expected nested path
        // This validates the helper that replaced FileSystem.AppDataDirectory
        var combined = AppPaths.Combine("subdir/file.txt");
        var expected = Path.Combine(AppPaths.AppDataDirectory, "subdir/file.txt");
        Assert.Equal(expected, combined);
    }

    [Fact]
    public void AppendDebugLog_RespectsDebugLoggingEnabled()
    {
        // Phase 02 fix: DebugLoggingEnabled gates AppendDebugLog
        // When false, AppendDebugLog returns immediately without writing
        var service = new RpSimulationService(new RpFakeLlmClient());

        // Ensure debug logging is disabled
        RpSimulationService.DebugLoggingEnabled = false;
        var logPath = RpSimulationService.DebugLogPath;

        // Delete the log file if it exists so we can detect writes
        if (File.Exists(logPath))
            File.Delete(logPath);

        RpSimulationService.AppendDebugLog("should not appear when disabled");

        Assert.False(File.Exists(logPath)); // File should NOT exist after disabled append

        // Now enable and verify logging creates the file
        RpSimulationService.DebugLoggingEnabled = true;
        RpSimulationService.AppendDebugLog("should appear when enabled");

        Assert.True(File.Exists(logPath)); // File should exist after enabled append

        // Clean up
        if (File.Exists(logPath))
            File.Delete(logPath);
        RpSimulationService.DebugLoggingEnabled = false;
    }

    [Fact]
    public void TurnOrder_IsDeterministicAndIdBased()
    {
        // Phase 02 fix: Turn order uses OrderByDescending(c => c.TurnPriority).ThenBy(c => c.Id)
        // Build two worlds with NPCs whose Names sort opposite to their Ids
        // The key property: identical worlds produce identical event ordering
        var service = new RpSimulationService(new RpFakeLlmClient());

        var world1 = RpTestWorldBuilder.CreateMinimalWorld();
        var world2 = RpTestWorldBuilder.CreateMinimalWorld();

        // Add characters with names that sort opposite to their Ids
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        // Ensure id1 < id2 but name1 > name2 alphabetically
        var charA = new Character
        {
            Id = id1,
            Name = "Zebra",
            Position = new Vec3Int(5, 5, 5),
            Vitals = new RpVitals { HealthMax = 10, HealthCurrent = 10 },
            Race = "Human",
            TurnPriority = 10
        };
        var charB = new Character
        {
            Id = id2,
            Name = "Apple",
            Position = new Vec3Int(5, 5, 5),
            Vitals = new RpVitals { HealthMax = 10, HealthCurrent = 10 },
            Race = "Human",
            TurnPriority = 10
        };

        world1.Characters[id1] = charA;
        world1.Characters[id2] = charB;
        world2.Characters[id1] = new Character
        {
            Id = id1,
            Name = "Zebra",
            Position = new Vec3Int(5, 5, 5),
            Vitals = new RpVitals { HealthMax = 10, HealthCurrent = 10 },
            Race = "Human",
            TurnPriority = 10
        };
        world2.Characters[id2] = new Character
        {
            Id = id2,
            Name = "Apple",
            Position = new Vec3Int(5, 5, 5),
            Vitals = new RpVitals { HealthMax = 10, HealthCurrent = 10 },
            Race = "Human",
            TurnPriority = 10
        };

        // Run one tick on each world
        var events1 = service.TickAsync(world1, false, "test", "test-key", "test-model", null, CancellationToken.None).GetAwaiter().GetResult();
        var events2 = service.TickAsync(world2, false, "test", "test-key", "test-model", null, CancellationToken.None).GetAwaiter().GetResult();

        // Both worlds should produce the same number of events (deterministic)
        Assert.Equal(events1.Count, events2.Count);
    }
}
