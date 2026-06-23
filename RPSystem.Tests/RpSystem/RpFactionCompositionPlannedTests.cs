using FluentAssertions;
using Xunit;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpFactionCompositionPlannedTests
{
    private readonly RpCharacterCompositionService _composition = new();

    [Fact]
    public void CharacterOverride_BeatsRole()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = CreateContextWithAbilityLayers();
        var profile = CreateGuardProfile();
        profile.StructuredAbilities.Add(Ability("layered", "Character Version", mana: 3));

        var character = _composition.CreateCharacterFromProfile(world, context, profile, new Vec3Int(0, 0, 1));

        character.RpAbilities.Should().ContainSingle(a => a.Id == "layered");
        character.RpAbilities[0].Name.Should().Be("Character Version");
        character.RpAbilities[0].ManaCost.Should().Be(3);
    }

    [Fact]
    public void Role_BeatsFaction()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = CreateContextWithAbilityLayers();
        var profile = CreateGuardProfile();

        var character = _composition.CreateCharacterFromProfile(world, context, profile, new Vec3Int(0, 0, 1));

        character.RpAbilities.Should().ContainSingle(a => a.Id == "layered");
        character.RpAbilities[0].Name.Should().Be("Role Version");
        character.RpAbilities[0].ManaCost.Should().Be(7);
    }

    [Fact]
    public void RoleRestrictedAbility_AppliesOnlyToMatchingRole()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = new RpWorldContextEntry
        {
            Factions =
            [
                new RpFactionProfile
                {
                    FactionId = "brood",
                    IsEnabled = true,
                    Abilities =
                    [
                        Ability("common", "Common", tags: []),
                        Ability("guard_only", "Guard Only", tags: ["role:guard"])
                    ]
                }
            ]
        };

        var guard = _composition.CreateCharacterFromProfile(world, context, CreateGuardProfile(), new Vec3Int(0, 0, 1));
        var worker = _composition.CreateCharacterFromProfile(world, context, new RpWorldContextCharacter
        {
            Name = "Worker",
            Race = "Changeling",
            FactionId = "brood",
            RoleInWorld = "worker",
            TagsText = "creature, sapient, role:worker"
        }, new Vec3Int(0, 0, 2));

        guard.RpAbilities.Select(a => a.Id).Should().Contain(["common", "guard_only"]);
        worker.RpAbilities.Select(a => a.Id).Should().Contain("common");
        worker.RpAbilities.Select(a => a.Id).Should().NotContain("guard_only");
    }

    [Fact]
    public void CharacterWithFactionId_GetsFactionTags()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = new RpWorldContextEntry
        {
            Factions = [new RpFactionProfile { FactionId = "brood", IsEnabled = true, TagsText = "hive, magic:innate" }]
        };

        var character = _composition.CreateCharacterFromProfile(world, context, CreateGuardProfile(), new Vec3Int(0, 0, 1));

        character.FactionId.Should().Be("brood");
        character.RpTags.Should().Contain(["faction:brood", "hive", "magic:innate"]);
    }

    [Fact]
    public void FactionStatOverrides_ParseCompactStats()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = new RpWorldContextEntry
        {
            Factions =
            [
                new RpFactionProfile
                {
                    FactionId = "brood",
                    IsEnabled = true,
                    Roles =
                    [
                        new RpFactionRole
                        {
                            Name = "Guard",
                            IsEnabled = true,
                            AppliesToRoleOrTag = "role:guard",
                            DefaultStatsText = "hp=120; mana=15; focus=30; stamina=90"
                        }
                    ]
                }
            ]
        };

        var character = _composition.CreateCharacterFromProfile(world, context, CreateGuardProfile(), new Vec3Int(0, 0, 1));

        character.Vitals.HealthMax.Should().Be(120);
        character.Vitals.ManaMax.Should().Be(15);
        character.Vitals.FocusMax.Should().Be(30);
        character.Vitals.StaminaMax.Should().Be(90);
        character.StaminaMax.Should().Be(90);
    }

    [Fact]
    public void InvalidRoleStats_DoNotThrowAndPreserveValidEntries()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var context = new RpWorldContextEntry
        {
            Factions =
            [
                new RpFactionProfile
                {
                    FactionId = "brood",
                    IsEnabled = true,
                    Roles =
                    [
                        new RpFactionRole
                        {
                            Name = "Guard",
                            IsEnabled = true,
                            AppliesToRoleOrTag = "role:guard",
                            DefaultStatsText = "hp=bad; mana=25; stamina=-10; nonsense"
                        }
                    ]
                }
            ]
        };

        var act = () => _composition.CreateCharacterFromProfile(world, context, CreateGuardProfile(), new Vec3Int(0, 0, 1));

        var character = act.Should().NotThrow().Subject;
        character.Vitals.HealthMax.Should().Be(100);
        character.Vitals.ManaMax.Should().Be(25);
        character.Vitals.StaminaMax.Should().Be(100);
    }

    private static RpWorldContextEntry CreateContextWithAbilityLayers()
        => new()
        {
            Factions =
            [
                new RpFactionProfile
                {
                    FactionId = "brood",
                    IsEnabled = true,
                    Abilities = [Ability("layered", "Faction Version", mana: 11)],
                    Roles =
                    [
                        new RpFactionRole
                        {
                            Name = "Guard",
                            IsEnabled = true,
                            AppliesToRoleOrTag = "role:guard",
                            Abilities = [Ability("layered", "Role Version", mana: 7)]
                        }
                    ]
                }
            ]
        };

    private static RpWorldContextCharacter CreateGuardProfile()
        => new()
        {
            Name = "Guard",
            Race = "Changeling",
            FactionId = "brood",
            RoleInWorld = "guard",
            TagsText = "creature, sapient, role:guard"
        };

    private static RpAbility Ability(string id, string name, float mana = 0, List<string>? tags = null)
        => new()
        {
            Id = id,
            Name = name,
            TargetKind = RpAbilityTargetKind.Character,
            PrimaryResource = mana > 0 ? RpAbilityResource.Mana : RpAbilityResource.None,
            ManaCost = mana,
            Tags = tags ?? []
        };
}
