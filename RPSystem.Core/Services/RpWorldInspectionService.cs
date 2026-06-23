using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpWorldInspectionService
{
    public string GetTileGlyph(World world, Vec3Int position)
    {
        var character = world.Characters.Values.FirstOrDefault(c => c.Position == position);
        if (character != null)
        {
            return character.Name.Length == 0 ? "@" : character.Name[0].ToString().ToUpperInvariant();
        }

        var item = world.Items.Values.FirstOrDefault(i => i.Position == position);
        if (item != null)
        {
            return "*";
        }

        if (!world.Tiles.TryGetValue(position, out var tile))
        {
            return " ";
        }

        if (tile.Solidity == TileSolidity.Solid)
        {
            return "#";
        }

        if (tile.Sides.Any(side => side?.Feature == SideFeature.Door))
        {
            return "D";
        }

        if (tile.Sides.Any(side => side?.Feature == SideFeature.Window))
        {
            return "+";
        }

        if (tile.Sides.Any(side => side?.Material != null && side.IsPassable == false))
        {
            return "|";
        }

        if (tile.MovementFeatures.Contains(RpTileMovementFeature.LadderUp) ||
            tile.MovementFeatures.Contains(RpTileMovementFeature.LadderDown))
        {
            return "H";
        }

        if (tile.MovementFeatures.Contains(RpTileMovementFeature.RampUp))
        {
            return "▲";
        }

        if (tile.MovementFeatures.Contains(RpTileMovementFeature.RampDown))
        {
            return "▼";
        }

        var floor = tile.Sides[(int)Direction.Floor]?.Material;
        return floor switch
        {
            MaterialType.Moss => ",",
            MaterialType.Grass => ".",
            MaterialType.Water => "~",
            _ => "."
        };
    }

    public bool TryGetVisibleBounds(World world, RpSliceMode sliceMode, int sliceCoordinate, out int minA, out int maxA, out int minB, out int maxB)
    {
        var positions = world.Tiles.Keys
            .Where(pos => sliceMode == RpSliceMode.Horizontal ? pos.Y == sliceCoordinate : pos.Z == sliceCoordinate)
            .ToList();

        if (positions.Count == 0)
        {
            minA = maxA = minB = maxB = 0;
            return false;
        }

        if (sliceMode == RpSliceMode.Horizontal)
        {
            minA = positions.Min(pos => pos.X);
            maxA = positions.Max(pos => pos.X);
            minB = positions.Min(pos => pos.Z);
            maxB = positions.Max(pos => pos.Z);
        }
        else
        {
            minA = positions.Min(pos => pos.X);
            maxA = positions.Max(pos => pos.X);
            minB = positions.Min(pos => pos.Y);
            maxB = positions.Max(pos => pos.Y);
        }

        return true;
    }

    public TileInspection InspectTile(World world, Vec3Int position)
    {
        if (!world.Tiles.TryGetValue(position, out var tile))
        {
            return new TileInspection(position, $"Tile {position}{Environment.NewLine}Void / no tile data.", [], []);
        }

        var lines = new List<string> { $"Tile {position}", $"Solidity: {tile.Solidity}" };
        if (tile.BulkMaterial.HasValue)
        {
            lines.Add($"Bulk: {tile.BulkMaterial.Value} ({tile.BulkState?.ToString() ?? "state unknown"}), HP {tile.BulkHealth:0.#}");
        }

        lines.Add($"Temperature: {tile.Temperature:0.#}K");
        if (tile.FluidLevel > 0)
        {
            lines.Add($"Fluid: {tile.FluidLevel}/10");
        }

        if (tile.MovementFeatures.Count > 0)
        {
            lines.Add($"Movement: {string.Join(", ", tile.MovementFeatures)}");
        }

        var sides = tile.Sides
            .Where(side => side?.Material != null || side?.Feature != null)
            .Select(side => $"{side!.Direction}: {side.Material?.ToString() ?? "open"} {(side.Feature.HasValue ? side.Feature.Value.ToString() : string.Empty)} {(side.IsPassable ? "passable" : "blocked")}".Trim())
            .ToList();
        if (sides.Count > 0)
        {
            lines.Add("Sides:");
            lines.AddRange(sides.Select(side => $"  {side}"));
        }

        var adjacentBoundaries = GetAdjacentBoundaries(world, position)
            .Where(text => !sides.Any(side => side.StartsWith(text.DirectionText, StringComparison.OrdinalIgnoreCase)))
            .Select(text => text.Description)
            .ToList();
        if (adjacentBoundaries.Count > 0)
        {
            lines.Add("Adjacent boundaries:");
            lines.AddRange(adjacentBoundaries.Select(side => $"  {side}"));
        }

        var characters = tile.OccupantIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id])
            .ToList();
        var items = tile.OccupantIds
            .Where(world.Items.ContainsKey)
            .Select(id => world.Items[id])
            .ToList();

        var occupants = tile.OccupantIds
            .Select(id =>
                world.Characters.TryGetValue(id, out var character)
                    ? $"Character: {character.Name} ({character.Race}, {character.BodyType}), {character.Vitals.LifeState}, HP {character.Vitals.HealthCurrent:0.#}/{character.Vitals.HealthMax:0.#}, mana {character.Vitals.ManaCurrent:0.#}/{character.Vitals.ManaMax:0.#}, focus {character.Vitals.FocusCurrent:0.#}/{character.Vitals.FocusMax:0.#}, stamina {character.Vitals.StaminaCurrent:0.#}/{character.Vitals.StaminaMax:0.#}, mood {character.Mood}, tags {FormatTags(character)}"
                    : world.Items.TryGetValue(id, out var item)
                        ? $"Item: {item.Name} - {item.Description}{FormatAffordances(item)}"
                        : $"Unknown occupant: {id}")
            .ToList();

        lines.Add(occupants.Count == 0 ? "Occupants: none" : "Occupants:");
        if (occupants.Count > 0)
        {
            lines.AddRange(occupants.Select(occupant => $"  {occupant}"));
        }

        return new TileInspection(position, string.Join(Environment.NewLine, lines), characters, items);
    }

    private static IEnumerable<BoundaryDescription> GetAdjacentBoundaries(World world, Vec3Int position)
    {
        foreach (var direction in Vec3Int.Directions.Select((offset, index) => ((Direction)index, offset)))
        {
            var neighborPosition = position + direction.offset;
            if (!world.Tiles.TryGetValue(neighborPosition, out var neighborTile))
            {
                continue;
            }

            var neighborFace = Opposite(direction.Item1);
            var side = neighborTile.Sides[(int)neighborFace];
            if (side?.Material == null && side?.Feature == null)
            {
                continue;
            }

            var description = $"{direction.Item1}: {side?.Material?.ToString() ?? "open"} {(side?.Feature.HasValue == true ? side.Feature.Value.ToString() : string.Empty)} {(side?.IsPassable == true || side?.IsOpen == true ? "passable" : "blocked")}".Trim();
            yield return new BoundaryDescription(direction.Item1.ToString(), description);
        }
    }

    private static Direction Opposite(Direction direction)
        => direction switch
        {
            Direction.Ceil => Direction.Floor,
            Direction.Floor => Direction.Ceil,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            _ => Direction.Floor
        };

    private static string FormatTags(Character character)
        => character.RpTags.Count == 0 ? "none" : string.Join("/", character.RpTags.Take(5));

    private static string FormatAffordances(Item item)
        => item.GoalAffordances.Count == 0
            ? string.Empty
            : $" Uses: {string.Join(", ", item.GoalAffordances.Select(affordance => affordance.NameOrKind()))}";
}

public sealed record TileInspection(Vec3Int Position, string Description, IReadOnlyList<Character> Characters, IReadOnlyList<Item> Items);
public sealed record BoundaryDescription(string DirectionText, string Description);
