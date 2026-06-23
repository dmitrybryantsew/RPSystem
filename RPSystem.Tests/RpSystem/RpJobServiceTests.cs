using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace RPSystem.Tests.RpSystem;

public class RpJobServiceTests
{
    [Fact]
    public async Task TickAsync_LocalPatrolJob_MovesUnitTowardCurrentWaypoint()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var unit = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var before = unit.Position;
        unit.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Patrol,
            Name = "test patrol",
            Waypoints = [new Vec3Int(-2, 0, -1), new Vec3Int(-2, 0, 0)],
            Repeat = true
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        var events = await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        unit.Position.Should().NotBe(before);
        unit.Position.Should().Be(new Vec3Int(-2, 0, -1));
        unit.Jobs[0].Status.Should().Be(RpUnitJobStatus.Active);
        events.Should().Contain(evt => evt.ActorName == unit.Name && evt.Description.Contains("moved to"));
    }

    [Fact]
    public async Task TickAsync_LocalFollowJob_MovesUnitCloserToTarget()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var target = world.Characters.Values.First(character => character.Name == "Test Player");
        var follower = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var beforeDistance = DistanceManhattan(follower.Position, target.Position);
        follower.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Follow,
            Name = "follow player",
            TargetCharacterId = target.Id,
            FollowDistance = 1
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        DistanceManhattan(follower.Position, target.Position).Should().BeLessThan(beforeDistance);
        follower.Jobs[0].Status.Should().Be(RpUnitJobStatus.Active);
    }

    [Fact]
    public async Task TickAsync_LocalBuildJob_ConstructsAdjacentTileAndCompletesJob()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var builder = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var target = new Vec3Int(-1, 0, 0);
        builder.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = "build wall",
            TargetPosition = target,
            BuildSolidity = TileSolidity.Solid,
            BuildMaterial = MaterialType.Wood,
            BuildState = MaterialState.Solid
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        world.Tiles[target].Solidity.Should().Be(TileSolidity.Solid);
        world.Tiles[target].BulkMaterial.Should().Be(MaterialType.Wood);
        world.Tiles[target].BulkState.Should().Be(MaterialState.Solid);
        builder.Jobs[0].Status.Should().Be(RpUnitJobStatus.Completed);
    }

    [Fact]
    public async Task TickAsync_LocalBuildJob_ConstructsSideWallAndBlocksMovementAcrossBoundary()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var builder = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var target = new Vec3Int(-1, 0, 0);
        builder.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = "build side wall",
            TargetPosition = target,
            BuildKind = RpBuildKind.SideWall,
            BuildDirection = Direction.South,
            BuildMaterial = MaterialType.Wood,
            BuildState = MaterialState.Solid
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        world.Tiles[target].Sides[(int)Direction.South]!.Material.Should().Be(MaterialType.Wood);
        world.Tiles[target].Sides[(int)Direction.South]!.IsPassable.Should().BeFalse();
        world.Tiles[builder.Position].Sides[(int)Direction.North]!.Material.Should().Be(MaterialType.Wood);
        RpSimulationService.CanEnter(world, builder.Position, target).Should().BeFalse();
        builder.Jobs[0].Status.Should().Be(RpUnitJobStatus.Completed);
    }

    [Fact]
    public async Task TickAsync_LocalBuildJob_ConstructsFurnitureAsGroundItem()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var builder = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var target = new Vec3Int(-1, 0, 0);
        builder.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = "build chair",
            TargetPosition = target,
            BuildKind = RpBuildKind.Furniture,
            BuildMaterial = MaterialType.Wood,
            BuildItemName = "wooden chair",
            BuildItemDescription = "A simple built chair.",
            BuildItemTags = ["chair", "seat"]
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        var item = world.Items.Values.Single(item => item.Name == "wooden chair");
        item.Position.Should().Be(target);
        item.Material.Should().Be(MaterialType.Wood);
        item.Tags.Should().Contain(["built:furniture", "chair", "seat"]);
        world.Tiles[target].OccupantIds.Should().Contain(item.Id);
        builder.Jobs[0].Status.Should().Be(RpUnitJobStatus.Completed);
    }

    [Theory]
    [InlineData(RpBuildKind.RampUp, RpTileMovementFeature.RampUp)]
    [InlineData(RpBuildKind.RampDown, RpTileMovementFeature.RampDown)]
    [InlineData(RpBuildKind.LadderUp, RpTileMovementFeature.LadderUp)]
    [InlineData(RpBuildKind.LadderDown, RpTileMovementFeature.LadderDown)]
    public async Task TickAsync_LocalBuildJob_ConstructsMovementFeature(RpBuildKind buildKind, RpTileMovementFeature expectedFeature)
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var builder = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var target = new Vec3Int(-1, 0, 0);
        builder.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = $"build {buildKind}",
            TargetPosition = target,
            BuildKind = buildKind,
            BuildMaterial = MaterialType.Wood,
            BuildState = MaterialState.Solid
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        world.Tiles[target].MovementFeatures.Should().Contain(expectedFeature);
        builder.Jobs[0].Status.Should().Be(RpUnitJobStatus.Completed);
    }

    [Theory]
    [InlineData(RpBuildKind.Door, SideFeature.Door, "D")]
    [InlineData(RpBuildKind.Window, SideFeature.Window, "+")]
    public async Task TickAsync_LocalBuildJob_ConstructsSideFeature(RpBuildKind buildKind, SideFeature expectedFeature, string expectedGlyph)
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var builder = world.Characters.Values.First(character => character.Name == "Test Changeling");
        var target = new Vec3Int(-1, 0, 0);
        builder.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = $"build {buildKind}",
            TargetPosition = target,
            BuildKind = buildKind,
            BuildDirection = Direction.South,
            BuildMaterial = MaterialType.Wood,
            BuildState = MaterialState.Solid
        });
        var simulation = new RpSimulationService(new ThrowingLlmClient());
        var inspection = new RpWorldInspectionService();

        await simulation.TickAsync(world, useLlm: false, provider: string.Empty, apiKey: string.Empty, model: string.Empty, playerCharacterId: null, CancellationToken.None);

        world.Tiles[target].Sides[(int)Direction.South]!.Feature.Should().Be(expectedFeature);
        inspection.GetTileGlyph(world, target).Should().Be(expectedGlyph);
        builder.Jobs[0].Status.Should().Be(RpUnitJobStatus.Completed);
    }

    private static int DistanceManhattan(Vec3Int a, Vec3Int b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private sealed class ThrowingLlmClient : IRpLlmClient
    {
        public Task<LlmActionResponse> GetActionAsync(LlmSnapshot snapshot, string provider, string apiKey, string model, CancellationToken cancellationToken)
            => throw new InvalidOperationException("LLM should not be used by local job ticks.");
    }
}
