using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpJobService
{
    private const string BuildPayloadPrefix = "job-build:";

    public LlmActionResponse CreateLocalActionResponse(World world, Character character)
    {
        var job = GetActiveJob(character);
        if (job == null)
        {
            return RpSimulationService.WaitResponse("No active job.");
        }

        return job.Type switch
        {
            RpUnitJobType.Patrol => CreatePatrolAction(character, job),
            RpUnitJobType.Follow => CreateFollowAction(world, character, job),
            RpUnitJobType.Build => CreateBuildAction(world, character, job),
            _ => RpSimulationService.WaitResponse("Unsupported job type.")
        };
    }

    public bool TryResolveBuild(World world, Character character, CharacterAction action, out string description)
    {
        description = string.Empty;
        if (!TryGetBuildJob(character, action.Payload, out var job))
        {
            return false;
        }

        if (job.Status != RpUnitJobStatus.Active)
        {
            description = $"could not build because job {job.NameOrId()} is {job.Status}";
            return true;
        }

        if (!job.TargetPosition.HasValue)
        {
            job.Status = RpUnitJobStatus.Failed;
            description = $"could not build because job {job.NameOrId()} has no target tile";
            return true;
        }

        var target = job.TargetPosition.Value;
        if (DistanceManhattan(character.Position, target) > 1)
        {
            description = $"could not build at {target}: too far away";
            return true;
        }

        if (!world.Tiles.TryGetValue(target, out var tile))
        {
            tile = CreateEmptyTile(target);
            world.Tiles[target] = tile;
        }

        if (tile.OccupantIds.Any(world.Characters.ContainsKey))
        {
            description = $"could not build at {target}: occupied";
            return true;
        }

        ApplyBuild(world, tile, job);
        job.Status = RpUnitJobStatus.Completed;
        description = $"completed {DescribeBuild(job)} job {job.NameOrId()} at {target}";
        return true;
    }

    private static RpUnitJob? GetActiveJob(Character character)
        => character.Jobs
            .Where(job => job.Status == RpUnitJobStatus.Active)
            .OrderByDescending(job => job.Priority)
            .ThenBy(job => job.Name)
            .FirstOrDefault();

    private static LlmActionResponse CreatePatrolAction(Character character, RpUnitJob job)
    {
        if (job.Waypoints.Count == 0)
        {
            job.Status = RpUnitJobStatus.Failed;
            return RpSimulationService.WaitResponse($"Patrol job {job.NameOrId()} has no waypoints.");
        }

        job.CurrentWaypointIndex = Math.Clamp(job.CurrentWaypointIndex, 0, job.Waypoints.Count - 1);
        var waypoint = job.Waypoints[job.CurrentWaypointIndex];
        if (character.Position == waypoint)
        {
            if (job.CurrentWaypointIndex >= job.Waypoints.Count - 1)
            {
                if (!job.Repeat)
                {
                    job.Status = RpUnitJobStatus.Completed;
                    return RpSimulationService.WaitResponse($"Patrol job {job.NameOrId()} completed.");
                }

                job.CurrentWaypointIndex = 0;
            }
            else
            {
                job.CurrentWaypointIndex++;
            }

            waypoint = job.Waypoints[job.CurrentWaypointIndex];
        }

        return MoveToward($"Patrol job {job.NameOrId()} moving toward waypoint {job.CurrentWaypointIndex + 1}.", waypoint);
    }

    private static LlmActionResponse CreateFollowAction(World world, Character character, RpUnitJob job)
    {
        if (!job.TargetCharacterId.HasValue ||
            !world.Characters.TryGetValue(job.TargetCharacterId.Value, out var target))
        {
            job.Status = RpUnitJobStatus.Failed;
            return RpSimulationService.WaitResponse($"Follow job {job.NameOrId()} target is missing.");
        }

        if (DistanceManhattan(character.Position, target.Position) <= Math.Max(0, job.FollowDistance))
        {
            return RpSimulationService.WaitResponse($"Follow job {job.NameOrId()} is in range of {target.Name}.");
        }

        return MoveToward($"Follow job {job.NameOrId()} moving toward {target.Name}.", target.Position);
    }

    private static LlmActionResponse CreateBuildAction(World world, Character character, RpUnitJob job)
    {
        if (!job.TargetPosition.HasValue)
        {
            job.Status = RpUnitJobStatus.Failed;
            return RpSimulationService.WaitResponse($"Build job {job.NameOrId()} has no target tile.");
        }

        var target = job.TargetPosition.Value;
        if (world.Tiles.TryGetValue(target, out var tile) && BuildMatches(world, tile, job))
        {
            job.Status = RpUnitJobStatus.Completed;
            return RpSimulationService.WaitResponse($"Build job {job.NameOrId()} already complete.");
        }

        if (DistanceManhattan(character.Position, target) <= 1)
        {
            return new LlmActionResponse
            {
                Note = $"Build job {job.NameOrId()} constructing at {target}.",
                Actions =
                [
                    new CharacterAction
                    {
                        Type = ActionType.Build,
                        TargetPos = target,
                        Payload = BuildPayloadPrefix + job.Id.ToString("N"),
                        TickCost = 1,
                        Note = "Unit job build step."
                    }
                ]
            };
        }

        var standTarget = GetBuildStandTarget(world, character, target);
        if (!standTarget.HasValue)
        {
            job.Status = RpUnitJobStatus.Failed;
            return RpSimulationService.WaitResponse($"Build job {job.NameOrId()} has no reachable adjacent work tile.");
        }

        return MoveToward($"Build job {job.NameOrId()} moving to work tile.", standTarget.Value);
    }

    private static Vec3Int? GetBuildStandTarget(World world, Character character, Vec3Int buildTarget)
    {
        Vec3Int? best = null;
        var bestDistance = int.MaxValue;
        foreach (var position in buildTarget.Neighbors())
        {
            if (!world.Tiles.TryGetValue(position, out var tile) ||
                tile.OccupantIds.Any(id => id != character.Id && world.Characters.ContainsKey(id)))
            {
                continue;
            }

            var distance = DistanceManhattan(character.Position, position);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = position;
            bestDistance = distance;
        }

        return best;
    }

    private static LlmActionResponse MoveToward(string note, Vec3Int target)
        => new()
        {
            Note = note,
            Actions =
            [
                new CharacterAction
                {
                    Type = ActionType.Move,
                    TargetPos = target,
                    TickCost = 1,
                    Note = "Unit job movement."
                }
            ]
        };

    private static bool TryGetBuildJob(Character character, string? payload, out RpUnitJob job)
    {
        job = new RpUnitJob();
        if (string.IsNullOrWhiteSpace(payload) ||
            !payload.StartsWith(BuildPayloadPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParse(payload[BuildPayloadPrefix.Length..], out var jobId))
        {
            return false;
        }

        var match = character.Jobs.FirstOrDefault(candidate => candidate.Id == jobId);
        if (match == null)
        {
            return false;
        }

        job = match;
        return true;
    }

    private static void ApplyBuild(World world, Tile tile, RpUnitJob job)
    {
        switch (job.BuildKind)
        {
            case RpBuildKind.FullWall:
                ApplyFullWall(tile, job);
                break;
            case RpBuildKind.Floor:
                ApplyFloor(tile, job);
                break;
            case RpBuildKind.SideWall:
                ApplyBoundary(world, tile, job, feature: null, passable: false);
                break;
            case RpBuildKind.Door:
                ApplyBoundary(world, tile, job, SideFeature.Door, passable: false);
                break;
            case RpBuildKind.Window:
                ApplyBoundary(world, tile, job, SideFeature.Window, passable: false);
                break;
            case RpBuildKind.RampUp:
                AddMovementFeature(tile, RpTileMovementFeature.RampUp);
                break;
            case RpBuildKind.RampDown:
                AddMovementFeature(tile, RpTileMovementFeature.RampDown);
                break;
            case RpBuildKind.LadderUp:
                AddMovementFeature(tile, RpTileMovementFeature.LadderUp);
                break;
            case RpBuildKind.LadderDown:
                AddMovementFeature(tile, RpTileMovementFeature.LadderDown);
                break;
            case RpBuildKind.Furniture:
                ApplyFurniture(world, tile, job);
                break;
        }

        world.TerrainVersion++;
    }

    private static void ApplyFullWall(Tile tile, RpUnitJob job)
    {
        tile.Solidity = job.BuildSolidity;
        tile.BulkMaterial = job.BuildMaterial;
        tile.BulkState = job.BuildState;
        tile.BulkHealth = job.BuildSolidity == TileSolidity.Solid ? Math.Max(tile.BulkHealth, 100) : tile.BulkHealth;
        tile.FluidLevel = 0;
    }

    private static void ApplyFloor(Tile tile, RpUnitJob job)
    {
        tile.Solidity = TileSolidity.Empty;
        tile.BulkMaterial = MaterialType.Air;
        tile.BulkState = MaterialState.Gas;
        tile.FluidLevel = 0;
        tile.Sides[(int)Direction.Floor] = new Side
        {
            Direction = Direction.Floor,
            Material = job.BuildMaterial,
            Health = 100,
            IsPassable = true
        };
    }

    private static void ApplyBoundary(World world, Tile tile, RpUnitJob job, SideFeature? feature, bool passable)
    {
        var direction = job.BuildDirection ?? Direction.North;
        tile.Sides[(int)direction] = new Side
        {
            Direction = direction,
            Material = job.BuildMaterial,
            Health = 100,
            IsPassable = passable,
            Feature = feature
        };

        var neighborPosition = tile.Position + RpSimulationService.OffsetFor(direction);
        if (!world.Tiles.TryGetValue(neighborPosition, out var neighbor))
        {
            return;
        }

        var opposite = Opposite(direction);
        neighbor.Sides[(int)opposite] = new Side
        {
            Direction = opposite,
            Material = job.BuildMaterial,
            Health = 100,
            IsPassable = passable,
            Feature = feature
        };
    }

    private static void AddMovementFeature(Tile tile, RpTileMovementFeature feature)
    {
        if (!tile.MovementFeatures.Contains(feature))
        {
            tile.MovementFeatures.Add(feature);
        }
    }

    private static void ApplyFurniture(World world, Tile tile, RpUnitJob job)
    {
        if (world.Items.Values.Any(item => item.Position == tile.Position && item.Tags.Contains(BuildFurnitureTag, StringComparer.OrdinalIgnoreCase)))
        {
            return;
        }

        var item = new Item
        {
            Name = string.IsNullOrWhiteSpace(job.BuildItemName) ? "Furniture" : job.BuildItemName.Trim(),
            Description = string.IsNullOrWhiteSpace(job.BuildItemDescription) ? "Built furniture." : job.BuildItemDescription.Trim(),
            Material = job.BuildMaterial,
            Weight = 10,
            Position = tile.Position,
            Tags = [BuildFurnitureTag, .. job.BuildItemTags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Select(tag => tag.Trim())]
        };
        world.Items[item.Id] = item;
        if (!tile.OccupantIds.Contains(item.Id))
        {
            tile.OccupantIds.Add(item.Id);
        }
    }

    private static bool BuildMatches(World world, Tile tile, RpUnitJob job)
        => job.BuildKind switch
        {
            RpBuildKind.FullWall => tile.Solidity == job.BuildSolidity &&
                tile.BulkMaterial == job.BuildMaterial &&
                tile.BulkState == job.BuildState,
            RpBuildKind.Floor => tile.Solidity != TileSolidity.Solid &&
                tile.Sides[(int)Direction.Floor]?.Material == job.BuildMaterial,
            RpBuildKind.SideWall => BoundaryMatches(tile, job, feature: null),
            RpBuildKind.Door => BoundaryMatches(tile, job, SideFeature.Door),
            RpBuildKind.Window => BoundaryMatches(tile, job, SideFeature.Window),
            RpBuildKind.RampUp => tile.MovementFeatures.Contains(RpTileMovementFeature.RampUp),
            RpBuildKind.RampDown => tile.MovementFeatures.Contains(RpTileMovementFeature.RampDown),
            RpBuildKind.LadderUp => tile.MovementFeatures.Contains(RpTileMovementFeature.LadderUp),
            RpBuildKind.LadderDown => tile.MovementFeatures.Contains(RpTileMovementFeature.LadderDown),
            RpBuildKind.Furniture => world.Items.Values.Any(item =>
                item.Position == tile.Position &&
                item.Tags.Contains(BuildFurnitureTag, StringComparer.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(job.BuildItemName) ||
                    string.Equals(item.Name, job.BuildItemName.Trim(), StringComparison.OrdinalIgnoreCase))),
            _ => false
        };

    private static bool BoundaryMatches(Tile tile, RpUnitJob job, SideFeature? feature)
    {
        var direction = job.BuildDirection ?? Direction.North;
        var side = tile.Sides[(int)direction];
        return side?.Material == job.BuildMaterial &&
            side.Feature == feature &&
            !side.IsPassable;
    }

    private static Tile CreateEmptyTile(Vec3Int position)
        => new()
        {
            Position = position,
            Solidity = TileSolidity.Empty,
            BulkMaterial = MaterialType.Air,
            BulkState = MaterialState.Gas
        };

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

    private static string DescribeBuild(RpUnitJob job)
        => job.BuildKind switch
        {
            RpBuildKind.SideWall => $"{job.BuildMaterial} side wall",
            RpBuildKind.Door => $"{job.BuildMaterial} door",
            RpBuildKind.Window => $"{job.BuildMaterial} window",
            RpBuildKind.Furniture => string.IsNullOrWhiteSpace(job.BuildItemName) ? $"{job.BuildMaterial} furniture" : job.BuildItemName,
            RpBuildKind.RampUp => "ramp up",
            RpBuildKind.RampDown => "ramp down",
            RpBuildKind.LadderUp => "ladder up",
            RpBuildKind.LadderDown => "ladder down",
            RpBuildKind.Floor => $"{job.BuildMaterial} floor",
            _ => $"{job.BuildMaterial} full wall"
        };

    private static int DistanceManhattan(Vec3Int a, Vec3Int b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private const string BuildFurnitureTag = "built:furniture";
}

internal static class RpUnitJobFormattingExtensions
{
    public static string NameOrId(this RpUnitJob job)
        => string.IsNullOrWhiteSpace(job.Name) ? job.Id.ToString("N")[..8] : job.Name;
}
