using Xunit;
using FluentAssertions;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpAbilityServiceTests
{
    private readonly RpAbilityService _abilities = new();

    [Fact]
    public void CanUseAbility_ReturnsFalseWhenManaInsufficient()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var c = w.Characters.Values.First(ch => ch.Name == "Test Player");
        c.Vitals.ManaCurrent = 5;
        c.RpAbilities.Add(RpAbilityService.CreateFireballAbility());
        bool ok = _abilities.TryCastFireball(w, c, new Vec3Int(1, 0, 0), out _, out string status);
        ok.Should().BeFalse();
        status.Should().Contain("mana");
    }

    [Fact]
    public void UseFireball_ConsumesManaAndAppliesDamage()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var caster = w.Characters.Values.First(ch => ch.Name == "Test Player");
        var target = w.Characters.Values.First(ch => ch.Name == "Test Changeling");
        caster.RpAbilities.Add(RpAbilityService.CreateFireballAbility());
        float manaBefore = caster.Vitals.ManaCurrent;
        float healthBefore = target.Vitals.HealthCurrent;
        float criticalPartHpBefore = target.Body.First(p => p.IsCritical).HpCurrent;

        bool ok = _abilities.TryCastFireball(w, caster, new Vec3Int(-1, 0, -1), out var evt, out _);

        ok.Should().BeTrue();
        caster.Vitals.ManaCurrent.Should().Be(manaBefore - 20);
        target.Vitals.HealthCurrent.Should().Be(healthBefore - 30);
        target.Body.First(p => p.IsCritical).HpCurrent.Should().BeLessThan(criticalPartHpBefore);
        target.Vitals.LifeState.Should().Be(RpLifeState.Unconscious);
        evt.Description.Should().Contain("Test Changeling");
    }

    [Fact]
    public void Cooldown_PreventsRepeatedUseUntilTicksPass()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var c = w.Characters.Values.First(ch => ch.Name == "Test Player");
        c.RpAbilities.Add(RpAbilityService.CreateFireballAbility());
        _abilities.TryCastFireball(w, c, new Vec3Int(1, 0, 0), out _, out _);
        bool second = _abilities.TryCastFireball(w, c, new Vec3Int(1, 0, 0), out _, out string status);
        second.Should().BeFalse();
        status.Should().Contain("cooldown");
        c.RpAbilities.Single(a => a.Id == RpAbilityService.FireballId).RemainingCooldownTicks.Should().Be(1);
    }

    [Fact]
    public void UseFireball_ReturnsFalseWhenTargetIsOutOfRange()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var caster = w.Characters.Values.First(ch => ch.Name == "Test Player");
        caster.RpAbilities.Add(RpAbilityService.CreateFireballAbility());

        bool ok = _abilities.TryCastFireball(w, caster, new Vec3Int(20, 0, 0), out _, out string status);

        ok.Should().BeFalse();
        status.Should().Contain("out of range");
        caster.Vitals.ManaCurrent.Should().Be(50);
    }
}
