using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpConversationService(IRpTextSummarizer summarizer)
{
    public const int MaxTranscriptLength = 200; // hard safety cap; TakeLast used at the read site anyway

    /// <summary>
    /// Attempts to start a conversation between the player and the given partner.
    /// Fails (returns success=false) if: a conversation is already active, the
    /// partner does not exist, or the partner is not currently Conscious.
    /// Does NOT check line-of-sight/adjacency — the caller (ViewModel) is
    /// expected to only offer this action when the player has a valid target
    /// selected, mirroring how RpInteractionService.CreateTalkEvent worked before.
    /// </summary>
    public (bool Success, string Status, NarrativeEvent? OpeningEvent) StartConversation(
        World world, Guid playerCharacterId, Guid partnerCharacterId)
    {
        if (world.ActiveConversation is { IsActive: true } existing)
        {
            var existingPartnerName = world.Characters.TryGetValue(existing.PartnerCharacterId, out var p) ? p.Name : "someone";
            return (false, $"Already in a conversation with {existingPartnerName}. End it first.", null);
        }

        if (!world.Characters.TryGetValue(partnerCharacterId, out var partner))
        {
            return (false, "That character no longer exists.", null);
        }

        if (partner.Vitals.LifeState != RpLifeState.Conscious)
        {
            return (false, $"{partner.Name} is not able to talk right now.", null);
        }

        var playerName = world.Characters.TryGetValue(playerCharacterId, out var player) ? player.Name : "The player";

        var opening = new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = "Scene",
            Description = $"{playerName} begins speaking with {partner.Name}."
        };

        world.ActiveConversation = new RpConversationSession
        {
            PlayerCharacterId = playerCharacterId,
            PartnerCharacterId = partnerCharacterId,
            StartedTick = world.Clock.TickCount,
            Transcript = [opening]
        };

        // Nudge scene pacing forward one step if it's still at the very start.
        // This is engine-triggered (not model-triggered) so it's fine to set
        // directly rather than going through RpConsistencyArbiterService's
        // one-step-forward guard, which only applies to model-proposed changes.
        var context = RpSimulationService.GetActiveSceneContext(world);
        if (context is { SceneState.Phase: RpScenePhase.Setup })
        {
            context.SceneState.Phase = RpScenePhase.FirstContact;
        }

        return (true, $"Talking with {partner.Name}.", opening);
    }

    /// <summary>
    /// Adds a player-authored line to the active conversation's transcript.
    /// Caller must check world.ActiveConversation is not null before calling —
    /// this method throws InvalidOperationException if there is no active
    /// conversation, deliberately, so a ViewModel bug that calls this at the
    /// wrong time is caught in testing rather than silently swallowed.
    /// </summary>
    public NarrativeEvent AddPlayerLine(World world, string text)
    {
        if (world.ActiveConversation is not { IsActive: true } session)
        {
            throw new InvalidOperationException("AddPlayerLine called with no active conversation.");
        }

        var playerName = world.Characters.TryGetValue(session.PlayerCharacterId, out var player) ? player.Name : "Player";
        var evt = new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = playerName,
            Description = text
        };

        session.Transcript.Add(evt);
        if (session.Transcript.Count > MaxTranscriptLength)
        {
            session.Transcript = session.Transcript.Skip(session.Transcript.Count - MaxTranscriptLength).ToList();
        }

        // Also drop it into the partner's ordinary PerceivedLog so anything that
        // reads PerceivedLog directly (outside conversation-snapshot logic)
        // still sees it, and so it's still there if the conversation ends and
        // the partner is asked about it later in normal ambient play.
        if (world.Characters.TryGetValue(session.PartnerCharacterId, out var partner))
        {
            partner.PerceivedLog.Add(evt);
        }

        return evt;
    }

    /// <summary>
    /// Ends the active conversation (no-op, returns null, if none is active).
    /// Folds the transcript into the partner's Phase-2 MemorySummaries via the
    /// injected summarizer so the exchange isn't simply forgotten once the
    /// session object is discarded.
    /// </summary>
    public NarrativeEvent? EndConversation(World world)
    {
        if (world.ActiveConversation is not { IsActive: true } session)
        {
            return null;
        }

        if (world.Characters.TryGetValue(session.PartnerCharacterId, out var partner))
        {
            var lines = session.Transcript.Select(e => $"T{e.Tick} {e.ActorName}: {e.Description}").ToList();
            var summary = summarizer.Summarize(lines, 4);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                partner.MemorySummaries.Add($"[Conversation T{session.StartedTick}-T{world.Clock.TickCount}] {summary}");
            }
        }

        session.IsActive = false;
        world.ActiveConversation = null;

        return new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = "Scene",
            Description = "The conversation ends."
        };
    }
}
