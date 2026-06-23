using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;
using FluentAssertions;
using Xunit;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpCaveMapGeneratorTests
{
    private readonly RpCaveMapGenerator _generator = new();
    private readonly RpPathfindingService _pathfinding = new();
    private readonly RpFlowFieldService _flowFields = new();

    [Fact]
    public void Generate_SameSeedCreatesSameCaveLayout()
    {
        var options = new RpCaveMapGenerationOptions { Width = 17, Depth = 17, Levels = 2, Seed = 42 };

        var first = _generator.Generate(options);
        var second = _generator.Generate(options);

        first.World.Tiles.Count.Should().Be(second.World.Tiles.Count);
        first.OpenTileCount.Should().Be(second.OpenTileCount);
        first.SolidTileCount.Should().Be(second.SolidTileCount);
        first.World.Tiles
            .OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.Z)
            .Select(pair => $"{pair.Key}:{pair.Value.Solidity}:{string.Join("/", pair.Value.MovementFeatures.OrderBy(f => f))}")
            .Should()
            .Equal(second.World.Tiles
                .OrderBy(pair => pair.Key.X).ThenBy(pair => pair.Key.Y).ThenBy(pair => pair.Key.Z)
                .Select(pair => $"{pair.Key}:{pair.Value.Solidity}:{string.Join("/", pair.Value.MovementFeatures.OrderBy(f => f))}"));
    }

    [Fact]
    public void Generate_CreatesMultiLevelCaveWithVerticalConnectors()
    {
        var result = _generator.Generate(new RpCaveMapGenerationOptions { Width = 19, Depth = 19, Levels = 3, Seed = 7 });

        result.World.Tiles.Keys.Select(pos => pos.Y).Distinct().Should().BeEquivalentTo([0, 1, 2]);
        result.OpenTileCount.Should().BeGreaterThan(0);
        result.SolidTileCount.Should().BeGreaterThan(0);
        result.VerticalConnectors.Should().HaveCount(2);

        foreach (var connector in result.VerticalConnectors)
        {
            result.World.Tiles[connector.LowerPosition].MovementFeatures.Should().Contain(RpTileMovementFeature.RampUp);
            result.World.Tiles[connector.UpperPosition].MovementFeatures.Should().Contain(RpTileMovementFeature.RampDown);
        }
    }

    [Fact]
    public void Pathfinding_CanWalkAcrossRampConnectorBetweenCaveLevels()
    {
        var result = _generator.Generate(new RpCaveMapGenerationOptions { Width = 21, Depth = 21, Levels = 3, Seed = 99 });
        var character = result.World.Characters.Values.Single(c => c.Name == "Cave Test Player");

        var path = _pathfinding.FindPath(result.World, character, result.ExitPosition, new RpPathfindingOptions { MaxVisitedNodes = 5000 });

        path.Success.Should().BeTrue(path.FailureReason);
        path.Steps.Should().Contain(step => step.Y == 1);
        path.Steps.Should().Contain(step => step.Y == 2);
    }

    [Fact]
    public void Pathfinding_WalkCannotUseLadderButClimbCan()
    {
        var result = _generator.Generate(new RpCaveMapGenerationOptions
        {
            Width = 15,
            Depth = 15,
            Levels = 2,
            Seed = 5,
            ConnectorKind = RpVerticalConnectorKind.Ladder
        });
        var character = result.World.Characters.Values.Single(c => c.Name == "Cave Test Player");

        var walkOnly = _pathfinding.FindPath(result.World, character, result.ExitPosition, new RpPathfindingOptions { AllowPartial = false });
        character.Movement.Modes = [RpMovementMode.Climb];
        var climb = _pathfinding.FindPath(result.World, character, result.ExitPosition, new RpPathfindingOptions { MaxVisitedNodes = 5000 });

        walkOnly.Success.Should().BeFalse();
        climb.Success.Should().BeTrue(climb.FailureReason);
        climb.MovementMode.Should().Be(RpMovementMode.Climb);
    }

    [Fact]
    public void FlowField_CanGuideUnitAcrossGeneratedRampCave()
    {
        var result = _generator.Generate(new RpCaveMapGenerationOptions { Width = 17, Depth = 17, Levels = 2, Seed = 13 });
        var character = result.World.Characters.Values.Single(c => c.Name == "Cave Test Player");

        var step = _flowFields.GetNextStep(result.World, character, result.ExitPosition, maxVisitedNodes: 5000);

        step.Should().NotBeNull();
        RpMovementCostService.TryGetStepCost(result.World, character.Position, step!.Value, RpMovementMode.Walk, out _).Should().BeTrue();
    }
}
