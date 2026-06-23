using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpInventoryService
{
    public IReadOnlyList<Item> GetGroundItems(World world, Vec3Int? position)
    {
        if (position == null || !world.Tiles.TryGetValue(position.Value, out var tile))
        {
            return [];
        }

        return tile.OccupantIds
            .Where(world.Items.ContainsKey)
            .Select(id => world.Items[id])
            .OrderBy(item => item.Name)
            .ToList();
    }

    public IReadOnlyList<Item> GetInventoryItems(World world, Character? character)
    {
        if (character == null)
        {
            return [];
        }

        return character.Inventory
            .Where(world.Items.ContainsKey)
            .Select(id => world.Items[id])
            .OrderBy(item => item.Name)
            .ToList();
    }

    public string GetInventorySummary(World world, Character? character)
    {
        if (character == null)
        {
            return "Inventory: no player";
        }

        var items = GetInventoryItems(world, character);
        var heldName = character.HeldItemId.HasValue && world.Items.TryGetValue(character.HeldItemId.Value, out var held)
            ? held.Name
            : "nothing";

        return items.Count == 0
            ? $"Inventory: empty. Holding: {heldName}"
            : $"Inventory: {items.Count} item(s). Holding: {heldName}";
    }

    public bool TryPickUp(World world, Character character, Item item, out string error)
    {
        error = string.Empty;
        if (item.Position != character.Position)
        {
            error = "Item must be on the player tile to pick up.";
            return false;
        }

        if (world.Tiles.TryGetValue(character.Position, out var tile))
        {
            tile.OccupantIds.Remove(item.Id);
        }

        item.Position = null;
        item.OwnerId = character.Id;
        if (!character.Inventory.Contains(item.Id))
        {
            character.Inventory.Add(item.Id);
        }

        return true;
    }

    public bool TryDrop(World world, Character character, Item item, out string error)
    {
        error = string.Empty;
        if (!character.Inventory.Contains(item.Id))
        {
            error = "Selected item is not in inventory.";
            return false;
        }

        character.Inventory.Remove(item.Id);
        if (character.HeldItemId == item.Id)
        {
            character.HeldItemId = null;
        }

        item.OwnerId = null;
        item.Position = character.Position;
        if (world.Tiles.TryGetValue(character.Position, out var tile) &&
            !tile.OccupantIds.Contains(item.Id))
        {
            tile.OccupantIds.Add(item.Id);
        }

        return true;
    }

    public bool TryHold(Character character, Item item, out string error)
    {
        error = string.Empty;
        if (!character.Inventory.Contains(item.Id))
        {
            error = "Selected item is not in inventory.";
            return false;
        }

        character.HeldItemId = item.Id;
        var holdingPart = character.Body.FirstOrDefault(part => part.CanHoldItem);
        if (holdingPart != null)
        {
            holdingPart.HeldItemId = item.Id;
        }

        return true;
    }
}
