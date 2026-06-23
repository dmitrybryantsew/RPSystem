using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpMapRenderProjectionService
{
    private readonly RpWorldInspectionService _inspectionService;

    public RpMapRenderProjectionService(RpWorldInspectionService inspectionService)
    {
        _inspectionService = inspectionService;
    }

    public RpMapRenderSnapshot CreateSnapshot(
        World world,
        RpSliceMode sliceMode,
        int sliceCoordinate,
        int maxOpenSpaceLookDepth = 5,
        Vec3Int? selectedTilePosition = null,
        Vec3Int? playerPosition = null,
        Vec3Int? selectedCharacterPosition = null)
    {
        if (!_inspectionService.TryGetVisibleBounds(world, sliceMode, sliceCoordinate, out var minA, out var maxA, out var minB, out var maxB))
        {
            return RpMapRenderSnapshot.Empty(sliceMode, sliceCoordinate);
        }

        var cells = new List<RpMapRenderCell>((maxA - minA + 1) * (maxB - minB + 1));
        for (var b = maxB; b >= minB; b--)
        {
            for (var a = minA; a <= maxA; a++)
            {
                var position = PositionFromSlice(sliceMode, sliceCoordinate, a, b);
                cells.Add(CreateCell(world, position, a, b, Math.Max(0, maxOpenSpaceLookDepth), selectedTilePosition, playerPosition, selectedCharacterPosition));
            }
        }

        return new RpMapRenderSnapshot(sliceMode, sliceCoordinate, minA, maxA, minB, maxB, cells);
    }

    private RpMapRenderCell CreateCell(
        World world,
        Vec3Int position,
        int a,
        int b,
        int maxOpenSpaceLookDepth,
        Vec3Int? selectedTilePosition,
        Vec3Int? playerPosition,
        Vec3Int? selectedCharacterPosition)
    {
        world.Tiles.TryGetValue(position, out var tile);

        var character = world.Characters.Values.FirstOrDefault(candidate => candidate.Position == position);
        var item = world.Items.Values.FirstOrDefault(candidate => candidate.Position == position);
        var features = tile?.MovementFeatures ?? [];
        var isOpenSpace = tile != null && character == null && item == null && IsOpenSpaceTile(tile);
        var glyph = isOpenSpace ? " " : _inspectionService.GetTileGlyph(world, position);
        var underlay = isOpenSpace ? FindUnderlay(world, position, maxOpenSpaceLookDepth) : RpOpenSpaceUnderlay.None;

        return new RpMapRenderCell(
            position,
            a,
            b,
            glyph,
            ClassifyCell(glyph, tile, character, item, isOpenSpace),
            tile?.Solidity == TileSolidity.Solid,
            character != null,
            item != null,
            features.Contains(RpTileMovementFeature.RampUp),
            features.Contains(RpTileMovementFeature.RampDown),
            features.Contains(RpTileMovementFeature.LadderUp) || features.Contains(RpTileMovementFeature.LadderDown),
            isOpenSpace,
            underlay.Glyph,
            underlay.Depth,
            underlay.IsClipped,
            selectedTilePosition.HasValue && selectedTilePosition.Value == position,
            playerPosition.HasValue && playerPosition.Value == position,
            selectedCharacterPosition.HasValue && selectedCharacterPosition.Value == position);
    }

    private RpOpenSpaceUnderlay FindUnderlay(World world, Vec3Int position, int maxDepth)
    {
        var minY = world.Tiles.Keys
            .Where(candidate => candidate.X == position.X && candidate.Z == position.Z)
            .Select(candidate => candidate.Y)
            .DefaultIfEmpty(position.Y)
            .Min();

        var clipped = false;
        for (var y = position.Y - 1; y >= minY; y--)
        {
            var depth = position.Y - y;
            if (depth > maxDepth)
            {
                clipped = HasUnderlayBelow(world, position, y);
                break;
            }

            var below = new Vec3Int(position.X, y, position.Z);
            var hasVisibleOccupant = world.Characters.Values.Any(candidate => candidate.Position == below) ||
                world.Items.Values.Any(candidate => candidate.Position == below);
            if (!world.Tiles.TryGetValue(below, out var belowTile) ||
                (IsOpenSpaceTile(belowTile) && !hasVisibleOccupant))
            {
                continue;
            }

            var glyph = _inspectionService.GetTileGlyph(world, below);
            return new RpOpenSpaceUnderlay(string.IsNullOrWhiteSpace(glyph) ? "." : glyph, depth, false);
        }

        return clipped
            ? new RpOpenSpaceUnderlay("?", maxDepth + 1, true)
            : RpOpenSpaceUnderlay.None;
    }

    private static bool HasUnderlayBelow(World world, Vec3Int position, int startY)
    {
        var minY = world.Tiles.Keys
            .Where(candidate => candidate.X == position.X && candidate.Z == position.Z)
            .Select(candidate => candidate.Y)
            .DefaultIfEmpty(startY)
            .Min();

        for (var y = startY; y >= minY; y--)
        {
            var below = new Vec3Int(position.X, y, position.Z);
            if (world.Characters.Values.Any(candidate => candidate.Position == below) ||
                world.Items.Values.Any(candidate => candidate.Position == below))
            {
                return true;
            }

            if (world.Tiles.TryGetValue(below, out var tile) && !IsOpenSpaceTile(tile))
            {
                return true;
            }
        }

        return false;
    }

    private static string ClassifyCell(string glyph, Tile? tile, Character? character, Item? item, bool isOpenSpace)
    {
        if (character != null)
        {
            return "character";
        }

        if (item != null)
        {
            return "item";
        }

        if (tile == null)
        {
            return "void";
        }

        if (isOpenSpace)
        {
            return "open-space";
        }

        if (tile.Solidity == TileSolidity.Solid)
        {
            return "solid";
        }

        return glyph switch
        {
            "+" => "window",
            "H" => "ladder",
            "▲" => "ramp-up",
            "▼" => "ramp-down",
            "~" => "water",
            "," => "moss",
            _ => "floor"
        };
    }

    private static bool IsOpenSpaceTile(Tile tile)
        => tile.Solidity == TileSolidity.Empty &&
            tile.BulkMaterial == MaterialType.Air &&
            tile.BulkState == MaterialState.Gas &&
            tile.FluidLevel == 0 &&
            tile.MovementFeatures.Count == 0 &&
            tile.Sides.All(side => side?.Material == null && side?.Feature == null);

    private static Vec3Int PositionFromSlice(RpSliceMode sliceMode, int sliceCoordinate, int a, int b)
        => sliceMode == RpSliceMode.Horizontal
            ? new Vec3Int(a, sliceCoordinate, b)
            : new Vec3Int(a, b, sliceCoordinate);
}

