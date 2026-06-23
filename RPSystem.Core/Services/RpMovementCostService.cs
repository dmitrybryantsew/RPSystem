using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public static class RpMovementCostService
{
    public const int CostScale = 10;

    public static bool TryGetStepCost(World world, Vec3Int origin, Vec3Int destination, RpMovementMode mode, out int cost)
    {
        cost = CostScale;
        if (!world.Tiles.TryGetValue(origin, out var originTile) ||
            originTile.Solidity == TileSolidity.Solid ||
            !world.Tiles.TryGetValue(destination, out var destinationTile) ||
            destinationTile.Solidity == TileSolidity.Solid)
        {
            return false;
        }

        if (!RpSimulationService.CanEnter(world, origin, destination))
        {
            return false;
        }

        var delta = destination - origin;
        if (delta.Y != 0)
        {
            if (!CanMoveVertically(originTile, destinationTile, delta, mode))
            {
                return false;
            }
        }

        if (IsLiquid(destinationTile))
        {
            if (mode == RpMovementMode.Swim)
            {
                cost = CostScale;
                return true;
            }

            return false;
        }

        cost = mode == RpMovementMode.Fly ? 12 : CostScale;
        return mode is RpMovementMode.Walk or RpMovementMode.Fly or RpMovementMode.Climb;
    }

    private static bool CanMoveVertically(Tile originTile, Tile destinationTile, Vec3Int delta, RpMovementMode mode)
    {
        if (delta.X != 0 || delta.Z != 0 || Math.Abs(delta.Y) != 1)
        {
            return false;
        }

        if (mode == RpMovementMode.Fly)
        {
            return true;
        }

        var movingUp = delta.Y > 0;
        var originRamp = movingUp ? RpTileMovementFeature.RampUp : RpTileMovementFeature.RampDown;
        var destinationRamp = movingUp ? RpTileMovementFeature.RampDown : RpTileMovementFeature.RampUp;
        var originLadder = movingUp ? RpTileMovementFeature.LadderUp : RpTileMovementFeature.LadderDown;
        var destinationLadder = movingUp ? RpTileMovementFeature.LadderDown : RpTileMovementFeature.LadderUp;

        var hasRampPair = originTile.MovementFeatures.Contains(originRamp) &&
            destinationTile.MovementFeatures.Contains(destinationRamp);
        if (mode == RpMovementMode.Walk)
        {
            return hasRampPair;
        }

        var hasLadderPair = originTile.MovementFeatures.Contains(originLadder) &&
            destinationTile.MovementFeatures.Contains(destinationLadder);
        return mode == RpMovementMode.Climb && (hasLadderPair || hasRampPair);
    }

    public static bool IsTeleportDestination(Tile tile)
        => tile.Solidity != TileSolidity.Solid && !IsHazard(tile);

    public static int Heuristic(Vec3Int a, Vec3Int b)
        => (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z)) * CostScale;

    public static bool IsLiquid(Tile tile)
        => tile.FluidLevel > 0 ||
            tile.BulkState == MaterialState.Liquid ||
            tile.BulkMaterial is MaterialType.Water or MaterialType.Blood or MaterialType.Oil or MaterialType.SapLiquid or MaterialType.Lava;

    public static bool IsHazard(Tile tile)
        => tile.BulkMaterial is MaterialType.Fire or MaterialType.Lava ||
            tile.BulkState == MaterialState.Plasma;
}
