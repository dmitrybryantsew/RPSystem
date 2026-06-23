using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpPathfindingService
{
    public RpPathResult FindPath(World world, Character character, Vec3Int target, RpPathfindingOptions? options = null)
    {
        options ??= new RpPathfindingOptions();
        var start = character.Position;
        if (start == target)
        {
            return RpPathResult.Found(RpMovementMode.Walk, [], 0);
        }

        var modes = GetUsableMovementModes(character).ToList();
        var maxVisitedNodes = Math.Max(1, options.MaxVisitedNodes ?? character.Movement.MaxPathSearchNodes);

        RpPathResult? bestPartial = null;
        RpPathResult? lastFailure = null;
        foreach (var mode in modes.Where(mode => mode != RpMovementMode.Teleport))
        {
            var result = FindPathForMode(world, start, target, mode, maxVisitedNodes, options.AllowPartial);
            if (result.Success)
            {
                return result;
            }

            if (result.IsPartial && bestPartial == null)
            {
                bestPartial = result;
            }

            if (!result.Success && !result.IsPartial)
            {
                lastFailure = result;
            }
        }

        if (modes.Contains(RpMovementMode.Teleport))
        {
            var teleportResult = FindTeleportStep(world, character, target);
            if (teleportResult.Success || teleportResult.IsPartial)
            {
                return teleportResult;
            }
        }

        if (bestPartial != null)
        {
            return bestPartial;
        }

        return lastFailure ?? RpPathResult.Failed(maxVisitedNodes, "No route found.");
    }

    public Vec3Int? FindNextStep(World world, Character character, Vec3Int target, RpPathfindingOptions? options = null)
        => FindPath(world, character, target, options).NextStep;

    public static IReadOnlyList<RpMovementMode> GetUsableMovementModes(Character character)
    {
        var modes = new HashSet<RpMovementMode>(character.Movement.Modes);
        if (modes.Count == 0)
        {
            modes.Add(RpMovementMode.Walk);
        }

        if (HasWorkingWings(character))
        {
            modes.Add(RpMovementMode.Fly);
        }

        if (character.RpTags.Any(tag => tag.Equals("aquatic", StringComparison.OrdinalIgnoreCase) ||
            tag.Equals("swimmer", StringComparison.OrdinalIgnoreCase)))
        {
            modes.Add(RpMovementMode.Swim);
        }

        if (character.Movement.TeleportRange > 0 &&
            character.RpTags.Any(tag => tag.Equals("teleport", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("teleporter", StringComparison.OrdinalIgnoreCase)))
        {
            modes.Add(RpMovementMode.Teleport);
        }

        return modes.ToList();
    }

    private static bool HasWorkingWings(Character character)
        => character.RpTags.Any(tag => tag.Equals("winged", StringComparison.OrdinalIgnoreCase)) ||
            character.Body.Any(part => part.Role == BodyPartRole.Wing && part.HpCurrent > 0);

    private static RpPathResult FindPathForMode(
        World world,
        Vec3Int start,
        Vec3Int target,
        RpMovementMode mode,
        int maxVisitedNodes,
        bool allowPartial)
    {
        var frontier = new PriorityQueue<Vec3Int, int>();
        var cameFrom = new Dictionary<Vec3Int, Vec3Int>();
        var costSoFar = new Dictionary<Vec3Int, int> { [start] = 0 };
        var visited = 0;
        var best = start;
        var bestHeuristic = RpMovementCostService.Heuristic(start, target);

        frontier.Enqueue(start, bestHeuristic);
        while (frontier.Count > 0 && visited < maxVisitedNodes)
        {
            var current = frontier.Dequeue();
            visited++;

            if (current == target)
            {
                return RpPathResult.Found(mode, ReconstructPath(cameFrom, start, target), visited);
            }

            var currentHeuristic = RpMovementCostService.Heuristic(current, target);
            if (currentHeuristic < bestHeuristic)
            {
                best = current;
                bestHeuristic = currentHeuristic;
            }

            foreach (var next in current.Neighbors())
            {
                if (!RpMovementCostService.TryGetStepCost(world, current, next, mode, out var stepCost))
                {
                    continue;
                }

                var newCost = costSoFar[current] + stepCost;
                if (costSoFar.TryGetValue(next, out var existingCost) && newCost >= existingCost)
                {
                    continue;
                }

                costSoFar[next] = newCost;
                cameFrom[next] = current;
                frontier.Enqueue(next, newCost + RpMovementCostService.Heuristic(next, target));
            }
        }

        if (allowPartial && best != start)
        {
            return RpPathResult.Partial(mode, ReconstructPath(cameFrom, start, best), visited, "Search limit reached before target.");
        }

        return RpPathResult.Failed(visited, frontier.Count == 0 ? "No route found." : "Search limit reached before target.");
    }

    private static RpPathResult FindTeleportStep(World world, Character character, Vec3Int target)
    {
        var range = Math.Max(0, character.Movement.TeleportRange);
        if (range <= 0)
        {
            return RpPathResult.Failed(0, "Teleport range is not configured.");
        }

        var origin = character.Position;
        var best = world.Tiles.Values
            .Where(RpMovementCostService.IsTeleportDestination)
            .Where(tile => RpMovementCostService.Heuristic(origin, tile.Position) <= range * RpMovementCostService.CostScale)
            .OrderBy(tile => RpMovementCostService.Heuristic(tile.Position, target))
            .ThenBy(tile => RpMovementCostService.Heuristic(origin, tile.Position))
            .Select(tile => tile.Position)
            .FirstOrDefault(origin);

        if (best == origin)
        {
            return RpPathResult.Failed(0, "No useful teleport destination in range.");
        }

        var success = best == target;
        return new RpPathResult
        {
            Success = success,
            IsPartial = !success,
            MovementMode = RpMovementMode.Teleport,
            Steps = [best],
            NodesSearched = 1,
            FailureReason = success ? string.Empty : "Teleport can only move closer with current stub rules."
        };
    }

    private static List<Vec3Int> ReconstructPath(Dictionary<Vec3Int, Vec3Int> cameFrom, Vec3Int start, Vec3Int end)
    {
        var path = new List<Vec3Int>();
        var current = end;
        while (current != start)
        {
            path.Add(current);
            if (!cameFrom.TryGetValue(current, out current))
            {
                return [];
            }
        }

        path.Reverse();
        return path;
    }
}

public sealed class RpPathfindingOptions
{
    public int? MaxVisitedNodes { get; set; }
    public bool AllowPartial { get; set; } = true;
}

public sealed class RpPathResult
{
    public bool Success { get; set; }
    public bool IsPartial { get; set; }
    public RpMovementMode MovementMode { get; set; } = RpMovementMode.Walk;
    public List<Vec3Int> Steps { get; set; } = [];
    public int NodesSearched { get; set; }
    public string FailureReason { get; set; } = string.Empty;

    public Vec3Int? NextStep => Steps.Count == 0 ? null : Steps[0];

    public static RpPathResult Found(RpMovementMode mode, List<Vec3Int> steps, int nodesSearched)
        => new() { Success = true, MovementMode = mode, Steps = steps, NodesSearched = nodesSearched };

    public static RpPathResult Partial(RpMovementMode mode, List<Vec3Int> steps, int nodesSearched, string reason)
        => new() { IsPartial = true, MovementMode = mode, Steps = steps, NodesSearched = nodesSearched, FailureReason = reason };

    public static RpPathResult Failed(int nodesSearched, string reason)
        => new() { NodesSearched = nodesSearched, FailureReason = reason };
}
