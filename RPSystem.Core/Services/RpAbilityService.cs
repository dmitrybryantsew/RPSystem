using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpAbilityService
{
    public const string FireballId = "rp_fireball";

    public RpAbility CreateFireball()
        => CreateFireballAbility();

    public static RpAbility CreateFireballAbility()
        => new()
        {
            Id = FireballId,
            Name = "Fireball",
            Description = "A compact burst of magical fire aimed at a visible tile.",
            TargetKind = RpAbilityTargetKind.Tile,
            DamageType = RpDamageType.Fire,
            PrimaryResource = RpAbilityResource.Mana,
            ManaCost = 20,
            Damage = 30,
            Range = 6,
            TickCost = 1,
            CooldownTicks = 1,
            RangeText = "Up to 6 tiles by Manhattan distance.",
            TargetRules = "Target must be an existing visible tile.",
            Constraints = "Requires enough mana and a conscious caster. The current implementation does not yet trace line of sight.",
            WorldEffect = "Applies fire damage to creatures occupying the target tile.",
            NarrativeEffect = "A compact magical fire burst strikes the chosen tile.",
            AllowedUsage = "Use against hostile targets or environmental threats when the caster can afford the mana cost.",
            ForbiddenUsage = "Do not cast if the target tile is out of range, missing, or the caster lacks mana.",
            Tags = ["spell", "fire", "area"]
        };

    public bool TryCastFireball(World world, Character caster, Vec3Int targetPosition, out NarrativeEvent evt, out string status)
    {
        evt = EmptyEvent(world, caster);
        var ability = caster.RpAbilities.FirstOrDefault(a => string.Equals(a.Id, FireballId, StringComparison.OrdinalIgnoreCase));
        if (ability == null)
        {
            status = $"{caster.Name} does not know Fireball.";
            return false;
        }

        if (caster.Vitals.LifeState != RpLifeState.Conscious)
        {
            status = $"{caster.Name} cannot cast while {caster.Vitals.LifeState.ToString().ToLowerInvariant()}.";
            return false;
        }

        if (!HasResources(caster, ability, out var resourceError))
        {
            status = resourceError;
            return false;
        }

        if (ability.RemainingCooldownTicks > 0)
        {
            status = $"{ability.Name} is on cooldown for {ability.RemainingCooldownTicks} tick(s).";
            return false;
        }

        var distance = Math.Abs(caster.Position.X - targetPosition.X) +
            Math.Abs(caster.Position.Y - targetPosition.Y) +
            Math.Abs(caster.Position.Z - targetPosition.Z);
        if (distance > ability.Range)
        {
            status = $"Target is out of range. Fireball range is {ability.Range}.";
            return false;
        }

        if (!world.Tiles.TryGetValue(targetPosition, out var targetTile))
        {
            status = "Target tile does not exist.";
            return false;
        }

        SpendResources(caster, ability);
        ability.RemainingCooldownTicks = ability.CooldownTicks;
        var affected = new List<string>();
        foreach (var character in targetTile.OccupantIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id]))
        {
            ApplyDamage(character, ability.Damage, ability.DamageType);
            affected.Add($"{character.Name} ({character.Vitals.HealthCurrent:0.#}/{character.Vitals.HealthMax:0.#} HP, {character.Vitals.LifeState})");
        }

        var targetText = affected.Count == 0
            ? "no creature was caught in the blast"
            : string.Join("; ", affected);
        evt = new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = caster.Name,
            Description = $"{caster.Name} cast Fireball at {targetPosition}; {targetText}."
        };

        RpSimulationService.UpdatePerception(world);
        status = $"Fireball cast. Mana {caster.Vitals.ManaCurrent:0.#}/{caster.Vitals.ManaMax:0.#}.";
        return true;
    }

    private static bool HasResources(Character caster, RpAbility ability, out string error)
    {
        if (caster.Vitals.ManaCurrent < ability.ManaCost)
        {
            error = $"Not enough mana. {ability.Name} needs {ability.ManaCost:0.#}.";
            return false;
        }

        if (caster.Vitals.FocusCurrent < ability.FocusCost)
        {
            error = $"Not enough focus. {ability.Name} needs {ability.FocusCost:0.#}.";
            return false;
        }

        if (caster.Vitals.StaminaCurrent < ability.StaminaCost)
        {
            error = $"Not enough stamina. {ability.Name} needs {ability.StaminaCost:0.#}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void SpendResources(Character caster, RpAbility ability)
    {
        caster.Vitals.ManaCurrent = Math.Max(0, caster.Vitals.ManaCurrent - ability.ManaCost);
        caster.Vitals.FocusCurrent = Math.Max(0, caster.Vitals.FocusCurrent - ability.FocusCost);
        caster.Vitals.StaminaCurrent = Math.Max(0, caster.Vitals.StaminaCurrent - ability.StaminaCost);
        caster.StaminaCurrent = caster.Vitals.StaminaCurrent;
    }

    public void ApplyDamage(Character target, float amount, RpDamageType damageType)
    {
        target.Vitals.HealthCurrent = Math.Max(0, target.Vitals.HealthCurrent - amount);

        var criticalPart = target.Body
            .Where(part => part.IsCritical)
            .OrderBy(part => part.Role == BodyPartRole.Head || part.Role == BodyPartRole.Sensor ? 0 : 1)
            .FirstOrDefault();
        if (criticalPart != null)
        {
            criticalPart.HpCurrent = Math.Max(0, criticalPart.HpCurrent - MathF.Ceiling(amount * 0.5f));
        }

        EvaluateLifeState(target);
    }

    public void EvaluateLifeState(Character character)
    {
        if (character.Vitals.HealthCurrent <= 0 ||
            character.Body.Any(part => part.IsCritical && part.HpCurrent <= 0))
        {
            character.Vitals.LifeState = RpLifeState.Unconscious;
        }
    }

    private static NarrativeEvent EmptyEvent(World world, Character actor)
        => new()
        {
            Tick = world.Clock.TickCount,
            ActorName = actor.Name,
            Description = string.Empty
        };
}
