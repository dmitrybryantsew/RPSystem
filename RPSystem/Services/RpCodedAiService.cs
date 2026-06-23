using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpCodedAiService
{
    private readonly RpJobService _jobService = new();

    public IReadOnlyList<NarrativeEvent> PlanWorld(World world)
    {
        var events = new List<NarrativeEvent>();
        UpdateNeeds(world);
        AssignHiveJobs(world, events);
        return events;
    }

    public LlmActionResponse CreateLocalActionResponse(World world, Character character)
    {
        UpdateNeeds(character, world);

        if (character.Jobs.Any(job => job.Status == RpUnitJobStatus.Active))
        {
            return _jobService.CreateLocalActionResponse(world, character);
        }

        if (TryCreateNeedAction(world, character, out var needResponse))
        {
            return needResponse;
        }

        if (TryCreateObjectAction(world, character, out var objectResponse))
        {
            return objectResponse;
        }

        if (TryCreateGoalAction(world, character, out var goalResponse))
        {
            return goalResponse;
        }

        if (TryCreateDesireAction(world, character, out var desireResponse))
        {
            return desireResponse;
        }

        return RpSimulationService.WaitResponse("No coded AI goal is currently actionable.");
    }

    private static void UpdateNeeds(World world)
    {
        foreach (var character in world.Characters.Values)
        {
            UpdateNeeds(character, world);
        }
    }

    private static void UpdateNeeds(Character character, World world)
    {
        character.Needs =
        [
            CreateNeed(RpNeedType.Health, Ratio(character.Vitals.HealthCurrent, character.Vitals.HealthMax), "Health below maximum."),
            CreateNeed(RpNeedType.Stamina, Ratio(character.Vitals.StaminaCurrent, character.Vitals.StaminaMax), "Stamina below maximum."),
            CreateNeed(RpNeedType.Mana, Ratio(character.Vitals.ManaCurrent, character.Vitals.ManaMax), "Mana below maximum."),
            CreateNeed(RpNeedType.Focus, Ratio(character.Vitals.FocusCurrent, character.Vitals.FocusMax), "Focus below maximum."),
            CreateNeed(RpNeedType.Safety, ComputeSafety(world, character), "Visible hostile or dangerous tile nearby."),
            CreateNeed(RpNeedType.Purpose, HasActivePurpose(character) ? 1 : 0.35f, "No active job or actionable goal.")
        ];
    }

    private static RpNeedState CreateNeed(RpNeedType type, float value, string reason)
    {
        value = Math.Clamp(value, 0, 1);
        return new RpNeedState
        {
            Type = type,
            Value = value,
            Urgency = 1 - value,
            Reason = reason
        };
    }

    private static float ComputeSafety(World world, Character character)
    {
        var visibleHostile = character.PerceivedState.VisibleCharacterIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id])
            .Any(other => IsHostile(character, other));

        if (visibleHostile)
        {
            return 0.25f;
        }

        return world.Tiles.TryGetValue(character.Position, out var tile) && RpMovementCostService.IsHazard(tile)
            ? 0.1f
            : 1;
    }

    private static bool HasActivePurpose(Character character)
        => character.Jobs.Any(job => job.Status == RpUnitJobStatus.Active) ||
            !string.IsNullOrWhiteSpace(character.CurrentGoal.Description) ||
            character.Desires.Count > 0;

    private static bool TryCreateNeedAction(World world, Character character, out LlmActionResponse response)
    {
        response = new LlmActionResponse();
        var mostUrgent = character.Needs
            .OrderByDescending(need => need.Urgency)
            .FirstOrDefault();
        if (mostUrgent == null || mostUrgent.Urgency < 0.45f)
        {
            return false;
        }

        if (mostUrgent.Type is RpNeedType.Stamina or RpNeedType.Mana or RpNeedType.Focus or RpNeedType.Health)
        {
            response = RpSimulationService.WaitResponse($"Resting because {mostUrgent.Type.ToString().ToLowerInvariant()} need is urgent.");
            return true;
        }

        if (mostUrgent.Type == RpNeedType.Safety &&
            !HasDefensiveIntent(character) &&
            TryFindNearestHostile(world, character, out var hostile) &&
            TryFindStepAwayFrom(world, character, hostile.Position, out var retreat))
        {
            response = MoveToward($"Retreating from {hostile.Name}.", retreat);
            return true;
        }

        return false;
    }

    private static bool TryCreateGoalAction(World world, Character character, out LlmActionResponse response)
    {
        response = new LlmActionResponse();
        var goal = $"{character.CurrentGoal.Description} {character.LifeGoal.Description}";
        if (HasDefensiveIntent(character) &&
            TryFindNearestHostile(world, character, out var hostile))
        {
            if (DistanceManhattan(character.Position, hostile.Position) <= 1)
            {
                response = new LlmActionResponse
                {
                    Note = $"Attacking hostile {hostile.Name}.",
                    Actions =
                    [
                        new CharacterAction
                        {
                            Type = ActionType.Attack,
                            TargetId = hostile.Id,
                            TickCost = 1,
                            Note = "Coded AI defense."
                        }
                    ]
                };
                return true;
            }

            response = MoveToward($"Moving toward hostile {hostile.Name}.", hostile.Position);
            return true;
        }

        if (ContainsAny(goal, "follow", "escort", "queen") &&
            TryFindFactionQueen(world, character.FactionId, out var queen) &&
            queen.Id != character.Id)
        {
            character.Jobs.Add(new RpUnitJob
            {
                Type = RpUnitJobType.Follow,
                Name = $"Follow {queen.Name}",
                TargetCharacterId = queen.Id,
                FollowDistance = 2,
                Priority = 80,
                Note = "Assigned by coded unit goal."
            });
            response = new RpJobService().CreateLocalActionResponse(world, character);
            return true;
        }

        return false;
    }

    private static bool TryCreateObjectAction(World world, Character character, out LlmActionResponse response)
    {
        response = new LlmActionResponse();
        var goalText = $"{character.CurrentGoal.Description} {character.LifeGoal.Description}";
        var desireText = string.Join(' ', character.Desires);
        var urgentNeedTypes = character.Needs
            .Where(need => need.Urgency >= 0.35f)
            .Select(need => need.Type)
            .ToHashSet();

        var match = world.Items.Values
            .Where(item => item.Position.HasValue)
            .SelectMany(item => item.GoalAffordances.Select(affordance => new GoalObjectMatch(item, affordance, ScoreGoalObject(character, item, affordance, goalText, desireText, urgentNeedTypes))))
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => DistanceManhattan(character.Position, match.Item.Position!.Value))
            .FirstOrDefault();

        if (match == null)
        {
            return false;
        }

        var itemPosition = match.Item.Position!.Value;
        var range = Math.Max(0, match.Affordance.InteractionRange);
        if (DistanceManhattan(character.Position, itemPosition) <= range)
        {
            response = new LlmActionResponse
            {
                Note = $"Using {match.Item.Name} for {match.Affordance.NameOrKind()}.",
                Actions =
                [
                    new CharacterAction
                    {
                        Type = ActionType.Interact,
                        TargetId = match.Item.Id,
                        TargetPos = itemPosition,
                        Payload = match.Affordance.Name,
                        TickCost = 1,
                        Note = "Coded AI goal object."
                    }
                ]
            };
            return true;
        }

        response = MoveToward($"Moving toward {match.Item.Name} for {match.Affordance.NameOrKind()}.", itemPosition);
        return true;
    }

    private static int ScoreGoalObject(
        Character character,
        Item item,
        RpGoalObjectAffordance affordance,
        string goalText,
        string desireText,
        HashSet<RpNeedType> urgentNeedTypes)
    {
        var score = 0;
        if (affordance.NeedTypes.Any(urgentNeedTypes.Contains))
        {
            score += 100;
        }

        score += CountKeywordMatches(goalText, affordance.GoalKeywords) * 40;
        score += CountKeywordMatches(desireText, affordance.DesireKeywords) * 30;
        if (item.Tags.Any(tag => ContainsAny(goalText, tag) || ContainsAny(desireText, tag)))
        {
            score += 15;
        }

        if (score <= 0)
        {
            return 0;
        }

        return score + affordance.Priority - DistanceManhattan(character.Position, item.Position!.Value);
    }

    private static int CountKeywordMatches(string text, IEnumerable<string> keywords)
        => keywords.Count(keyword => !string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static bool TryCreateDesireAction(World world, Character character, out LlmActionResponse response)
    {
        response = new LlmActionResponse();
        var desireText = string.Join(' ', character.Desires);
        if (ContainsAny(desireText, "patrol", "watch", "guard"))
        {
            AssignLocalPatrol(world, character);
            response = new RpJobService().CreateLocalActionResponse(world, character);
            return true;
        }

        if (ContainsAny(desireText, "build", "nest", "hive", "settlement"))
        {
            AssignSimpleBuildJob(world, character);
            response = new RpJobService().CreateLocalActionResponse(world, character);
            return true;
        }

        return false;
    }

    private static void AssignHiveJobs(World world, List<NarrativeEvent> events)
    {
        foreach (var factionGroup in world.Characters.Values
            .Where(character => character.Vitals.LifeState == RpLifeState.Conscious)
            .Where(character => IsHiveCharacter(world, character))
            .GroupBy(character => character.FactionId ?? string.Empty)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key)))
        {
            var members = factionGroup.ToList();
            var queen = members.FirstOrDefault(IsQueenLike) ?? members.FirstOrDefault();
            if (queen == null)
            {
                continue;
            }

            foreach (var member in members.Where(member => member.Id != queen.Id && !HasActiveJob(member)))
            {
                var assigned = IsWorkerLike(member)
                    ? AssignHiveBuildJob(world, member, queen)
                    : AssignHiveFollowOrPatrolJob(world, member, queen);
                if (assigned)
                {
                    events.Add(new NarrativeEvent
                    {
                        Tick = world.Clock.TickCount,
                        ActorName = "Hive AI",
                        Description = $"Assigned {member.Jobs.Last().Type} job '{member.Jobs.Last().Name}' to {member.Name}."
                    });
                }
            }
        }
    }

    private static bool AssignHiveBuildJob(World world, Character member, Character queen)
    {
        if (!TryFindNearbyBuildTile(world, queen.Position, out var target))
        {
            return false;
        }

        member.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = "Expand hive furniture",
            Priority = 70,
            TargetPosition = target,
            BuildKind = RpBuildKind.Furniture,
            BuildMaterial = MaterialType.SapSolid,
            BuildState = MaterialState.Solid,
            BuildItemName = "resin work node",
            BuildItemDescription = "A simple hive work node formed from resin.",
            BuildItemTags = ["hive", "resin", "workstation"],
            Note = "Assigned by hive coded AI."
        });
        return true;
    }

    private static bool AssignHiveFollowOrPatrolJob(World world, Character member, Character queen)
    {
        if (DistanceManhattan(member.Position, queen.Position) > 3)
        {
            member.Jobs.Add(new RpUnitJob
            {
                Type = RpUnitJobType.Follow,
                Name = $"Guard {queen.Name}",
                Priority = 60,
                TargetCharacterId = queen.Id,
                FollowDistance = 2,
                Note = "Assigned by hive coded AI."
            });
            return true;
        }

        AssignLocalPatrol(world, member, queen.Position);
        return true;
    }

    private static void AssignLocalPatrol(World world, Character character, Vec3Int? anchor = null)
    {
        var center = anchor ?? character.Position;
        var waypoints = new[]
            {
                center + Vec3Int.East,
                center + Vec3Int.North,
                center + Vec3Int.West,
                center + Vec3Int.South
            }
            .Where(world.Tiles.ContainsKey)
            .Where(pos => world.Tiles[pos].Solidity != TileSolidity.Solid)
            .Take(4)
            .ToList();

        if (waypoints.Count == 0)
        {
            return;
        }

        character.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Patrol,
            Name = "Local patrol",
            Priority = 40,
            Waypoints = waypoints,
            Repeat = true,
            Note = "Assigned by coded AI."
        });
    }

    private static void AssignSimpleBuildJob(World world, Character character)
    {
        if (!TryFindNearbyBuildTile(world, character.Position, out var target))
        {
            return;
        }

        character.Jobs.Add(new RpUnitJob
        {
            Type = RpUnitJobType.Build,
            Name = "Build simple furniture",
            TargetPosition = target,
            BuildKind = RpBuildKind.Furniture,
            BuildMaterial = MaterialType.Wood,
            BuildItemName = "simple workbench",
            BuildItemDescription = "A basic workbench.",
            BuildItemTags = ["workbench"],
            Note = "Assigned by coded desire."
        });
    }

    private static bool TryFindNearbyBuildTile(World world, Vec3Int anchor, out Vec3Int target)
    {
        target = default;
        foreach (var radius in Enumerable.Range(1, 4))
        {
            var candidate = world.Tiles.Keys
                .Where(pos => DistanceManhattan(anchor, pos) == radius)
                .Where(pos => world.Tiles[pos].Solidity != TileSolidity.Solid)
                .Where(pos => !world.Tiles[pos].OccupantIds.Any(id => world.Characters.ContainsKey(id) || world.Items.ContainsKey(id)))
                .OrderBy(pos => pos.X)
                .ThenBy(pos => pos.Z)
                .ThenBy(pos => pos.Y)
                .FirstOrDefault();
            if (candidate != default || world.Tiles.ContainsKey(candidate))
            {
                target = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryFindNearestHostile(World world, Character character, out Character hostile)
    {
        hostile = new Character();
        var match = character.PerceivedState.VisibleCharacterIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id])
            .Where(other => IsHostile(character, other))
            .OrderBy(other => DistanceManhattan(character.Position, other.Position))
            .FirstOrDefault();
        if (match == null)
        {
            return false;
        }

        hostile = match;
        return true;
    }

    private static bool TryFindStepAwayFrom(World world, Character character, Vec3Int threat, out Vec3Int target)
    {
        target = default;
        var currentDistance = DistanceManhattan(character.Position, threat);
        var best = character.Position.Neighbors()
            .Where(world.Tiles.ContainsKey)
            .Where(pos => world.Tiles[pos].Solidity != TileSolidity.Solid)
            .Where(pos => DistanceManhattan(pos, threat) > currentDistance)
            .OrderByDescending(pos => DistanceManhattan(pos, threat))
            .FirstOrDefault();
        if (best == default && !world.Tiles.ContainsKey(best))
        {
            return false;
        }

        target = best;
        return true;
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
                    Note = "Coded AI movement."
                }
            ]
        };

    private static bool IsHiveCharacter(World world, Character character)
    {
        if (character.RpTags.Any(tag => ContainsAny(tag, "hive", "brood", "queen", "worker", "changeling")))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(character.FactionId) &&
            world.Factions.TryGetValue(character.FactionId, out var faction))
        {
            return ContainsAny(faction.Name, "hive", "brood", "nest", "changeling") ||
                ContainsAny(faction.Description, "hive", "brood", "nest", "changeling");
        }

        return false;
    }

    private static bool IsQueenLike(Character character)
        => character.RpTags.Any(tag => ContainsAny(tag, "queen", "role:queen")) ||
            character.Name.Contains("queen", StringComparison.OrdinalIgnoreCase);

    private static bool IsWorkerLike(Character character)
        => character.RpTags.Any(tag => ContainsAny(tag, "worker", "builder", "drone", "role:worker")) ||
            character.CurrentGoal.Description.Contains("build", StringComparison.OrdinalIgnoreCase) ||
            character.LifeGoal.Description.Contains("build", StringComparison.OrdinalIgnoreCase);

    private static bool HasActiveJob(Character character)
        => character.Jobs.Any(job => job.Status == RpUnitJobStatus.Active);

    private static bool HasDefensiveIntent(Character character)
        => ContainsAny(
            $"{character.CurrentGoal.Description} {character.LifeGoal.Description} {string.Join(' ', character.Desires)}",
            "protect",
            "guard",
            "drive",
            "intruder",
            "hostile",
            "attack");

    private static bool TryFindFactionQueen(World world, string? factionId, out Character queen)
    {
        queen = new Character();
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        var match = world.Characters.Values
            .Where(character => string.Equals(character.FactionId, factionId, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(IsQueenLike);
        if (match == null)
        {
            return false;
        }

        queen = match;
        return true;
    }

    private static bool IsHostile(Character character, Character other)
    {
        if (other.Id == character.Id)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(character.FactionId) &&
            string.Equals(character.FactionId, other.FactionId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (character.RpTags.Contains("player") == other.RpTags.Contains("player"))
        {
            return false;
        }

        return ContainsAny($"{character.CurrentGoal.Description} {character.LifeGoal.Description} {string.Join(' ', character.Desires)}", "protect", "drive", "hostile", "attack", "intruder") ||
            ContainsAny($"{other.CurrentGoal.Description} {other.LifeGoal.Description} {string.Join(' ', other.Desires)}", "attack", "hostile", "intruder");
    }

    private static bool ContainsAny(string? value, params string[] needles)
        => !string.IsNullOrWhiteSpace(value) &&
            needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static float Ratio(float current, float max)
        => max <= 0 ? 1 : Math.Clamp(current / max, 0, 1);

    private static int DistanceManhattan(Vec3Int a, Vec3Int b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private sealed record GoalObjectMatch(Item Item, RpGoalObjectAffordance Affordance, int Score);
}

internal static class RpGoalObjectAffordanceFormattingExtensions
{
    public static string NameOrKind(this RpGoalObjectAffordance affordance)
        => string.IsNullOrWhiteSpace(affordance.Name) ? affordance.Kind.ToString() : affordance.Name;
}