public sealed record RpMapRenderSnapshot(
    RpSliceMode SliceMode,
    int SliceCoordinate,
    int MinA,
    int MaxA,
    int MinB,
    int MaxB,
    IReadOnlyList<RpMapRenderCell> Cells)
{
    public bool HasCells => Cells.Count > 0;

    public static RpMapRenderSnapshot Empty(RpSliceMode sliceMode, int sliceCoordinate)
        => new(sliceMode, sliceCoordinate, 0, 0, 0, 0, []);

    public Vec3Int PositionFromSlice(int a, int b)
        => SliceMode == RpSliceMode.Horizontal
            ? new Vec3Int(a, SliceCoordinate, b)
            : new Vec3Int(a, b, SliceCoordinate);
}

public sealed record RpMapRenderCell(
    Vec3Int Position,
    int A,
    int B,
    string Glyph,
    string Kind,
    bool IsSolid,
    bool HasCharacter,
    bool HasItem,
    bool IsRampUp,
    bool IsRampDown,
    bool IsLadder,
    bool IsOpenSpace,
    string UnderlayGlyph,
    int UnderlayDepth,
    bool IsUnderlayClipped,
    bool IsSelected,
    bool IsPlayer,
    bool IsSelectedCharacter);

public readonly record struct RpOpenSpaceUnderlay(string Glyph, int Depth, bool IsClipped)
{
    public static RpOpenSpaceUnderlay None { get; } = new(string.Empty, 0, false);
}
