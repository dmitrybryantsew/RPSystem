using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpFlowFieldService
{
    private readonly Dictionary<RpFlowFieldKey, RpFlowField> _cache = [];

    public RpFlowField GetOrCreateFlowField(World world, RpMovementMode movementMode, Vec3Int target, int? maxVisitedNodes = null)
    {
        var key = RpFlowFieldKey.Create(world, movementMode, target);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var created = BuildFlowField(world, movementMode, target, maxVisitedNodes);
        _cache[key] = created;
        return created;
    }

    public Vec3Int? GetNextStep(World world, Character character, Vec3Int target, int? maxVisitedNodes = null)
    {
        foreach (var mode in RpPathfindingService.GetUsableMovementModes(character).Where(mode => mode != RpMovementMode.Teleport))
        {
            var field = GetOrCreateFlowField(world, mode, target, maxVisitedNodes ?? character.Movement.MaxPathSearchNodes);
            if (field.TryGetNextStep(character.Position, out var nextStep))
            {
                return nextStep;
            }
        }

        return null;
    }

    public void ClearCache() => _cache.Clear();

    public int CachedFieldCount => _cache.Count;

    private static RpFlowField BuildFlowField(World world, RpMovementMode movementMode, Vec3Int target, int? maxVisitedNodes)
    {
        var field = new RpFlowField
        {
            MovementMode = movementMode,
            Target = target
        };

        if (!world.Tiles.TryGetValue(target, out var targetTile) ||
            targetTile.Solidity == TileSolidity.Solid)
        {
            field.IsComplete = true;
            field.FailureReason = "Target is not traversable.";
            return field;
        }

        var frontier = new PriorityQueue<Vec3Int, int>();
        var maxNodes = Math.Max(1, maxVisitedNodes ?? int.MaxValue);
        var visited = 0;

        field.Cells[target] = new RpFlowFieldCell
        {
            Position = target,
            CostToTarget = 0,
            NextStep = null
        };
        frontier.Enqueue(target, 0);

        while (frontier.Count > 0 && visited < maxNodes)
        {
            var current = frontier.Dequeue();
            visited++;

            foreach (var candidate in current.Neighbors())
            {
                if (!world.Tiles.ContainsKey(candidate))
                {
                    continue;
                }

                if (!RpMovementCostService.TryGetStepCost(world, candidate, current, movementMode, out var stepCost))
                {
                    continue;
                }

                var newCost = field.Cells[current].CostToTarget + stepCost;
                if (field.Cells.TryGetValue(candidate, out var existing) && newCost >= existing.CostToTarget)
                {
                    continue;
                }

                field.Cells[candidate] = new RpFlowFieldCell
                {
                    Position = candidate,
                    CostToTarget = newCost,
                    NextStep = current
                };
                frontier.Enqueue(candidate, newCost);
            }
        }

        field.NodesVisited = visited;
        field.IsComplete = frontier.Count == 0;
        field.FailureReason = field.IsComplete ? string.Empty : "Search limit reached before full field bake.";
        return field;
    }
}

public sealed class RpFlowField
{
    public RpMovementMode MovementMode { get; set; }
    public Vec3Int Target { get; set; }
    public Dictionary<Vec3Int, RpFlowFieldCell> Cells { get; set; } = [];
    public int NodesVisited { get; set; }
    public bool IsComplete { get; set; }
    public string FailureReason { get; set; } = string.Empty;

    public bool TryGetNextStep(Vec3Int position, out Vec3Int nextStep)
    {
        nextStep = default;
        if (!Cells.TryGetValue(position, out var cell) || cell.NextStep == null)
        {
            return false;
        }

        nextStep = cell.NextStep.Value;
        return true;
    }
}

public sealed class RpFlowFieldCell
{
    public Vec3Int Position { get; set; }
    public int CostToTarget { get; set; }
    public Vec3Int? NextStep { get; set; }
}

public readonly record struct RpFlowFieldKey(int WorldFingerprint, RpMovementMode MovementMode, Vec3Int Target)
{
    public static RpFlowFieldKey Create(World world, RpMovementMode movementMode, Vec3Int target)
        => new(ComputeWorldFingerprint(world), movementMode, target);

    private static int ComputeWorldFingerprint(World world)
    {
        var hash = new HashCode();
        hash.Add(world.Tiles.Count);
        foreach (var tile in world.Tiles.Values.OrderBy(tile => tile.Position.X).ThenBy(tile => tile.Position.Y).ThenBy(tile => tile.Position.Z))
        {
            hash.Add(tile.Position);
            hash.Add(tile.Solidity);
            hash.Add(tile.BulkState);
            hash.Add(tile.BulkMaterial);
            hash.Add(tile.FluidLevel);
            foreach (var feature in tile.MovementFeatures.OrderBy(feature => feature))
            {
                hash.Add(feature);
            }

            for (var i = 0; i < tile.Sides.Length; i++)
            {
                var side = tile.Sides[i];
                hash.Add(i);
                hash.Add(side?.IsPassable);
                hash.Add(side?.IsOpen);
                hash.Add(side?.Material);
            }
        }

        return hash.ToHashCode();
    }
}
