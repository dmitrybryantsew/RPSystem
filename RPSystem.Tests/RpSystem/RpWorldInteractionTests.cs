using Xunit;
using FluentAssertions;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpWorldInteractionTests
{
    private readonly RpWorldInspectionService _inspection = new();

    [Fact]
    public void InspectTile_SolidTile_ReportsSolid()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var i = _inspection.InspectTile(w, new Vec3Int(1, 0, 1));
        i.Description.Should().Contain("Solid");
    }

    [Fact]
    public void GlassWindow_BlocksMovement()
    {
        var w = RpWorldFactory.CreateGlasshouseOutpostWorld();
        var t = w.Tiles[new Vec3Int(0, 0, -2)];
        var s = t.Sides[(int)Direction.North];
        s.Should().NotBeNull();
        s!.Material.Should().Be(MaterialType.Glass);
        s.IsPassable.Should().BeFalse();
        RpSimulationService.CanEnter(w, new Vec3Int(0, 0, -2), new Vec3Int(0, 0, -1)).Should().BeFalse();
    }

    [Fact]
    public void TryMoveCharacter_GlassWindowDoesNotMoveCharacter()
    {
        var w = RpWorldFactory.CreateGlasshouseOutpostWorld();
        var c = w.Characters.Values.First(ch => ch.Name == "Mira");
        w.Tiles[c.Position].OccupantIds.Remove(c.Id);
        c.Position = new Vec3Int(0, 0, -2);
        w.Tiles[c.Position].OccupantIds.Add(c.Id);

        bool moved = RpSimulationService.TryMoveCharacter(w, c, new Vec3Int(0, 0, -1), out string error);

        moved.Should().BeFalse();
        error.Should().Contain("Blocked");
        c.Position.Should().Be(new Vec3Int(0, 0, -2));
    }

    [Fact]
    public void TryMoveCharacter_SolidTileDoesNotMoveCharacter()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var c = w.Characters.Values.First(ch => ch.Name == "Test Player");

        bool moved = RpSimulationService.TryMoveCharacter(w, c, new Vec3Int(1, 0, 1), out string error);

        moved.Should().BeFalse();
        error.Should().Contain("Blocked");
        c.Position.Should().Be(new Vec3Int(0, 0, 0));
    }

    [Fact]
    public void TryMoveCharacterToward_DistantTargetMovesOnePathStep()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var c = w.Characters.Values.First(ch => ch.Name == "Test Player");

        bool moved = RpSimulationService.TryMoveCharacterToward(w, c, new Vec3Int(2, 0, 2), out string error);

        moved.Should().BeTrue(error);
        c.Position.Should().NotBe(new Vec3Int(2, 0, 2));
        c.Position.Should().NotBe(new Vec3Int(1, 0, 1));
        c.Position.Should().BeOneOf(new Vec3Int(1, 0, 0), new Vec3Int(0, 0, 1));
        w.Tiles[c.Position].OccupantIds.Should().Contain(c.Id);
    }

    [Fact]
    public void TryMoveCharacterToward_TeleportStubMovesCloserWithoutAdjacency()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld(size: 9);
        var c = w.Characters.Values.First(ch => ch.Name == "Test Player");
        c.Movement.Modes = [RpMovementMode.Teleport];
        c.Movement.TeleportRange = 3;
        c.RpTags.Add("teleport");

        bool moved = RpSimulationService.TryMoveCharacterToward(w, c, new Vec3Int(4, 0, 0), out string error);

        moved.Should().BeTrue(error);
        c.Position.Should().Be(new Vec3Int(3, 0, 0));
        w.Tiles[c.Position].OccupantIds.Should().Contain(c.Id);
    }

    [Fact]
    public void InspectTile_InteractiveObjectReportsItem()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var item = w.Items.Values.First(i => i.Name == "test lever");
        item.GoalAffordances.Add(new RpGoalObjectAffordance
        {
            Kind = RpGoalObjectKind.Workstation,
            Name = "mechanism control"
        });

        var inspection = _inspection.InspectTile(w, new Vec3Int(2, 0, 0));

        inspection.Items.Should().ContainSingle(i => i.Name == "test lever");
        inspection.Description.Should().Contain("test lever");
        inspection.Description.Should().Contain("mechanism control");
    }

    [Fact]
    public void InspectTile_EmptyTileReportsNoOccupants()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();

        var inspection = _inspection.InspectTile(w, new Vec3Int(0, 0, 1));

        inspection.Characters.Should().BeEmpty();
        inspection.Items.Should().BeEmpty();
        inspection.Description.Should().Contain("Occupants: none");
    }

    [Fact]
    public void GetTileGlyph_Character_ReturnsFirstLetter()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        _inspection.GetTileGlyph(w, new Vec3Int(0, 0, 0)).Should().Be("T");
    }

    [Fact]
    public void GetTileGlyph_SolidTile_ReturnsHash()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        _inspection.GetTileGlyph(w, new Vec3Int(1, 0, 1)).Should().Be("#");
    }

    [Theory]
    [InlineData(RpTileMovementFeature.RampUp, "▲")]
    [InlineData(RpTileMovementFeature.RampDown, "▼")]
    [InlineData(RpTileMovementFeature.LadderUp, "H")]
    [InlineData(RpTileMovementFeature.LadderDown, "H")]
    public void GetTileGlyph_MovementConnector_ReturnsConnectorGlyph(RpTileMovementFeature feature, string expectedGlyph)
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var position = new Vec3Int(0, 0, 1);
        w.Tiles[position].MovementFeatures.Add(feature);

        _inspection.GetTileGlyph(w, position).Should().Be(expectedGlyph);
        _inspection.InspectTile(w, position).Description.Should().Contain(feature.ToString());
    }
}
