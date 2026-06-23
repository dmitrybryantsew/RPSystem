using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using FluentAssertions;
using Xunit;

namespace RPSystem.Tests.RpSystem;

public class RpMapRenderProjectionServiceTests
{
    private readonly RpMapRenderProjectionService _projection = new(new RpWorldInspectionService());

    [Fact]
    public void CreateSnapshot_HorizontalSlice_ProjectsBoundsAndCells()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();

        var snapshot = _projection.CreateSnapshot(world, RpSliceMode.Horizontal, 0);
        var visiblePositions = world.Tiles.Keys.Where(position => position.Y == 0).ToList();

        snapshot.HasCells.Should().BeTrue();
        snapshot.MinA.Should().Be(visiblePositions.Min(position => position.X));
        snapshot.MaxA.Should().Be(visiblePositions.Max(position => position.X));
        snapshot.MinB.Should().Be(visiblePositions.Min(position => position.Z));
        snapshot.MaxB.Should().Be(visiblePositions.Max(position => position.Z));
        snapshot.Cells.Should().HaveCount(visiblePositions.Count);
        snapshot.PositionFromSlice(2, 1).Should().Be(new Vec3Int(2, 0, 1));
        snapshot.Cells.Should().Contain(cell => cell.Position == new Vec3Int(0, 0, 0) && cell.HasCharacter && cell.Kind == "character");
        snapshot.Cells.Should().Contain(cell => cell.Position == new Vec3Int(1, 0, 1) && cell.IsSolid && cell.Kind == "solid");
    }

    [Theory]
    [InlineData(RpTileMovementFeature.RampUp, "▲", "ramp-up")]
    [InlineData(RpTileMovementFeature.RampDown, "▼", "ramp-down")]
    [InlineData(RpTileMovementFeature.LadderUp, "H", "ladder")]
    [InlineData(RpTileMovementFeature.LadderDown, "H", "ladder")]
    public void CreateSnapshot_MovementConnector_ExposesRenderFlags(RpTileMovementFeature feature, string glyph, string kind)
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var position = new Vec3Int(0, 0, 1);
        world.Tiles[position].MovementFeatures.Add(feature);

        var snapshot = _projection.CreateSnapshot(world, RpSliceMode.Horizontal, 0);

        var cell = snapshot.Cells.Should().ContainSingle(candidate => candidate.Position == position).Subject;
        cell.Glyph.Should().Be(glyph);
        cell.Kind.Should().Be(kind);
        cell.IsRampUp.Should().Be(feature == RpTileMovementFeature.RampUp);
        cell.IsRampDown.Should().Be(feature == RpTileMovementFeature.RampDown);
        cell.IsLadder.Should().Be(feature is RpTileMovementFeature.LadderUp or RpTileMovementFeature.LadderDown);
    }

    [Fact]
    public void CreateSnapshot_VerticalSlice_MapsScreenCoordinatesToWorldPosition()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var upper = new Vec3Int(1, 1, 0);
        world.Tiles[upper] = new Tile { Position = upper };

        var snapshot = _projection.CreateSnapshot(world, RpSliceMode.Vertical, 0);

        snapshot.PositionFromSlice(1, 1).Should().Be(upper);
        snapshot.Cells.Should().Contain(cell => cell.Position == upper && cell.A == 1 && cell.B == 1);
    }

    [Fact]
    public void CreateSnapshot_OpenSpace_UsesDimUnderlayGlyphFromTileBelow()
    {
        var world = RpWorldFactory.CreateGlassAtriumFlightTestWorld();
        var position = new Vec3Int(0, 1, 0);

        var snapshot = _projection.CreateSnapshot(world, RpSliceMode.Horizontal, 1);

        var cell = snapshot.Cells.Should().ContainSingle(candidate => candidate.Position == position).Subject;
        cell.Kind.Should().Be("open-space");
        cell.IsOpenSpace.Should().BeTrue();
        cell.Glyph.Should().Be(" ");
        cell.UnderlayGlyph.Should().Be("A");
        cell.UnderlayDepth.Should().Be(1);
        cell.IsUnderlayClipped.Should().BeFalse();
    }

    [Fact]
    public void CreateSnapshot_OpenSpaceBeyondLookDepth_UsesCutoffMarker()
    {
        var world = RpWorldFactory.CreateGlassAtriumFlightTestWorld();
        var position = new Vec3Int(0, 6, 0);

        var snapshot = _projection.CreateSnapshot(world, RpSliceMode.Horizontal, 6, maxOpenSpaceLookDepth: 5);

        var cell = snapshot.Cells.Should().ContainSingle(candidate => candidate.Position == position).Subject;
        cell.IsOpenSpace.Should().BeTrue();
        cell.UnderlayGlyph.Should().Be("?");
        cell.IsUnderlayClipped.Should().BeTrue();
        cell.UnderlayDepth.Should().Be(6);
    }
}
