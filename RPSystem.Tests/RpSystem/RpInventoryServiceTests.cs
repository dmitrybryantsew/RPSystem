using Xunit;
using FluentAssertions;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpInventoryServiceTests
{
    private readonly RpInventoryService _inv = new();

    [Fact]
    public void PickUpItem_AddsToInventoryAndRemovesFromTile()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        w.Tiles[p.Position].OccupantIds.Remove(p.Id);
        p.Position = new Vec3Int(2, 0, 0);
        w.Tiles[p.Position].OccupantIds.Add(p.Id);
        var item = w.Items.Values.First(i => i.Name == "test lever");
        bool ok = _inv.TryPickUp(w, p, item, out string err);
        ok.Should().BeTrue(err);
        p.Inventory.Should().Contain(item.Id);
        item.Position.Should().BeNull();
        item.OwnerId.Should().Be(p.Id);
        w.Tiles[p.Position].OccupantIds.Should().NotContain(item.Id);
    }

    [Fact]
    public void CannotPickUp_ItemOnDifferentTile()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        var item = w.Items.Values.First(i => i.Name == "test lever");
        bool ok = _inv.TryPickUp(w, p, item, out string err);
        ok.Should().BeFalse();
        err.Should().Contain("player tile");
    }

    [Fact]
    public void GetInventorySummary_EmptyInventory()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        _inv.GetInventorySummary(w, p).Should().Contain("empty");
    }

    [Fact]
    public void DropItem_RemovesFromInventoryAndAddsToCurrentTile()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        var item = w.Items.Values.First(i => i.Name == "test lever");
        MoveCharacterToItemTile(w, p, item);
        _inv.TryPickUp(w, p, item, out _).Should().BeTrue();

        bool ok = _inv.TryDrop(w, p, item, out string err);

        ok.Should().BeTrue(err);
        p.Inventory.Should().NotContain(item.Id);
        item.OwnerId.Should().BeNull();
        item.Position.Should().Be(p.Position);
        w.Tiles[p.Position].OccupantIds.Should().Contain(item.Id);
    }

    [Fact]
    public void HoldItem_TracksHeldItemOnCharacterAndBodyPart()
    {
        var w = RpTestWorldBuilder.CreateMinimalWorld();
        var p = w.Characters.Values.First(c => c.Name == "Test Player");
        var item = w.Items.Values.First(i => i.Name == "test lever");
        MoveCharacterToItemTile(w, p, item);
        _inv.TryPickUp(w, p, item, out _).Should().BeTrue();

        bool ok = _inv.TryHold(p, item, out string err);

        ok.Should().BeTrue(err);
        p.HeldItemId.Should().Be(item.Id);
        p.Body.Where(part => part.CanHoldItem).Should().Contain(part => part.HeldItemId == item.Id);
    }

    private static void MoveCharacterToItemTile(World world, Character character, Item item)
    {
        world.Tiles[character.Position].OccupantIds.Remove(character.Id);
        character.Position = item.Position!.Value;
        world.Tiles[character.Position].OccupantIds.Add(character.Id);
    }
}
