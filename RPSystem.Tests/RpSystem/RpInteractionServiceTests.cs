using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpInteractionServiceTests
{
    private readonly RpInteractionService _interaction = new();

    [Fact]
    public void Talk_NoCharacterOnInspectedTileFailsWithUsefulMessage()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");

        var evt = _interaction.CreateTalkEvent(world, player, new Vec3Int(0, 0, 1), out string status);

        status.Should().Contain("No character");
        evt.Description.Should().BeEmpty();
    }

    [Fact]
    public void Talk_CharacterOnInspectedTileCreatesNarrativeEvent()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");

        var evt = _interaction.CreateTalkEvent(world, player, new Vec3Int(-1, 0, -1), out string status);

        status.Should().Contain("Test Changeling");
        evt.Description.Should().Contain("greeted Test Changeling");
    }

    [Fact]
    public void Use_RequiresActorAndInspectedTile()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();

        _interaction.CreateUseEvent(world, null, new Vec3Int(0, 0, 0), out string noActorStatus);
        _interaction.CreateUseEvent(world, world.Characters.Values.First(), null, out string noTileStatus);

        noActorStatus.Should().Contain("player character");
        noTileStatus.Should().Contain("Inspect a tile");
    }
}
