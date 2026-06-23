using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpWorldSaveServiceTests
{
    [Fact]
    public async Task WorldSave_PreservesTilesCharactersContextsAndFactions()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Brood Context",
            Factions = [RpTestWorldBuilder.CreateCavernVitaeBroodFixture()]
        });
        var service = new RpWorldSaveService();

        await service.SaveAsync(world);
        var loaded = await service.LoadAsync();

        loaded.Should().NotBeNull();
        loaded!.Tiles.Should().HaveCount(world.Tiles.Count);
        loaded.Characters.Should().HaveCount(world.Characters.Count);
        loaded.WorldContexts.Should().ContainSingle(c => c.Name == "Brood Context");
        loaded.WorldContexts[0].Factions.Should().ContainSingle(f => f.FactionId == "cavern_vitae_brood");
        loaded.WorldContexts[0].Factions[0].Roles.Should().HaveCount(9);
    }

    [Fact]
    public async Task WorldLoad_EnsuresBodyAndStatsForCharacters()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        player.Body.Clear();
        player.Vitals = new RpVitals { HealthMax = 0, HealthCurrent = 0, StaminaMax = 0, StaminaCurrent = 0 };
        var service = new RpWorldSaveService();

        await service.SaveAsync(world);
        var loaded = await service.LoadAsync();

        var loadedPlayer = loaded!.Characters[player.Id];
        loadedPlayer.Body.Should().NotBeEmpty();
        loadedPlayer.Vitals.HealthMax.Should().BeGreaterThan(0);
        loadedPlayer.Vitals.StaminaMax.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WorldSave_DoesNotLoseZLevels()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        world.Tiles[new Vec3Int(0, 1, 0)] = new Tile { Position = new Vec3Int(0, 1, 0) };
        world.Tiles[new Vec3Int(0, -1, 0)] = new Tile { Position = new Vec3Int(0, -1, 0) };
        var service = new RpWorldSaveService();

        await service.SaveAsync(world);
        var loaded = await service.LoadAsync();

        loaded!.Tiles.Keys.Should().Contain([new Vec3Int(0, 1, 0), new Vec3Int(0, -1, 0)]);
    }
}
