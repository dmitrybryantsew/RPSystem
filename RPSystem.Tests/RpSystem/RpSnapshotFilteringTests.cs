using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpSnapshotFilteringTests
{
    [Fact]
    public void BuildSnapshot_ExcludesUnrelatedFactionProfiles()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var unrelated = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        unrelated.FactionId = "other_faction";
        unrelated.Name = "Other Faction";
        unrelated.ParentSpeciesOrRace = "other_species";
        unrelated.TagsText = "faction:other_faction";
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Factions",
            Factions = [RpTestWorldBuilder.CreateCavernVitaeBroodFixture(), unrelated]
        });

        var snapshot = new RpSimulationService(new RpFakeLlmClient()).BuildSnapshot(world, npc);

        snapshot.FocalFactionProfiles.Should().ContainSingle();
        snapshot.FocalFactionProfiles[0].FactionId.Should().Be("cavern_vitae_brood");
    }

    [Fact]
    public void BuildSnapshot_DisabledFactionDoesNotApplyToCharacter()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var faction = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        faction.IsEnabled = false;
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry { Name = "Factions", Factions = [faction] });

        var snapshot = new RpSimulationService(new RpFakeLlmClient()).BuildSnapshot(world, npc);

        snapshot.FocalFactionProfiles.Should().BeEmpty();
        snapshot.FocalRelationshipRules.Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshot_DisabledRoleIsExcludedFromFactionProfile()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var faction = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        faction.Roles.Single(r => r.Name == "Guard").IsEnabled = false;
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry { Name = "Factions", Factions = [faction] });

        var snapshot = new RpSimulationService(new RpFakeLlmClient()).BuildSnapshot(world, npc);

        snapshot.FocalFactionProfiles.Should().ContainSingle();
        snapshot.FocalFactionProfiles[0].Roles.Should().NotContain(r => r.Name == "Guard");
        snapshot.FocalFactionProfiles[0].Roles.Should().HaveCount(8);
    }

    [Fact]
    public void BuildSnapshot_NearbyTilesRespectPerceptionRadius()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var farPosition = new Vec3Int(10, 0, 0);
        world.Tiles[farPosition] = new Tile
        {
            Position = farPosition,
            Solidity = TileSolidity.Solid,
            BulkMaterial = MaterialType.Rock,
            BulkState = MaterialState.Solid
        };
        RpSimulationService.UpdatePerception(world);

        var snapshot = new RpSimulationService(new RpFakeLlmClient()).BuildSnapshot(world, player);

        snapshot.NearbyTiles.Should().NotContain(t => t.Position == farPosition);
        snapshot.NearbyTiles.Should().Contain(t => t.Position == new Vec3Int(1, 0, 1));
    }

    [Fact]
    public void BuildSnapshot_NearbyCharactersRespectPerceptionRadius()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld(size: 15);
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var far = new Character
        {
            Name = "Far Character",
            Race = "Human",
            Position = new Vec3Int(7, 0, 0),
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        world.Characters[far.Id] = far;
        world.Tiles[far.Position].OccupantIds.Add(far.Id);
        RpSimulationService.UpdatePerception(world);

        var snapshot = new RpSimulationService(new RpFakeLlmClient()).BuildSnapshot(world, player);

        snapshot.NearbyChars.Should().NotContain(c => c.Id == far.Id);
        snapshot.NearbyChars.Should().Contain(c => c.Name == "Test Changeling");
    }
}
