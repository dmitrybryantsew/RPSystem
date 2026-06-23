using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;
using FluentAssertions;
using Xunit;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpPathfindingServiceTests
{
    private readonly RpPathfindingService _pathfinding = new();
    private readonly RpFlowFieldService _flowFields = new();

    [Fact]
    public void FindPath_RoutesAroundSolidObstacle()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var character = world.Characters.Values.First(c => c.Name == "Test Player");

        var result = _pathfinding.FindPath(world, character, new Vec3Int(2, 0, 2));

        result.Success.Should().BeTrue(result.FailureReason);
        result.MovementMode.Should().Be(RpMovementMode.Walk);
        result.Steps.Should().NotContain(new Vec3Int(1, 0, 1));
        result.NextStep.Should().NotBeNull();
        result.NodesSearched.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FindPath_StopsAtConfiguredSearchLimit()
    {
        var world = CreateOpenWorld(31);
        var character = CreateCharacter("Walker", new Vec3Int(-15, 0, -15));
        AddCharacter(world, character);

        var result = _pathfinding.FindPath(
            world,
            character,
            new Vec3Int(15, 0, 15),
            new RpPathfindingOptions { MaxVisitedNodes = 3, AllowPartial = false });

        result.Success.Should().BeFalse();
        result.IsPartial.Should().BeFalse();
        result.NodesSearched.Should().BeLessThanOrEqualTo(3);
        result.FailureReason.Should().Contain("Search limit");
    }

    [Fact]
    public void GetUsableMovementModes_AddsFlyWhenCharacterHasWorkingWings()
    {
        var character = CreateCharacter("Winged", new Vec3Int(0, 0, 0), BodyTypeKind.Avian);

        var modes = RpPathfindingService.GetUsableMovementModes(character);

        modes.Should().Contain(RpMovementMode.Walk);
        modes.Should().Contain(RpMovementMode.Fly);
    }

    [Fact]
    public void FindPath_WalkDoesNotEnterLiquidButSwimDoes()
    {
        var world = CreateOpenWorld(3);
        var target = new Vec3Int(1, 0, 0);
        world.Tiles[target].BulkState = MaterialState.Liquid;
        world.Tiles[target].BulkMaterial = MaterialType.Water;
        world.Tiles[target].FluidLevel = 10;

        var walker = CreateCharacter("Walker", new Vec3Int(0, 0, 0));
        AddCharacter(world, walker);

        var swimmer = CreateCharacter("Swimmer", new Vec3Int(0, 0, 0));
        swimmer.Movement.Modes = [RpMovementMode.Swim];
        AddCharacter(world, swimmer);

        var walkingResult = _pathfinding.FindPath(world, walker, target, new RpPathfindingOptions { AllowPartial = false });
        var swimmingResult = _pathfinding.FindPath(world, swimmer, target);

        walkingResult.Success.Should().BeFalse();
        swimmingResult.Success.Should().BeTrue(swimmingResult.FailureReason);
        swimmingResult.MovementMode.Should().Be(RpMovementMode.Swim);
        swimmingResult.NextStep.Should().Be(target);
    }

    [Fact]
    public void FindPath_TeleportStubMovesCloserWithinRange()
    {
        var world = CreateOpenWorld(9);
        var character = CreateCharacter("Blinker", new Vec3Int(0, 0, 0));
        character.RpTags.Add("teleport");
        character.Movement.Modes = [RpMovementMode.Teleport];
        character.Movement.TeleportRange = 3;
        AddCharacter(world, character);

        var result = _pathfinding.FindPath(world, character, new Vec3Int(4, 0, 0));

        result.Success.Should().BeFalse();
        result.IsPartial.Should().BeTrue();
        result.MovementMode.Should().Be(RpMovementMode.Teleport);
        result.NextStep.Should().Be(new Vec3Int(3, 0, 0));
    }

    [Fact]
    public void FlowField_BakesNextStepsTowardTargetForMovementLayer()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var character = world.Characters.Values.First(c => c.Name == "Test Player");

        var field = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, new Vec3Int(2, 0, 2));
        var hasStep = field.TryGetNextStep(character.Position, out var nextStep);

        hasStep.Should().BeTrue(field.FailureReason);
        nextStep.Should().BeOneOf(new Vec3Int(1, 0, 0), new Vec3Int(0, 0, 1));
        field.Cells.Should().NotContainKey(new Vec3Int(1, 0, 1));
        field.Cells[character.Position].CostToTarget.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FlowField_ReusesCachedFieldForSameMapMovementAndTarget()
    {
        var world = CreateOpenWorld(5);

        var first = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, new Vec3Int(2, 0, 0));
        var second = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, new Vec3Int(2, 0, 0));

        second.Should().BeSameAs(first);
        _flowFields.CachedFieldCount.Should().Be(1);
    }

    [Fact]
    public void FlowField_UsesSeparateMovementLayersForWalkAndSwim()
    {
        var world = CreateOpenWorld(3);
        var water = new Vec3Int(1, 0, 0);
        world.Tiles[water].BulkState = MaterialState.Liquid;
        world.Tiles[water].BulkMaterial = MaterialType.Water;
        world.Tiles[water].FluidLevel = 10;

        var walkField = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, water);
        var swimField = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Swim, water);

        walkField.TryGetNextStep(new Vec3Int(0, 0, 0), out _).Should().BeFalse();
        swimField.TryGetNextStep(new Vec3Int(0, 0, 0), out var swimStep).Should().BeTrue();
        swimStep.Should().Be(water);
        _flowFields.CachedFieldCount.Should().Be(2);
    }

    [Fact]
    public void FlowField_RebakesWhenMapTraversalChanges()
    {
        var world = CreateOpenWorld(5);
        var target = new Vec3Int(2, 0, 0);
        var origin = new Vec3Int(0, 0, 0);

        var first = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, target);
        first.TryGetNextStep(origin, out var firstStep).Should().BeTrue();
        firstStep.Should().Be(new Vec3Int(1, 0, 0));

        world.Tiles[new Vec3Int(1, 0, 0)].Solidity = TileSolidity.Solid;
        world.Tiles[new Vec3Int(1, 0, 0)].BulkMaterial = MaterialType.Rock;
        world.Tiles[new Vec3Int(1, 0, 0)].BulkState = MaterialState.Solid;
        world.TerrainVersion++; // real terrain-mutating code paths (RpJobService, ProcessPhysics)
                         // bump this automatically; this test mutates tiles directly,
                         // so it must declare the same intent itself.

        var second = _flowFields.GetOrCreateFlowField(world, RpMovementMode.Walk, target);

        second.Should().NotBeSameAs(first);
        second.TryGetNextStep(origin, out var secondStep).Should().BeTrue();
        secondStep.Should().NotBe(new Vec3Int(1, 0, 0));
        _flowFields.CachedFieldCount.Should().Be(2);
    }

    [Fact]
    public void PathfindingStressWorld_CreatesRequestedRunnerCountWithTargets()
    {
        var world = RpWorldFactory.CreatePathfindingStressWorld(50);

        var runners = world.Characters.Values
            .Where(character => character.RpTags.Contains("path-test-runner"))
            .ToList();

        runners.Should().HaveCount(50);
        runners.Should().OnlyContain(character => character.RpTags.Any(tag => tag.StartsWith("path-test-target:", StringComparison.OrdinalIgnoreCase)));
        runners.Should().OnlyContain(character => character.Movement.Modes.Contains(RpMovementMode.Walk));
    }

    [Fact]
    public async Task TickAsync_LocalPathTestRunner_MovesTowardTaggedTarget()
    {
        var world = RpWorldFactory.CreatePathfindingStressWorld(10);
        var runner = world.Characters.Values.First(character => character.RpTags.Contains("path-test-runner"));
        var before = runner.Position;
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        var events = await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        runner.Position.Should().NotBe(before);
        events.Should().Contain(evt => evt.ActorName == runner.Name && evt.Description.Contains("moved to"));
    }

    [Fact]
    public void VerticalPathfindingWorld_AllowsWalkersAndFlyersToFindRoutes()
    {
        var world = RpWorldFactory.CreateVerticalPathfindingTestWorld();
        var walker = world.Characters.Values.First(character => character.Name == "Ramp Walker A");
        var flyer = world.Characters.Values.First(character => character.Name == "Winged Flyer A");

        var walkerResult = _pathfinding.FindPath(world, walker, new Vec3Int(10, 2, 4));
        var flyerResult = _pathfinding.FindPath(world, flyer, new Vec3Int(10, 2, -5));

        walkerResult.Success.Should().BeTrue(walkerResult.FailureReason);
        walkerResult.MovementMode.Should().Be(RpMovementMode.Walk);
        flyerResult.Success.Should().BeTrue(flyerResult.FailureReason);
        flyerResult.MovementMode.Should().Be(RpMovementMode.Fly);
    }

    [Fact]
    public void GlassAtriumFlightWorld_FlyerPathCrossesOpenSpaceToUpperTarget()
    {
        var world = RpWorldFactory.CreateGlassAtriumFlightTestWorld();
        var flyer = world.Characters.Values.First(character => character.Name == "Atrium Flyer");
        var target = new Vec3Int(0, 9, 0);

        var result = _pathfinding.FindPath(world, flyer, target);

        result.Success.Should().BeTrue(result.FailureReason);
        result.MovementMode.Should().Be(RpMovementMode.Fly);
        result.Steps.Should().Contain(step => IsOpenSpace(world.Tiles[step]));
    }

    [Fact]
    public async Task TickAsync_GlassAtriumFlyer_MovesUpIntoOpenSpace()
    {
        var world = RpWorldFactory.CreateGlassAtriumFlightTestWorld();
        var flyer = world.Characters.Values.First(character => character.Name == "Atrium Flyer");
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        flyer.Position.Should().Be(new Vec3Int(0, 1, 0));
        IsOpenSpace(world.Tiles[flyer.Position]).Should().BeTrue();
    }

    private static World CreateOpenWorld(int size)
    {
        var world = new World { Name = "Open pathfinding world" };
        var half = size / 2;
        for (var x = -half; x <= half; x++)
        {
            for (var z = -half; z <= half; z++)
            {
                var tile = new Tile
                {
                    Position = new Vec3Int(x, 0, z),
                    Solidity = TileSolidity.Empty,
                    BulkMaterial = MaterialType.Air,
                    BulkState = MaterialState.Gas
                };
                world.Tiles[tile.Position] = tile;
            }
        }

        return world;
    }

    private static Character CreateCharacter(string name, Vec3Int position, BodyTypeKind bodyType = BodyTypeKind.Human)
        => new()
        {
            Name = name,
            Race = bodyType.ToString(),
            Position = position,
            BodyType = bodyType,
            Body = RpBodyFactory.CreateBody(bodyType),
            RpTags = ["creature"]
        };

    private static void AddCharacter(World world, Character character)
    {
        world.Characters[character.Id] = character;
        world.Tiles[character.Position].OccupantIds.Add(character.Id);
    }

    private static bool IsOpenSpace(Tile tile)
        => tile.Solidity == TileSolidity.Empty &&
            tile.BulkMaterial == MaterialType.Air &&
            tile.BulkState == MaterialState.Gas &&
            tile.Sides.All(side => side?.Material == null && side?.Feature == null);

    private sealed class ThrowingLlmClient : IRpLlmClient
    {
        public Task<LlmActionResponse> GetActionAsync(LlmSnapshot snapshot, string provider, string apiKey, string model, CancellationToken cancellationToken)
            => throw new InvalidOperationException("LLM should not be used by local path test ticks.");
    }
}
