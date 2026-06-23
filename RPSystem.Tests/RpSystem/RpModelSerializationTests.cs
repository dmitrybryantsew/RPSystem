using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using FluentAssertions;
using RPSystem.Core.RpSystem;

namespace RPSystem.Tests.RpSystem;

public class RpModelSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void RpFactionProfile_RoundTripsThroughJson()
    {
        var faction = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        var json = JsonSerializer.Serialize(faction, JsonOptions);
        var d = JsonSerializer.Deserialize<RpFactionProfile>(json, JsonOptions);
        d.Should().NotBeNull();
        d!.FactionId.Should().Be("cavern_vitae_brood");
        d.Name.Should().Be("Cavern Vitae Brood");
        d.IsEnabled.Should().BeTrue();
        d.Visibility.Should().Be(RpContextVisibility.WorldOnly);
        d.Roles.Should().HaveCount(9);
        d.Abilities.Should().HaveCount(3);
        d.RelationshipRules.Should().HaveCount(3);
        d.Roles.Select(r => r.Name).Should().Contain(["Worker", "Guard", "Queen"]);
        d.Abilities.Select(a => a.Id).Should().Contain(["brood_commune", "resin_bind", "royal_decree"]);
        d.RelationshipRules.Single(r => r.TargetNameOrTag == "player").Suspicion.Should().Be(60);
    }

    [Fact]
    public void RpWorldContextEntry_RoundTripsFactionAndSpeciesData()
    {
        var context = new RpWorldContextEntry
        {
            Name = "Brood Context",
            Factions = [RpTestWorldBuilder.CreateCavernVitaeBroodFixture()],
            SpeciesTemplates = [RpTestWorldBuilder.CreateChangelingSpeciesTemplate()]
        };

        var json = JsonSerializer.Serialize(context, JsonOptions);
        var d = JsonSerializer.Deserialize<RpWorldContextEntry>(json, JsonOptions);

        d.Should().NotBeNull();
        d!.Factions.Should().ContainSingle();
        d.Factions[0].Roles.Should().HaveCount(9);
        d.Factions[0].Abilities.Should().HaveCount(3);
        d.SpeciesTemplates.Should().ContainSingle(s => s.Name == "Changeling");
        d.SpeciesTemplates[0].AnatomyModifiers.Keys.Should().Contain("wings");
    }

    [Fact]
    public void LargeWorldContext_RoundTripsWithoutTruncation()
    {
        var largeText = new string('A', 300000) + "__END__";
        var ctx = new RpWorldContextEntry { Name = "Big", RulesText = largeText };
        var json = JsonSerializer.Serialize(ctx, JsonOptions);
        var d = JsonSerializer.Deserialize<RpWorldContextEntry>(json, JsonOptions);
        d!.RulesText.Should().HaveLength(largeText.Length);
        d.RulesText.Should().EndWith("__END__");
    }

    [Fact]
    public void DisabledFaction_RemainsDisabledAfterRoundTrip()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        f.IsEnabled = false;
        var json = JsonSerializer.Serialize(f, JsonOptions);
        var d = JsonSerializer.Deserialize<RpFactionProfile>(json, JsonOptions);
        d!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RoleAndAbilityIds_AreStableAcrossRoundTrip()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        var roleId = f.Roles[2].Id;
        var abilityId = f.Abilities[1].Id;

        var json = JsonSerializer.Serialize(f, JsonOptions);
        var d = JsonSerializer.Deserialize<RpFactionProfile>(json, JsonOptions);

        d!.Roles[2].Id.Should().Be(roleId);
        d.Abilities[1].Id.Should().Be(abilityId);
        d.Roles[2].AppliesToRoleOrTag.Should().Be("role:guard");
        d.Abilities[1].Tags.Should().Contain("role:guard");
    }
}
