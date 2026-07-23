using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpConsistencyArbiterService
{
    public const int MaxRelationshipDeltaPerTick = 15;
    public const int MinRelationshipValue = -100;
    public const int MaxRelationshipValue = 100;

    /// <summary>
    /// Applies every structured state-change field on an LlmActionResponse to the
    /// World, on behalf of `focal`. Must be called once per character per tick,
    /// after the response has been parsed but it does not need CharacterActions
    /// to have been resolved yet (relationship/continuity changes are independent
    /// of physical action resolution). Returns narrative events describing what
    /// was actually applied, for the caller to append to the tick's event list.
    /// Never throws — any malformed/unresolvable entry is silently skipped
    /// (that's the whole point: don't let bad model output corrupt world state,
    /// and don't crash the simulation over it either).
    /// </summary>
    public List<NarrativeEvent> ApplyStateUpdates(World world, Character focal, LlmActionResponse response)
    {
        var events = new List<NarrativeEvent>();

        ApplyRelationshipDeltas(world, focal, response.RelationshipDeltas, events);
        ApplySecretDisclosures(world, focal, response.SecretsLearned, events);
        ApplyContinuityUpdate(world, focal, response.ContinuityUpdate, events);
        ApplySceneUpdate(world, focal, response.SceneUpdate, events);

        return events;
    }

    private void ApplyRelationshipDeltas(
        World world,
        Character focal,
        List<RpRelationshipDelta>? deltas,
        List<NarrativeEvent> events)
    {
        foreach (var delta in deltas ?? [])
        {
            if (string.IsNullOrWhiteSpace(delta.TargetNameOrId)) continue;

            var target = ResolveVisibleOrKnownCharacter(world, focal, delta.TargetNameOrId);
            if (target == null) continue;

            var relationship = focal.KnownCharacters.FirstOrDefault(r => r.CharacterId == target.Id);
            if (relationship == null)
            {
                relationship = new Relationship { CharacterId = target.Id, Name = target.Name };
                focal.KnownCharacters.Add(relationship);
            }

            var before = (relationship.Trust, relationship.Fear, relationship.Loyalty);

            relationship.Trust = ClampStat(relationship.Trust, delta.TrustChange);
            relationship.Fear = ClampStat(relationship.Fear, delta.FearChange);
            relationship.Dependency = ClampStat(relationship.Dependency, delta.DependencyChange);
            relationship.Loyalty = ClampStat(relationship.Loyalty, delta.LoyaltyChange);
            relationship.Manipulation = ClampStat(relationship.Manipulation, delta.ManipulationChange);
            relationship.Suspicion = ClampStat(relationship.Suspicion, delta.SuspicionChange);

            if (delta.NewType.HasValue)
            {
                relationship.Type = delta.NewType.Value;
            }

            if (before != (relationship.Trust, relationship.Fear, relationship.Loyalty))
            {
                events.Add(new NarrativeEvent
                {
                    Tick = world.Clock.TickCount,
                    ActorName = focal.Name,
                    Description = string.IsNullOrWhiteSpace(delta.Reason)
                        ? $"{focal.Name}'s feelings toward {target.Name} shifted."
                        : $"{focal.Name}'s feelings toward {target.Name} shifted: {delta.Reason}"
                });
            }
        }
    }

    private static int ClampStat(int current, int rawDelta)
    {
        var clampedDelta = Math.Clamp(rawDelta, -MaxRelationshipDeltaPerTick, MaxRelationshipDeltaPerTick);
        return Math.Clamp(current + clampedDelta, MinRelationshipValue, MaxRelationshipValue);
    }

    private void ApplySecretDisclosures(
        World world,
        Character focal,
        List<RpSecretDisclosure>? disclosures,
        List<NarrativeEvent> events)
    {
        foreach (var disclosure in disclosures ?? [])
        {
            if (string.IsNullOrWhiteSpace(disclosure.Secret)) continue;
            var target = ResolveVisibleOrKnownCharacter(world, focal, disclosure.TargetNameOrId);
            if (target == null) continue;

            var relationship = focal.KnownCharacters.FirstOrDefault(r => r.CharacterId == target.Id);
            if (relationship == null)
            {
                relationship = new Relationship { CharacterId = target.Id, Name = target.Name };
                focal.KnownCharacters.Add(relationship);
            }

            relationship.KnownSecrets ??= [];
            if (!relationship.KnownSecrets.Contains(disclosure.Secret, StringComparer.OrdinalIgnoreCase))
            {
                relationship.KnownSecrets.Add(disclosure.Secret);
                events.Add(new NarrativeEvent
                {
                    Tick = world.Clock.TickCount,
                    ActorName = focal.Name,
                    Description = $"{focal.Name} learned something about {target.Name}."
                });
            }
        }
    }

    private void ApplyContinuityUpdate(
        World world,
        Character focal,
        RpContinuityUpdate? update,
        List<NarrativeEvent> events)
    {
        if (update == null) return;
        var context = RpSimulationService.GetActiveSceneContext(world);
        if (context == null) return;
        var continuity = context.Continuity;

        AddIfPresent(continuity.PersistentPhysicalChanges, update.AddPersistentPhysicalChange);
        AddIfPresent(continuity.EmotionalStateChanges, update.AddEmotionalStateChange);
        AddIfPresent(continuity.RelationshipChanges, update.AddRelationshipChangeNote);
        AddIfPresent(continuity.Flags, update.AddFlag);
        AddIfPresent(continuity.Triggers, update.AddTrigger);
        AddIfPresent(continuity.IrreversibleEvents, update.AddIrreversibleEvent);
        AddIfPresent(continuity.PendingConsequences, update.AddPendingConsequence);

        if (!string.IsNullOrWhiteSpace(update.ResolvePendingConsequence))
        {
            continuity.PendingConsequences.RemoveAll(
                item => string.Equals(item, update.ResolvePendingConsequence, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(update.AddIrreversibleEvent))
        {
            events.Add(new NarrativeEvent
            {
                Tick = world.Clock.TickCount,
                ActorName = focal.Name,
                Description = $"[Irreversible] {update.AddIrreversibleEvent}"
            });
        }
    }

    private static void AddIfPresent(List<string> list, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(value);
        }
    }

    private void ApplySceneUpdate(
        World world,
        Character focal,
        RpSceneUpdate? update,
        List<NarrativeEvent> events)
    {
        if (update == null) return;
        var context = RpSimulationService.GetActiveSceneContext(world);
        if (context == null) return;
        var scene = context.SceneState;

        if (update.EscalationDelta > 0)
        {
            var spend = Math.Min(update.EscalationDelta, scene.EscalationBudget);
            scene.EscalationBudget -= spend;
        }

        if (update.AdvanceToPhase.HasValue)
        {
            var current = (int)scene.Phase;
            var requested = (int)update.AdvanceToPhase.Value;
            if (requested <= current || requested == current + 1)
            {
                scene.Phase = update.AdvanceToPhase.Value;
                events.Add(new NarrativeEvent
                {
                    Tick = world.Clock.TickCount,
                    ActorName = "Scene",
                    Description = $"Scene phase moved to {scene.Phase}."
                });
            }
        }

        AddIfPresent(scene.ActiveThreads, update.AddActiveThread);
        AddIfPresent(scene.ForeshadowedElements, update.AddForeshadowedElement);
        AddIfPresent(scene.UnresolvedPromises, update.AddUnresolvedPromise);

        if (!string.IsNullOrWhiteSpace(update.ResolveActiveThread))
        {
            scene.ActiveThreads.RemoveAll(
                item => string.Equals(item, update.ResolveActiveThread, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(update.ResolveUnresolvedPromise))
        {
            scene.UnresolvedPromises.RemoveAll(
                item => string.Equals(item, update.ResolveUnresolvedPromise, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static Character? ResolveVisibleOrKnownCharacter(World world, Character focal, string nameOrId)
    {
        if (Guid.TryParse(nameOrId, out var guid) && world.Characters.TryGetValue(guid, out var byId))
        {
            return byId;
        }

        var candidate = world.Characters.Values.FirstOrDefault(
            c => string.Equals(c.Name, nameOrId, StringComparison.OrdinalIgnoreCase));
        if (candidate == null) return null;

        var isVisible = focal.PerceivedState.VisibleCharacterIds.Contains(candidate.Id);
        var isKnown = focal.KnownCharacters.Any(r => r.CharacterId == candidate.Id);
        return isVisible || isKnown ? candidate : null;
    }
}
