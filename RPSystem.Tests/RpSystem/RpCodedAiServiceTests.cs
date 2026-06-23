using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace RPSystem.Tests.RpSystem;

public class RpCodedAiServiceTests
{
    [Fact]
    public async Task TickAsync_LocalCodedAi_UpdatesNeedsAndRestsWhenStaminaUrgent()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(character => character.Name == "Test Player");
        var unit = world.Characters.Values.First(character => character.Name == "Test Changeling");
        unit.CurrentGoal.Description = "Drive the intruder away.";
        unit.Vitals.StaminaCurrent = 1;
        unit.StaminaCurrent = 1;
        var before = unit.Position;
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        var events = await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, player.Id, CancellationToken.None);

        unit.Position.Should().Be(before);
        unit.Needs.Should().Contain(need => need.Type == RpNeedType.Stamina && need.Urgency > 0.8f);
        events.Should().Contain(evt => evt.ActorName == unit.Name && evt.Description.Contains("Resting because stamina need is urgent"));
    }

    [Fact]
    public async Task TickAsync_LocalCodedAi_DefensiveGoalMovesTowardVisibleHostile()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(character => character.Name == "Test Player");
        var unit = world.Characters.Values.First(character => character.Name == "Test Changeling");
        unit.CurrentGoal.Description = "Protect the brood and drive intruders away.";
        unit.LifeGoal.Description = "Protect the brood.";
        var beforeDistance = DistanceManhattan(unit.Position, player.Position);
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, player.Id, CancellationToken.None);

        DistanceManhattan(unit.Position, player.Position).Should().BeLessThan(beforeDistance);
    }

    [Fact]
    public async Task TickAsync_LocalHiveDirector_AssignsWorkerBuildJobWithoutLlm()
    {
        var world = CreateHiveWorld();
        var worker = world.Characters.Values.First(character => character.RpTags.Contains("role:worker"));
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        var events = await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        worker.Jobs.Should().ContainSingle(job => job.Type == RpUnitJobType.Build);
        worker.Jobs[0].BuildKind.Should().Be(RpBuildKind.Furniture);
        events.Should().Contain(evt => evt.ActorName == "Hive AI" && evt.Description.Contains(worker.Name));
    }

    [Fact]
    public async Task TickAsync_LocalCodedAi_UsesVisibleGoalObjectMatchingGoal()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(character => character.Name == "Test Player");
        var unit = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var item = world.Items.Values.First(item => item.Name == "test lever");
        item.GoalAffordances.Add(new RpGoalObjectAffordance
        {
            Kind = RpGoalObjectKind.Workstation,
            Name = "mechanism control",
            GoalKeywords = ["control", "mechanism", "lever"],
            Priority = 90,
            ResultText = "the mechanism is checked for useful changes"
        });
        unit.CurrentGoal.Description = "Control the mechanism.";
        MoveCharacter(world, unit, new Vec3Int(1, 0, 0));
        RpSimulationService.UpdatePerception(world);
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        var events = await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, player.Id, CancellationToken.None);

        events.Should().Contain(evt => evt.ActorName == unit.Name && evt.Description.Contains("used test lever"));
    }

    private static World CreateHiveWorld()
    {
        var world = new World { Name = "Hive AI test" };
        world.Factions["hive"] = new Faction
        {
            Id = "hive",
            Name = "Test Hive",
            Description = "A changeling hive brood with a queen and workers."
        };

        for (var x = -3; x <= 3; x++)
        {
            for (var z = -3; z <= 3; z++)
            {
                var tile = new Tile
                {
                    Position = new Vec3Int(x, 0, z),
                    Solidity = TileSolidity.Empty,
                    BulkMaterial = MaterialType.Air,
                    BulkState = MaterialState.Gas
                };
                tile.Sides[(int)Direction.Floor] = new Side
                {
                    Direction = Direction.Floor,
                    Material = MaterialType.Rock,
                    Health = 100,
                    IsPassable = true
                };
                world.Tiles[tile.Position] = tile;
            }
        }

        AddCharacter(world, new Character
        {
            Name = "Hive Queen",
            Race = "Changeling",
            Position = new Vec3Int(0, 0, 0),
            FactionId = "hive",
            BodyType = BodyTypeKind.Changeling,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Changeling),
            RpTags = ["creature", "sapient", "changeling", "role:queen"]
        });
        AddCharacter(world, new Character
        {
            Name = "Hive Worker",
            Race = "Changeling",
            Position = new Vec3Int(1, 0, 0),
            FactionId = "hive",
            BodyType = BodyTypeKind.Changeling,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Changeling),
            RpTags = ["creature", "sapient", "changeling", "role:worker"]
        });

        RpSimulationService.UpdatePerception(world);
        return world;
    }

    private static void AddCharacter(World world, Character character)
    {
        world.Characters[character.Id] = character;
        world.Tiles[character.Position].OccupantIds.Add(character.Id);
    }

    private static void MoveCharacter(World world, Character character, Vec3Int position)
    {
        world.Tiles[character.Position].OccupantIds.Remove(character.Id);
        character.Position = position;
        world.Tiles[position].OccupantIds.Add(character.Id);
    }

    private static int DistanceManhattan(Vec3Int a, Vec3Int b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private sealed class ThrowingLlmClient : IRpLlmClient
    {
        public Task<LlmActionResponse> GetActionAsync(LlmSnapshot snapshot, string provider, string apiKey, string model, CancellationToken cancellationToken)
            => throw new InvalidOperationException("LLM should not be used by coded AI tests.");
    }
}
