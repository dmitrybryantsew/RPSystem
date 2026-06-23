using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpInteractionService
{
    public NarrativeEvent CreateTalkEvent(World world, Character? actor, Vec3Int? inspectedPosition, out string status)
    {
        var target = GetInspectedCharacters(world, inspectedPosition).FirstOrDefault(c => actor == null || c.Id != actor.Id);
        if (target == null)
        {
            status = "No character on inspected tile to talk to.";
            return EmptyEvent(world, actor);
        }

        status = $"Talked to {target.Name}.";
        return new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor?.Name ?? "Player",
            Description = $"{actor?.Name ?? "Player"} greeted {target.Name}."
        };
    }

    public NarrativeEvent CreateInteractEvent(World world, Character? actor, Vec3Int? inspectedPosition, out string status)
    {
        if (inspectedPosition == null)
        {
            status = "Inspect a tile first.";
            return EmptyEvent(world, actor);
        }

        status = "Interaction recorded.";
        return new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor?.Name ?? "Player",
            Description = $"{actor?.Name ?? "Player"} interacted with tile {inspectedPosition.Value}."
        };
    }

    public NarrativeEvent CreateUseEvent(World world, Character? actor, Vec3Int? inspectedPosition, out string status)
    {
        if (actor == null)
        {
            status = "Choose a player character first.";
            return EmptyEvent(world, actor);
        }

        if (inspectedPosition == null)
        {
            status = "Inspect a tile first.";
            return EmptyEvent(world, actor);
        }

        var heldName = actor.HeldItemId.HasValue && world.Items.TryGetValue(actor.HeldItemId.Value, out var held)
            ? held.Name
            : "empty hands";
        status = $"Used {heldName}.";
        return new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor.Name,
            Description = $"{actor.Name} used {heldName} on tile {inspectedPosition.Value}."
        };
    }

    public NarrativeEvent CreateAttackEvent(World world, Character? actor, Vec3Int? inspectedPosition, out string status)
    {
        var target = GetInspectedCharacters(world, inspectedPosition).FirstOrDefault(c => actor == null || c.Id != actor.Id);
        if (target == null)
        {
            status = "No character on inspected tile to attack.";
            return EmptyEvent(world, actor);
        }

        status = $"Attack attempt against {target.Name} recorded.";
        return new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor?.Name ?? "Player",
            Description = $"{actor?.Name ?? "Player"} made an attack attempt against {target.Name}."
        };
    }

    public IReadOnlyList<Character> GetInspectedCharacters(World world, Vec3Int? inspectedPosition)
    {
        if (inspectedPosition == null || !world.Tiles.TryGetValue(inspectedPosition.Value, out var tile))
        {
            return [];
        }

        return tile.OccupantIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id])
            .ToList();
    }

    private static NarrativeEvent EmptyEvent(World world, Character? actor)
        => new()
        {
            Tick = world.Clock.TickCount,
            ActorName = actor?.Name ?? "System",
            Description = string.Empty
        };
}
