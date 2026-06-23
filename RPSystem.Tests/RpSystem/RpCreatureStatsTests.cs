using Xunit;
using FluentAssertions;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpCreatureStatsTests
{
    [Fact]
    public void EnsureCreatureStats_AddsHealthManaFocusStamina()
    {
        var c = new Character { Name = "T", Race = "Human", RpTags = ["creature", "sapient"] };
        RpCreatureService.EnsureCreatureStats(c);
        c.Vitals.HealthMax.Should().BeGreaterThan(0);
        c.Vitals.StaminaMax.Should().BeGreaterThan(0);
        c.Vitals.FocusMax.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EnsureCreatureStats_ClampsCurrentToMax()
    {
        var c = new Character { Name = "O", RpTags = ["creature"], Vitals = new RpVitals { HealthMax = 100, HealthCurrent = 999 } };
        RpCreatureService.EnsureCreatureStats(c);
        c.Vitals.HealthCurrent.Should().BeLessOrEqualTo(c.Vitals.HealthMax);
    }

    [Fact]
    public void EnsureCreatureStats_AddsCreatureTagIfMissing()
    {
        var c = new Character { Name = "N", RpTags = [] };
        RpCreatureService.EnsureCreatureStats(c);
        c.RpTags.Should().Contain("creature");
    }

    [Fact]
    public async Task Regeneration_PerTick_IncreasesResourcesWithoutExceedingMax()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        p.Vitals.ManaCurrent = 49;
        p.Vitals.FocusCurrent = 29;
        p.Vitals.StaminaCurrent = 99;
        var service = new RpSimulationService(new RpFakeLlmClient());

        await service.TickAsync(w, useLlm: false, provider: "", apiKey: "", model: "", p.Id, CancellationToken.None);

        p.Vitals.ManaCurrent.Should().Be(50);
        p.Vitals.FocusCurrent.Should().Be(30);
        p.Vitals.StaminaCurrent.Should().Be(100);
    }

    [Fact]
    public async Task SixSecondTick_AdvancesClockBySixSeconds()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        var service = new RpSimulationService(new RpFakeLlmClient());

        await service.TickAsync(w, useLlm: false, provider: "", apiKey: "", model: "", p.Id, CancellationToken.None);

        w.Clock.TickCount.Should().Be(1);
        w.Clock.Second.Should().Be(6);
        w.Clock.Display.Should().Contain("12:00:06");
    }
}
