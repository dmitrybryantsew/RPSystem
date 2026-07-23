using System.Text.Json;
using System.Text.Json.Serialization;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Core.Services;

public interface IRpLlmClient
{
    Task<LlmActionResponse> GetActionAsync(LlmSnapshot snapshot, string provider, string apiKey, string model, CancellationToken cancellationToken);
}

public sealed class RpLlmClient : IRpLlmClient
{
    private readonly OpenRouterService _openRouterService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public RpLlmClient(OpenRouterService openRouterService)
    {
        _openRouterService = openRouterService;
    }

    public async Task<LlmActionResponse> GetActionAsync(LlmSnapshot snapshot, string provider, string apiKey, string model, CancellationToken cancellationToken)
    {
        RpSimulationService.AppendDebugLog(
            $"LLM request for {snapshot.FocalCharacter.Name} | provider={provider} | model={model} | apiKeyPresent={!string.IsNullOrWhiteSpace(apiKey)} | apiKeyLength={apiKey?.Length ?? 0}");
        RpSimulationService.AppendDebugLog(
            $"Snapshot summary | world={snapshot.WorldName} | date={snapshot.CurrentDate} | activeContexts={snapshot.ActiveWorldContexts.Count} | focalProfiles={snapshot.FocalContextProfiles.Count} | focalSpecies={snapshot.FocalSpeciesTemplates.Count} | focalFactions={snapshot.FocalFactionProfiles.Count} | focalRelationships={snapshot.FocalRelationshipRules.Count} | nearbyTiles={snapshot.NearbyTiles.Count} | nearbyChars={snapshot.NearbyChars.Count} | availableActions={snapshot.AvailableActions.Count} | recentMemorySummaries={snapshot.RecentMemorySummaries.Count} | historyDigestEntries={snapshot.HistoryDigest.Count}");
        foreach (var profile in snapshot.FocalContextProfiles)
        {
            RpSimulationService.AppendDebugLog(
                $"Focal context profile | {profile.Name} | visibility={profile.Visibility} | appliesTo={profile.AppliesTo} | speechLength={profile.BehaviorProtocol?.SpeechStyle?.Length ?? 0} | negotiationLength={profile.BehaviorProtocol?.NegotiationStyle?.Length ?? 0}");
        }
        foreach (var template in snapshot.FocalSpeciesTemplates)
        {
            RpSimulationService.AppendDebugLog(
                $"Focal species template | {template.Name} | appliesTo={template.AppliesToRace} | bodyType={template.BodyType} | bodyLanguage={template.BodyLanguage?.Count ?? 0} | vocalizations={template.Vocalizations?.Count ?? 0} | tags={template.Tags?.Count ?? 0}");
        }
        foreach (var faction in snapshot.FocalFactionProfiles)
        {
            RpSimulationService.AppendDebugLog(
                $"Focal faction profile | {faction.Name} | id={faction.FactionId} | visibility={faction.Visibility} | roles={faction.Roles?.Count ?? 0} | abilities={faction.Abilities?.Count ?? 0} | relationships={faction.RelationshipRules?.Count ?? 0}");
        }
        foreach (var relationship in snapshot.FocalRelationshipRules)
        {
            RpSimulationService.AppendDebugLog(
                $"Focal relationship rule | target={relationship.TargetNameOrTag} | type={relationship.Type} | trust={relationship.Trust} | fear={relationship.Fear} | suspicion={relationship.Suspicion}");
        }

        foreach (var context in snapshot.ActiveWorldContexts)
        {
            RpSimulationService.AppendDebugLog(
                $"Active world context | {context.Name} | rulesLength={context.RulesText?.Length ?? 0} | modules={context.Modules.Count} | characters={context.Characters.Count}");
            foreach (var module in context.Modules)
            {
                RpSimulationService.AppendDebugLog(
                    $"Active context module | {module.Name} | type={module.Type} | visibility={module.Visibility} | priority={module.Priority} | appliesTo={module.AppliesTo} | textLength={module.Text?.Length ?? 0}");
            }
        }
        foreach (var availableAction in snapshot.AvailableActions)
        {
            RpSimulationService.AppendDebugLog(
                $"Available action | {availableAction.Id} | {availableAction.Label} | {JsonSerializer.Serialize(availableAction.Action, JsonOptions)}");
        }

        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(apiKey))
        {
            RpSimulationService.AppendDebugLog("LLM request skipped: no model or API key configured.");
            return RpSimulationService.WaitResponse("No model or API key configured.");
        }

        var snapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        var raw = await _openRouterService.GenerateTextAsync(
            provider,
            apiKey,
            model,
            BuildPrompt(),
            [new ChatApiMessage { Role = "user", Content = snapshotJson }]);
        RpSimulationService.AppendDebugLog($"LLM raw response ({raw?.Length ?? 0} chars): {Truncate(raw, 4000)}");
        return RpSimulationService.ParseLlmResponse(raw);
    }

    private static string BuildPrompt()
        => "You are deciding one turn for a character in a tile-based roleplay simulation. " +
            "Use the previous message as the complete JSON world snapshot. " +
            "When focalContextProfiles are present, apply their behaviorProtocol fields as the acting character's speech, encounter, negotiation, escalation, deception, relationship, combat, and capture behavior constraints. " +
            "When focalSpeciesTemplates are present, apply their body language, vocalization, diet, energy, magic, anatomy, and species tag rules to the acting character. " +
            "When focalFactionProfiles are present, apply their faction identity, culture, hierarchy, role/caste, appearance, anatomy override, faction ability, faction resource, outsider/member behavior, and faction relationship rules. Character profile overrides faction role; faction role overrides faction profile; faction profile overrides species template. " +
            "When focalRelationshipRules or nearbyChars relationship fields are present, apply those trust, fear, loyalty, dependency, manipulation, suspicion, known secret, and handling rules to choices and dialogue. " +
            "Treat recentMemorySummaries and historyDigest as compressed background memory: " +
            "they summarize things that already happened but are no longer shown in full detail. " +
            "Do not contradict them, and do not treat their absence of detail as permission to invent " +
            "new facts about what happened during those periods. " +
            "If recentEvents contains a back-and-forth exchange with the player rather than " +
            "general world events, you are in a focused conversation — respond in-scene to " +
            "the most recent player line, staying consistent with everything earlier in that " +
            "same exchange. " +
            "Return only JSON with this schema: {\"note\":\"one short sentence explaining the chosen move\",\"speech\":null|\"dialogue\",\"actions\":[{\"type\":\"Move|Attack|Interact|Speak|Wait|Use\",\"targetPos\":{\"x\":0,\"y\":0,\"z\":0}|null,\"targetId\":null,\"payload\":null,\"tickCost\":1,\"note\":\"optional short reason\"}]}. " +
            "Choose only actions listed in availableActions and copy the action object shape exactly, changing only payload text for Speak/Use if needed. " +
            "Prefer a concrete world action such as Move, Speak, or Interact when one is available. Choose Wait only when every other action is clearly useless.";

    private static string Truncate(string? value, int maxChars)
        => string.IsNullOrEmpty(value) || value.Length <= maxChars
            ? value ?? string.Empty
            : value[..maxChars] + "...";
}

public sealed class RpSimulationService
{
    private const int MaxPerceivedLogSize = 25;
    private const int PerceptionRadius = 4;
    private const int MaxWorldContextCharsPerEntry = 350000;
    private readonly IRpLlmClient _llmClient;
    public static string DebugLogPath => Path.Combine(AppPaths.AppDataDirectory, "rp-llm-debug.log");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public RpSimulationService(IRpLlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<IReadOnlyList<NarrativeEvent>> TickAsync(
        World world,
        bool useLlm,
        string provider,
        string apiKey,
        string model,
        Guid? playerCharacterId,
        CancellationToken cancellationToken)
    {
        var events = new List<NarrativeEvent>();
        AppendDebugLog($"Tick start | tick={world.Clock.TickCount + 1} | useLlm={useLlm} | provider={provider} | model={model} | playerId={playerCharacterId}");
        AdvanceClock(world);
        RegenerateSceneEscalationBudget(world);
        RegenerateCreatures(world);
        ProcessPhysics(world, events);
        UpdatePerception(world);
        if (world.ActiveConversation is { IsActive: true } activeConv &&
            (!world.Characters.TryGetValue(activeConv.PartnerCharacterId, out var partnerCheck) ||
             partnerCheck.Vitals.LifeState != RpLifeState.Conscious))
        {
            activeConv.IsActive = false;
            world.ActiveConversation = null;
        }
        if (!useLlm)
        {
            events.AddRange(new RpCodedAiService().PlanWorld(world));
        }

        foreach (var character in world.Characters.Values
            .OrderByDescending(c => c.TurnPriority)
            .ThenBy(c => c.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var eventCountBeforeThisCharacter = events.Count;
            if (character.Vitals.LifeState != RpLifeState.Conscious)
            {
                continue;
            }

            AddActionProgress(character);
            if (character.ActionQueue.Count > 0)
            {
                ProcessQueuedActions(world, character, events);
                if (character.ActionQueue.Count > 0)
                {
                    continue;
                }
            }

            if (playerCharacterId.HasValue && character.Id == playerCharacterId.Value)
            {
                continue;
            }

            if (world.ActiveConversation is { IsActive: true } focusSession &&
                character.Id != focusSession.PartnerCharacterId)
            {
                continue;
            }

            var response = useLlm
                ? await _llmClient.GetActionAsync(BuildSnapshot(world, character, playerCharacterId), provider, apiKey, model, cancellationToken)
                : CreateLocalActionResponse(world, character);
            AppendDebugLog($"Parsed response for {character.Name} | note={response.Note} | speechLength={response.Speech?.Length ?? 0} | actions={response.Actions.Count}");
            foreach (var action in response.Actions)
            {
                AppendDebugLog($"Parsed action for {character.Name} | {JsonSerializer.Serialize(action, JsonOptions)}");
            }

            if (!string.IsNullOrWhiteSpace(response.Note))
            {
                AddEvent(world, events, character.Name, $"{character.Name} chose an action: {response.Note}");
            }
            else if (!string.IsNullOrWhiteSpace(response.Thoughts))
            {
                AddEvent(world, events, character.Name, $"{character.Name} considered: {response.Thoughts}");
            }

            if (!string.IsNullOrWhiteSpace(response.Speech))
            {
                ResolveAction(world, character, new CharacterAction
                {
                    Type = ActionType.Speak,
                    Payload = response.Speech,
                    TickCost = 1
                }, events);
            }

            var validActions = ValidateActions(world, character, response.Actions);
            AppendDebugLog($"Validation for {character.Name} | validActions={validActions.Count}");
            var arbiter = new RpConsistencyArbiterService();
            var stateEvents = arbiter.ApplyStateUpdates(world, character, response);
            events.AddRange(stateEvents);
            foreach (var action in validActions)
            {
                character.ActionQueue.Enqueue(action);
            }

            if (character.ActionQueue.Count == 0)
            {
                AddEvent(world, events, character.Name, $"{character.Name} had no valid LLM action and fell back to waiting.");
                character.ActionQueue.Enqueue(new CharacterAction
                {
                    Type = ActionType.Wait,
                    Payload = "No valid action.",
                    TickCost = 1,
                    Note = "No valid action was returned."
                });
            }

            ProcessQueuedActions(world, character, events);

            if (world.ActiveConversation is { IsActive: true } syncSession &&
                character.Id == syncSession.PartnerCharacterId)
            {
                var newEvents = events.Skip(eventCountBeforeThisCharacter).ToList();
                syncSession.Transcript.AddRange(newEvents);
                if (syncSession.Transcript.Count > RpConversationService.MaxTranscriptLength)
                {
                    syncSession.Transcript = syncSession.Transcript
                        .Skip(syncSession.Transcript.Count - RpConversationService.MaxTranscriptLength)
                        .ToList();
                }
            }
        }

        UpdatePerception(world);
        AppendEventsToPerceivedLogs(world, events);

        var memoryService = new RpMemoryCompactionService(new RpRuleBasedTextSummarizer());
        foreach (var character in world.Characters.Values)
        {
            memoryService.CompactCharacterMemory(character);
        }
        memoryService.RecordAndMaybeCompactWorldHistory(world, events);

        AppendDebugLog($"Tick end | tick={world.Clock.TickCount} | events={events.Count}");
        return events;
    }

    /// <summary>
    /// Resolves the single <see cref="RpWorldContextEntry"/> whose
    /// <see cref="RpSceneState"/> and <see cref="RpContinuityState"/> should
    /// be read/written for the current scene. Prefers an enabled entry whose
    /// name does not end with "Import" (those are lore/character containers
    /// created by the markdown importer, not scene containers). Returns null
    /// when the world has no enabled contexts — callers must treat null as
    /// "scene/continuity features are inert for this world".
    /// </summary>
    public static RpWorldContextEntry? GetActiveSceneContext(World world)
    {
        var enabled = world.WorldContexts.Where(c => c.IsEnabled).ToList();
        return enabled.FirstOrDefault(c => !c.Name.EndsWith("Import", StringComparison.OrdinalIgnoreCase))
            ?? enabled.FirstOrDefault();
    }

    public static void UpdatePerception(World world)
    {
        foreach (var character in world.Characters.Values)
        {
            character.PerceivedState.VisibleTilePositions = world.Tiles.Keys
                .Where(pos => DistanceManhattan(pos, character.Position) <= PerceptionRadius)
                .ToList();

            character.PerceivedState.VisibleCharacterIds = world.Characters.Values
                .Where(other => other.Id != character.Id && DistanceManhattan(other.Position, character.Position) <= PerceptionRadius)
                .Select(other => other.Id)
                .ToList();

            character.PerceivedState.VisibleItemIds = world.Items.Values
                .Where(item => item.Position.HasValue && DistanceManhattan(item.Position.Value, character.Position) <= PerceptionRadius)
                .Select(item => item.Id)
                .ToList();
        }
    }

    public static LlmActionResponse ParseLlmResponse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("Error:", StringComparison.OrdinalIgnoreCase))
        {
            AppendDebugLog($"Parse result: wait fallback because raw was empty or error. raw={Truncate(raw, 1000)}");
            return WaitResponse(raw);
        }

        var json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            AppendDebugLog($"Parse result: wait fallback because response had no JSON object. raw={Truncate(raw, 1000)}");
            return WaitResponse("LLM response did not include JSON.");
        }

        try
        {
            var response = JsonSerializer.Deserialize<LlmActionResponse>(json, JsonOptions);
            if (response == null)
            {
                AppendDebugLog("Parse result: wait fallback because JSON parsed to null.");
                return WaitResponse("LLM JSON parsed to null.");
            }

            response.Actions ??= [];
            AppendDebugLog($"Parse result: JSON parsed. actions={response.Actions.Count} note={response.Note}");
            return response.Actions.Count == 0 ? WaitResponse("LLM returned no actions.") : response;
        }
        catch (JsonException ex)
        {
            AppendDebugLog($"Parse result: wait fallback because JSON parse failed: {ex.Message}");
            return WaitResponse($"Could not parse LLM JSON: {ex.Message}");
        }
    }

    public static LlmActionResponse WaitResponse(string? reason)
        => new()
        {
            Note = string.IsNullOrWhiteSpace(reason) ? "Waiting." : reason,
            Thoughts = string.IsNullOrWhiteSpace(reason) ? "Waiting." : reason,
            Actions =
            [
                new CharacterAction
                {
                    Type = ActionType.Wait,
                    TickCost = 1,
                    Note = string.IsNullOrWhiteSpace(reason) ? "Waiting." : reason
                }
            ]
        };

    private static LlmActionResponse CreateLocalActionResponse(World world, Character character)
    {
        if (TryGetPathTestTarget(character, out var target))
        {
            if (character.Position == target)
            {
                return WaitResponse("Path test target reached.");
            }

            return new LlmActionResponse
            {
                Note = $"Path test moving toward {target}.",
                Actions =
                [
                    new CharacterAction
                    {
                        Type = ActionType.Move,
                        TargetPos = target,
                        TickCost = 1,
                        Note = "Path test autopilot."
                    }
                ]
            };
        }

        return new RpCodedAiService().CreateLocalActionResponse(world, character);
    }

    private static bool TryGetPathTestTarget(Character character, out Vec3Int target)
    {
        const string prefix = "path-test-target:";
        target = default;
        var tag = character.RpTags.FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var parts = tag[prefix.Length..].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var x) ||
            !int.TryParse(parts[1], out var y) ||
            !int.TryParse(parts[2], out var z))
        {
            return false;
        }

        target = new Vec3Int(x, y, z);
        return true;
    }

    public LlmSnapshot BuildSnapshot(World world, Character focal, Guid? playerCharacterId = null)
    {
        var isFocalPlayer = playerCharacterId.HasValue && focal.Id == playerCharacterId.Value;
        var visiblePositions = focal.PerceivedState.VisibleTilePositions.Count > 0
            ? focal.PerceivedState.VisibleTilePositions
            : world.Tiles.Keys.Where(pos => DistanceManhattan(pos, focal.Position) <= PerceptionRadius).ToList();
        var focalProfiles = world.WorldContexts
            .Where(context => context.IsEnabled)
            .SelectMany(context => BuildVisibleContextCharacters(context, focal, isFocalPlayer))
            .Where(profile => IsContextProfileForFocal(profile, focal))
            .GroupBy(profile => profile.Id)
            .Select(group => group.First())
            .ToList();
        var visibleCharacters = focal.PerceivedState.VisibleCharacterIds
            .Where(world.Characters.ContainsKey)
            .Select(id => world.Characters[id])
            .ToList();
        var focalFactionProfiles = world.WorldContexts
            .Where(context => context.IsEnabled)
            .SelectMany(context => BuildVisibleFactionProfiles(context, focal, isFocalPlayer))
            .Where(faction => IsFactionProfileForFocal(faction, focal))
            .GroupBy(faction => faction.Id)
            .Select(group => group.First())
            .ToList();
        var focalRelationshipRules = BuildFocalRelationshipRules(focalProfiles, focalFactionProfiles, focal, visibleCharacters);
        var activeContext = GetActiveSceneContext(world);

        return new LlmSnapshot
        {
            WorldName = world.Name,
            CurrentDate = world.Clock.Display,
            WorldLoreSummary = Summarize(world.Lore, 600),
            ActiveWorldContexts = world.WorldContexts
                .Where(context => context.IsEnabled && ((context.Modules ?? []).Any(module => module.IsEnabled && !string.IsNullOrWhiteSpace(module.Text)) || !string.IsNullOrWhiteSpace(context.RulesText) || (context.Characters?.Count ?? 0) > 0 || (context.SpeciesTemplates?.Count ?? 0) > 0 || (context.Factions?.Count ?? 0) > 0))
                .Select(context => new RpWorldContextEntry
                {
                    Id = context.Id,
                    Name = context.Name,
                    IsEnabled = true,
                    RulesText = Summarize(context.RulesText, MaxWorldContextCharsPerEntry),
                    Modules = BuildActiveContextModules(context, focal, isFocalPlayer),
                    SpeciesTemplates = (context.SpeciesTemplates ?? []).Select(CloneSpeciesTemplateForSnapshot).ToList(),
                    SceneState = CloneSceneState(context.SceneState),
                    EnvironmentRules = CloneEnvironmentRules(context.EnvironmentRules),
                    Continuity = CloneContinuity(context.Continuity),
                    Characters = BuildVisibleContextCharacters(context, focal, isFocalPlayer),
                    Factions = BuildVisibleFactionProfiles(context, focal, isFocalPlayer)
                })
                .ToList(),
            FocalContextProfiles = focalProfiles,
            FocalSpeciesTemplates = world.WorldContexts
                .Where(context => context.IsEnabled)
                .SelectMany(context => context.SpeciesTemplates ?? [])
                .Where(template => IsSpeciesTemplateForFocal(template, focal))
                .Select(CloneSpeciesTemplateForSnapshot)
                .GroupBy(template => template.Id)
                .Select(group => group.First())
                .ToList(),
            FocalFactionProfiles = focalFactionProfiles,
            FocalRelationshipRules = focalRelationshipRules,
            FocalCharacter = focal,
            RecentEvents = world.ActiveConversation is { IsActive: true } conversationSession &&
                conversationSession.PartnerCharacterId == focal.Id
                    ? conversationSession.Transcript.TakeLast(60).ToList()
                    : focal.PerceivedLog.TakeLast(15).ToList(),
            NearbyTiles = visiblePositions
                .Where(world.Tiles.ContainsKey)
                .Select(pos => BuildTileSummary(world, world.Tiles[pos]))
                .Where(summary => summary.Description.Length > 0 || summary.OccupantNames.Count > 0)
                .Take(90)
                .ToList(),
            NearbyChars = focal.PerceivedState.VisibleCharacterIds
                .Where(world.Characters.ContainsKey)
                .Select(id => BuildCharSummary(focal, world.Characters[id], focalRelationshipRules))
                .ToList(),
            AvailableActions = BuildAvailableActions(world, focal),
            ActiveSceneState = activeContext?.SceneState,
            ActiveContinuity = activeContext?.Continuity,
            RecentMemorySummaries = focal.MemorySummaries.TakeLast(5).ToList(),
            HistoryDigest = world.History.TakeLast(5).Select(h => h.Summary ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s)).ToList()
        };
    }

    private static List<RpContextModule> BuildActiveContextModules(RpWorldContextEntry context, Character focal, bool isFocalPlayer)
    {
        var modules = context.Modules
            .Where(module => module.IsEnabled && !string.IsNullOrWhiteSpace(module.Text))
            .Where(module => IsContextVisibleToFocal(module.Visibility, module.AppliesTo, focal, isFocalPlayer))
            .OrderBy(module => module.Priority)
            .ThenBy(module => module.Name)
            .Select(module => new RpContextModule
            {
                Id = module.Id,
                Name = module.Name,
                IsEnabled = true,
                Type = module.Type,
                Visibility = module.Visibility,
                Priority = module.Priority,
                SourceLabel = module.SourceLabel,
                AppliesTo = module.AppliesTo,
                Text = Summarize(module.Text, MaxWorldContextCharsPerEntry)
            })
            .ToList();

        if (modules.Count == 0 && !string.IsNullOrWhiteSpace(context.RulesText))
        {
            modules.Add(new RpContextModule
            {
                Name = "Legacy Rules",
                IsEnabled = true,
                Type = RpContextModuleType.GeneralRules,
                Visibility = RpContextVisibility.WorldOnly,
                Priority = 100,
                SourceLabel = "RulesText",
                Text = Summarize(context.RulesText, MaxWorldContextCharsPerEntry)
            });
        }

        return modules;
    }

    private static List<RpWorldContextCharacter> BuildVisibleContextCharacters(RpWorldContextEntry context, Character focal, bool isFocalPlayer)
        => (context.Characters ?? [])
            .Where(character => IsContextVisibleToFocal(character.Visibility, character.AppliesTo, focal, isFocalPlayer))
            .Select(CloneContextCharacterForSnapshot)
            .ToList();

    private static RpWorldContextCharacter CloneContextCharacterForSnapshot(RpWorldContextCharacter character)
        => new()
        {
            Id = character.Id,
            Name = character.Name,
            IsNamedCharacter = character.IsNamedCharacter,
            Visibility = character.Visibility,
            AppliesTo = character.AppliesTo,
            Archetype = character.Archetype,
            Race = character.Race,
            BodyType = character.BodyType,
            FactionId = character.FactionId,
            RoleInWorld = character.RoleInWorld,
            PersonalityText = Summarize(character.PersonalityText, 12000),
            StoryText = Summarize(character.StoryText, 40000),
            AbilityText = Summarize(character.AbilityText, 12000),
            GoalText = character.GoalText,
            LifeGoalText = character.LifeGoalText,
            TagsText = character.TagsText,
            BehaviorProtocol = CloneBehaviorProtocol(character.BehaviorProtocol),
            StructuredAbilities = (character.StructuredAbilities ?? []).Select(CloneAbilityForSnapshot).ToList(),
            RelationshipRules = (character.RelationshipRules ?? []).Select(CloneRelationshipRule).ToList()
        };

    private static bool IsContextProfileForFocal(RpWorldContextCharacter profile, Character focal)
    {
        if (string.Equals(profile.Name, focal.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Race) &&
            string.Equals(profile.Race, focal.Race, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(profile.Archetype) &&
            focal.RpTags.Any(tag => string.Equals(tag, profile.Archetype, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var profileTags = profile.TagsText.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return profileTags.Any(profileTag => focal.RpTags.Any(tag => string.Equals(tag, profileTag, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<RpFactionProfile> BuildVisibleFactionProfiles(RpWorldContextEntry context, Character focal, bool isFocalPlayer)
        => (context.Factions ?? [])
            .Where(faction => faction.IsEnabled)
            .Where(faction => IsContextVisibleToFocal(faction.Visibility, faction.AppliesTo, focal, isFocalPlayer))
            .Where(faction => IsFactionProfileForFocal(faction, focal) || MatchesContextTarget(faction.AppliesTo, focal))
            .Select(CloneFactionProfileForSnapshot)
            .ToList();

    private static RpFactionProfile CloneFactionProfileForSnapshot(RpFactionProfile faction)
        => new()
        {
            Id = faction.Id,
            FactionId = faction.FactionId,
            Name = faction.Name,
            IsEnabled = true,
            Visibility = faction.Visibility,
            AppliesTo = faction.AppliesTo,
            ParentSpeciesOrRace = faction.ParentSpeciesOrRace,
            PublicDescription = Summarize(faction.PublicDescription, 8000),
            HiddenDoctrine = Summarize(faction.HiddenDoctrine, 8000),
            CultureText = Summarize(faction.CultureText, 8000),
            HierarchyText = Summarize(faction.HierarchyText, 8000),
            GoalsText = Summarize(faction.GoalsText, 8000),
            TaboosText = Summarize(faction.TaboosText, 4000),
            OutsiderBehavior = Summarize(faction.OutsiderBehavior, 4000),
            MemberBehavior = Summarize(faction.MemberBehavior, 4000),
            AppearanceText = Summarize(faction.AppearanceText, 8000),
            AnatomyOverridesText = Summarize(faction.AnatomyOverridesText, 4000),
            MagicRules = Summarize(faction.MagicRules, 6000),
            ResourceRules = Summarize(faction.ResourceRules, 4000),
            TagsText = faction.TagsText,
            Roles = (faction.Roles ?? []).Where(role => role.IsEnabled).Select(CloneFactionRoleForSnapshot).ToList(),
            Abilities = (faction.Abilities ?? []).Select(CloneAbilityForSnapshot).ToList(),
            RelationshipRules = (faction.RelationshipRules ?? []).Select(CloneRelationshipRule).ToList()
        };

    private static RpFactionRole CloneFactionRoleForSnapshot(RpFactionRole role)
        => new()
        {
            Id = role.Id,
            Name = role.Name,
            IsEnabled = true,
            AppliesToRoleOrTag = role.AppliesToRoleOrTag,
            Description = Summarize(role.Description, 4000),
            DefaultStatsText = Summarize(role.DefaultStatsText, 2000),
            BehaviorText = Summarize(role.BehaviorText, 4000),
            EquipmentText = Summarize(role.EquipmentText, 2000),
            TagsText = role.TagsText,
            Abilities = (role.Abilities ?? []).Select(CloneAbilityForSnapshot).ToList()
        };

    private static bool IsFactionProfileForFocal(RpFactionProfile faction, Character focal)
    {
        if (string.IsNullOrWhiteSpace(faction.FactionId))
        {
            return false;
        }

        if (string.Equals(focal.FactionId, faction.FactionId, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, $"faction:{faction.FactionId}", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var tags = SplitContextTargets(faction.TagsText);
        if (tags.Any(tag => focal.RpTags.Any(focalTag => string.Equals(focalTag, tag, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        var parentTargets = SplitContextTargets(faction.ParentSpeciesOrRace);
        return parentTargets.Any(target =>
            string.Equals(target, focal.Race, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)));
    }

    private static RpSpeciesTemplate CloneSpeciesTemplateForSnapshot(RpSpeciesTemplate template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            AppliesToRace = template.AppliesToRace,
            BodyType = template.BodyType,
            BodyLanguage = (template.BodyLanguage ?? []).ToDictionary(pair => pair.Key, pair => Summarize(pair.Value, 2000), StringComparer.OrdinalIgnoreCase),
            Vocalizations = (template.Vocalizations ?? []).ToDictionary(pair => pair.Key, pair => Summarize(pair.Value, 2000), StringComparer.OrdinalIgnoreCase),
            DietRules = Summarize(template.DietRules, 4000),
            EnergyRules = Summarize(template.EnergyRules, 4000),
            MagicRules = Summarize(template.MagicRules, 4000),
            AnatomyModifiers = (template.AnatomyModifiers ?? []).ToDictionary(pair => pair.Key, pair => Summarize(pair.Value, 2000), StringComparer.OrdinalIgnoreCase),
            Tags = (template.Tags ?? []).ToList()
        };

    private static bool IsSpeciesTemplateForFocal(RpSpeciesTemplate template, Character focal)
    {
        if (template.BodyType == focal.BodyType)
        {
            return true;
        }

        var targets = template.AppliesToRace.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (targets.Any(target =>
            string.Equals(target, focal.Race, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return (template.Tags ?? []).Any(templateTag =>
            string.Equals(templateTag, focal.Race, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, templateTag, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<RpRelationshipRule> BuildFocalRelationshipRules(
        IEnumerable<RpWorldContextCharacter> focalProfiles,
        IEnumerable<RpFactionProfile> focalFactionProfiles,
        Character focal,
        IReadOnlyCollection<Character> visibleCharacters)
    {
        var rules = focalProfiles
            .SelectMany(profile => profile.RelationshipRules ?? [])
            .Where(rule => RelationshipRuleMatchesAny(rule, focal, visibleCharacters))
            .Select(CloneRelationshipRule)
            .ToList();

        rules.AddRange(focalFactionProfiles
            .SelectMany(faction => faction.RelationshipRules ?? [])
            .Where(rule => RelationshipRuleMatchesAny(rule, focal, visibleCharacters))
            .Select(CloneRelationshipRule));

        foreach (var relationship in focal.KnownCharacters ?? [])
        {
            if (!visibleCharacters.Any(character => character.Id == relationship.CharacterId) &&
                !RelationshipRuleMatchesAny(relationship.Name, focal, visibleCharacters))
            {
                continue;
            }

            rules.Add(new RpRelationshipRule
            {
                TargetNameOrTag = relationship.Name,
                Type = relationship.Type,
                Trust = relationship.Trust,
                Fear = relationship.Fear,
                Dependency = relationship.Dependency,
                Loyalty = relationship.Loyalty,
                Manipulation = relationship.Manipulation,
                Suspicion = relationship.Suspicion,
                KnownSecrets = (relationship.KnownSecrets ?? []).Select(secret => Summarize(secret, 2000)).ToList(),
                HandlingRules = (relationship.SharedHistory?.Count ?? 0) == 0
                    ? string.Empty
                    : $"Shared history: {string.Join("; ", relationship.SharedHistory.Select(item => Summarize(item, 1000)))}"
            });
        }

        return rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.TargetNameOrTag))
            .GroupBy(rule => $"{rule.TargetNameOrTag}|{rule.Type}|{rule.Trust}|{rule.Fear}|{rule.Suspicion}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static bool RelationshipRuleMatchesAny(RpRelationshipRule rule, Character focal, IReadOnlyCollection<Character> visibleCharacters)
        => RelationshipRuleMatchesAny(rule.TargetNameOrTag, focal, visibleCharacters);

    private static bool RelationshipRuleMatchesAny(string targetNameOrTag, Character focal, IReadOnlyCollection<Character> visibleCharacters)
    {
        var targets = SplitContextTargets(targetNameOrTag);
        if (targets.Length == 0)
        {
            return false;
        }

        return targets.Any(target =>
            MatchesFactionTarget(target, focal) ||
            string.Equals(target, focal.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, focal.Race, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)) ||
            visibleCharacters.Any(character => RelationshipTargetMatchesCharacter(target, character)));
    }

    private static bool RelationshipTargetMatchesCharacter(string target, Character character)
        => MatchesFactionTarget(target, character) ||
            string.Equals(target, character.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, character.Race, StringComparison.OrdinalIgnoreCase) ||
            character.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase));

    private static RpCharacterBehaviorProtocol CloneBehaviorProtocol(RpCharacterBehaviorProtocol? protocol)
        => protocol == null
            ? new RpCharacterBehaviorProtocol()
            : new RpCharacterBehaviorProtocol
            {
                SpeechStyle = Summarize(protocol.SpeechStyle, 4000),
                FirstEncounterBehavior = Summarize(protocol.FirstEncounterBehavior, 4000),
                NegotiationStyle = Summarize(protocol.NegotiationStyle, 4000),
                EscalationPattern = Summarize(protocol.EscalationPattern, 4000),
                DeEscalationPattern = Summarize(protocol.DeEscalationPattern, 4000),
                RelationshipHandling = Summarize(protocol.RelationshipHandling, 4000),
                DeceptionMode = Summarize(protocol.DeceptionMode, 4000),
                CombatPreferences = Summarize(protocol.CombatPreferences, 4000),
                CapturePreferences = Summarize(protocol.CapturePreferences, 4000)
            };

    private static RpAbility CloneAbilityForSnapshot(RpAbility ability)
        => new()
        {
            Id = ability.Id,
            Name = ability.Name,
            Description = Summarize(ability.Description, 2000),
            TargetKind = ability.TargetKind,
            DamageType = ability.DamageType,
            PrimaryResource = ability.PrimaryResource,
            ManaCost = ability.ManaCost,
            FocusCost = ability.FocusCost,
            StaminaCost = ability.StaminaCost,
            Damage = ability.Damage,
            Range = ability.Range,
            TickCost = ability.TickCost,
            CooldownTicks = ability.CooldownTicks,
            RemainingCooldownTicks = ability.RemainingCooldownTicks,
            RangeText = Summarize(ability.RangeText, 1000),
            TargetRules = Summarize(ability.TargetRules, 2000),
            Constraints = Summarize(ability.Constraints, 2000),
            WorldEffect = Summarize(ability.WorldEffect, 2000),
            NarrativeEffect = Summarize(ability.NarrativeEffect, 2000),
            AllowedUsage = Summarize(ability.AllowedUsage, 2000),
            ForbiddenUsage = Summarize(ability.ForbiddenUsage, 2000),
            Tags = (ability.Tags ?? []).ToList()
        };

    private static RpRelationshipRule CloneRelationshipRule(RpRelationshipRule rule)
        => new()
        {
            Id = rule.Id,
            TargetNameOrTag = rule.TargetNameOrTag,
            Type = rule.Type,
            Trust = rule.Trust,
            Fear = rule.Fear,
            Dependency = rule.Dependency,
            Loyalty = rule.Loyalty,
            Manipulation = rule.Manipulation,
            Suspicion = rule.Suspicion,
            KnownSecrets = (rule.KnownSecrets ?? []).Select(secret => Summarize(secret, 2000)).ToList(),
            HandlingRules = Summarize(rule.HandlingRules, 4000)
        };

    private static RpSceneState CloneSceneState(RpSceneState? scene)
        => scene == null
            ? new RpSceneState()
            : new RpSceneState
            {
                Phase = scene.Phase,
                ActiveThreads = (scene.ActiveThreads ?? []).Select(thread => Summarize(thread, 2000)).ToList(),
                ForeshadowedElements = (scene.ForeshadowedElements ?? []).Select(item => Summarize(item, 2000)).ToList(),
                UnresolvedPromises = (scene.UnresolvedPromises ?? []).Select(item => Summarize(item, 2000)).ToList(),
                EscalationBudget = scene.EscalationBudget,
                EscalationRatePerTick = scene.EscalationRatePerTick,
                MajorActionPrerequisites = (scene.MajorActionPrerequisites ?? []).Select(item => Summarize(item, 2000)).ToList()
            };

    private static RpEnvironmentRuleSet CloneEnvironmentRules(RpEnvironmentRuleSet? rules)
        => rules == null
            ? new RpEnvironmentRuleSet()
            : new RpEnvironmentRuleSet
            {
                InteractiveObjects = (rules.InteractiveObjects ?? []).Select(item => new RpInteractiveObjectRule
                {
                    Id = item.Id,
                    Name = item.Name,
                    AppliesToTileOrTag = item.AppliesToTileOrTag,
                    Interaction = Summarize(item.Interaction, 2000),
                    WorldEffect = Summarize(item.WorldEffect, 2000),
                    NarrativeEffect = Summarize(item.NarrativeEffect, 2000),
                    Constraints = Summarize(item.Constraints, 2000)
                }).ToList(),
                EnvironmentalTells = (rules.EnvironmentalTells ?? []).Select(item => Summarize(item, 2000)).ToList(),
                Hazards = (rules.Hazards ?? []).Select(item => Summarize(item, 2000)).ToList(),
                TerrainAffordances = (rules.TerrainAffordances ?? []).Select(item => Summarize(item, 2000)).ToList(),
                Clues = (rules.Clues ?? []).Select(item => Summarize(item, 2000)).ToList(),
                DomainOwnerAwarenessRules = (rules.DomainOwnerAwarenessRules ?? []).Select(item => Summarize(item, 2000)).ToList()
            };

    private static RpContinuityState CloneContinuity(RpContinuityState? continuity)
        => continuity == null
            ? new RpContinuityState()
            : new RpContinuityState
            {
                PersistentPhysicalChanges = (continuity.PersistentPhysicalChanges ?? []).Select(item => Summarize(item, 2000)).ToList(),
                EmotionalStateChanges = (continuity.EmotionalStateChanges ?? []).Select(item => Summarize(item, 2000)).ToList(),
                RelationshipChanges = (continuity.RelationshipChanges ?? []).Select(item => Summarize(item, 2000)).ToList(),
                Flags = (continuity.Flags ?? []).Select(item => Summarize(item, 2000)).ToList(),
                Triggers = (continuity.Triggers ?? []).Select(item => Summarize(item, 2000)).ToList(),
                IrreversibleEvents = (continuity.IrreversibleEvents ?? []).Select(item => Summarize(item, 2000)).ToList(),
                PendingConsequences = (continuity.PendingConsequences ?? []).Select(item => Summarize(item, 2000)).ToList()
            };

    private static bool IsContextVisibleToFocal(RpContextVisibility visibility, string appliesTo, Character focal, bool isFocalPlayer)
    {
        var targetMatches = string.IsNullOrWhiteSpace(appliesTo) || MatchesContextTarget(appliesTo, focal);
        return visibility switch
        {
            RpContextVisibility.Public => targetMatches,
            RpContextVisibility.WorldOnly => !isFocalPlayer && targetMatches,
            RpContextVisibility.HiddenFromPlayer => !isFocalPlayer && targetMatches,
            RpContextVisibility.CharacterKnown => MatchesContextTarget(appliesTo, focal),
            _ => !isFocalPlayer && targetMatches
        };
    }

    private static bool MatchesContextTarget(string appliesTo, Character focal)
    {
        if (string.IsNullOrWhiteSpace(appliesTo))
        {
            return false;
        }

        var targets = SplitContextTargets(appliesTo);
        return targets.Any(target =>
            MatchesFactionTarget(target, focal) ||
            string.Equals(target, focal.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, focal.Race, StringComparison.OrdinalIgnoreCase) ||
            focal.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)));
    }

    private static string[] SplitContextTargets(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchesFactionTarget(string target, Character character)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var normalized = target.StartsWith("faction:", StringComparison.OrdinalIgnoreCase)
            ? target["faction:".Length..]
            : target;
        return !string.IsNullOrWhiteSpace(character.FactionId) &&
            string.Equals(character.FactionId, normalized, StringComparison.OrdinalIgnoreCase);
    }

    public static Direction Opposite(Direction direction)
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

    public static Vec3Int OffsetFor(Direction direction)
        => direction switch
        {
            Direction.Ceil => Vec3Int.Ceil,
            Direction.Floor => Vec3Int.Floor,
            Direction.East => Vec3Int.East,
            Direction.West => Vec3Int.West,
            Direction.North => Vec3Int.North,
            Direction.South => Vec3Int.South,
            _ => new Vec3Int()
        };

    public static bool CanEnter(World world, Vec3Int origin, Vec3Int destination)
    {
        if (!world.Tiles.TryGetValue(destination, out var tile))
        {
            return false;
        }

        if (tile.Solidity == TileSolidity.Solid)
        {
            return false;
        }

        var direction = DirectionFromDelta(destination - origin);
        if (direction == null)
        {
            return false;
        }

        var entryFace = Opposite(direction.Value);
        var originSide = world.Tiles.TryGetValue(origin, out var originTile)
            ? originTile.Sides[(int)direction.Value]
            : null;
        var destinationSide = tile.Sides[(int)entryFace];
        return IsSidePassable(originSide) && IsSidePassable(destinationSide);
    }

    private static bool IsSidePassable(Side? side)
        => side == null || side.IsPassable || side.IsOpen;

    public static bool TryMoveCharacter(World world, Character character, Vec3Int destination, out string error)
    {
        if (!RpPathfindingService.GetUsableMovementModes(character)
            .Where(mode => mode != RpMovementMode.Teleport)
            .Any(mode => RpMovementCostService.TryGetStepCost(world, character.Position, destination, mode, out _)))
        {
            error = $"Blocked: {destination}";
            return false;
        }

        MoveCharacter(world, character, destination);
        UpdatePerception(world);
        error = string.Empty;
        return true;
    }

    public static bool TryMoveCharacterToward(World world, Character character, Vec3Int target, out string error)
    {
        if (TryMoveCharacter(world, character, target, out error))
        {
            return true;
        }

        var flowStep = new RpFlowFieldService().GetNextStep(world, character, target);
        if (flowStep.HasValue && CanEnter(world, character.Position, flowStep.Value))
        {
            MoveCharacter(world, character, flowStep.Value);
            UpdatePerception(world);
            error = string.Empty;
            return true;
        }

        var path = new RpPathfindingService().FindPath(world, character, target);
        if (path.NextStep == null)
        {
            error = string.IsNullOrWhiteSpace(path.FailureReason) ? $"Blocked: {target}" : path.FailureReason;
            return false;
        }

        var nextStep = path.NextStep.Value;
        if (path.MovementMode == RpMovementMode.Teleport)
        {
            MoveCharacter(world, character, nextStep);
            UpdatePerception(world);
            error = string.Empty;
            return true;
        }

        if (!CanEnter(world, character.Position, nextStep))
        {
            error = $"Blocked: {nextStep}";
            return false;
        }

        MoveCharacter(world, character, nextStep);
        UpdatePerception(world);
        error = string.Empty;
        return true;
    }

    private static Direction? DirectionFromDelta(Vec3Int delta)
    {
        for (int i = 0; i < Vec3Int.Directions.Length; i++)
        {
            if (Vec3Int.Directions[i] == delta)
            {
                return (Direction)i;
            }
        }

        return null;
    }

    private static void ProcessQueuedActions(World world, Character character, List<NarrativeEvent> events)
    {
        var resolvedAny = false;
        while (character.ActionQueue.Count > 0)
        {
            var action = character.ActionQueue.Peek();
            var cost = Math.Max(1, action.TickCost);
            if (GetActionProgress(character, action.Type) < cost)
            {
                break;
            }

            SpendActionProgress(character, action.Type, cost);
            character.ActionQueue.Dequeue();
            ResolveAction(world, character, action, events);
            resolvedAny = true;
        }

        if (!resolvedAny && character.ActionQueue.Count > 0)
        {
            var pending = character.ActionQueue.Peek();
            AddEvent(world, events, character.Name, $"{character.Name} prepared {pending.Type.ToString().ToLowerInvariant()}.");
        }
    }

    private static void AddActionProgress(Character character)
    {
        character.ActionSpeeds.MoveProgress += Math.Max(0.05f, character.ActionSpeeds.MoveSpeed);
        character.ActionSpeeds.AttackProgress += Math.Max(0.05f, character.ActionSpeeds.AttackSpeed);
        character.ActionSpeeds.CastProgress += Math.Max(0.05f, character.ActionSpeeds.CastSpeed);
    }

    private static float GetActionProgress(Character character, ActionType actionType)
        => actionType switch
        {
            ActionType.Move => character.ActionSpeeds.MoveProgress,
            ActionType.Attack => character.ActionSpeeds.AttackProgress,
            ActionType.Use => character.ActionSpeeds.CastProgress,
            _ => 1
        };

    private static void SpendActionProgress(Character character, ActionType actionType, int cost)
    {
        switch (actionType)
        {
            case ActionType.Move:
                character.ActionSpeeds.MoveProgress = Math.Max(0, character.ActionSpeeds.MoveProgress - cost);
                break;
            case ActionType.Attack:
                character.ActionSpeeds.AttackProgress = Math.Max(0, character.ActionSpeeds.AttackProgress - cost);
                break;
            case ActionType.Use:
                character.ActionSpeeds.CastProgress = Math.Max(0, character.ActionSpeeds.CastProgress - cost);
                break;
        }
    }

    private static void AdvanceClock(World world)
    {
        world.Clock.TickCount++;
        var seconds = world.Clock.Second + Math.Max(1, world.Clock.SecondsPerTick);
        world.Clock.Minute += seconds / 60;
        world.Clock.Second = seconds % 60;
        if (world.Clock.Minute < 60)
        {
            return;
        }

        world.Clock.Minute = 0;
        world.Clock.Hour++;
        if (world.Clock.Hour < 24)
        {
            return;
        }

        world.Clock.Hour = 0;
        world.Clock.Day++;
    }

    private static void RegenerateSceneEscalationBudget(World world)
    {
        var context = GetActiveSceneContext(world);
        if (context == null) return;
        var scene = context.SceneState;
        scene.EscalationBudget = Math.Clamp(
            scene.EscalationBudget + scene.EscalationRatePerTick,
            0f,
            1f);
    }

    private static void RegenerateCreatures(World world)
    {
        foreach (var character in world.Characters.Values)
        {
            foreach (var ability in character.RpAbilities)
            {
                ability.RemainingCooldownTicks = Math.Max(0, ability.RemainingCooldownTicks - 1);
            }

            if (character.Vitals.LifeState != RpLifeState.Conscious)
            {
                continue;
            }

            character.Vitals.ManaCurrent = Regenerate(character.Vitals.ManaCurrent, character.Vitals.ManaMax, character.Vitals.ManaRegenPerTick);
            character.Vitals.FocusCurrent = Regenerate(character.Vitals.FocusCurrent, character.Vitals.FocusMax, character.Vitals.FocusRegenPerTick);
            character.Vitals.StaminaCurrent = Regenerate(character.Vitals.StaminaCurrent, character.Vitals.StaminaMax, character.Vitals.StaminaRegenPerTick);
            character.StaminaMax = character.Vitals.StaminaMax;
            character.StaminaCurrent = character.Vitals.StaminaCurrent;
        }
    }

    private static float Regenerate(float current, float max, float amount)
        => max <= 0 || amount <= 0 ? current : Math.Min(max, current + amount);


    private static void ProcessPhysics(World world, List<NarrativeEvent> events)
    {
        foreach (var tile in world.Tiles.Values.ToList())
        {
            if (tile.BulkMaterial == MaterialType.Fire || tile.BulkState == MaterialState.Plasma)
            {
                foreach (var occupantId in tile.OccupantIds)
                {
                    if (world.Characters.TryGetValue(occupantId, out var character))
                    {
                        character.Vitals.StaminaCurrent = Math.Max(0, character.Vitals.StaminaCurrent - 5);
                        character.StaminaCurrent = character.Vitals.StaminaCurrent;
                        AddEvent(world, events, character.Name, $"{character.Name} was burned by plasma heat.");
                    }
                }
            }

            if (tile.BulkHealth <= 0 && tile.Solidity == TileSolidity.Solid)
            {
                tile.Solidity = TileSolidity.Empty;
                tile.BulkMaterial = MaterialType.Air;
                tile.BulkState = MaterialState.Gas;
                world.TerrainVersion++;
                AddEvent(world, events, "World", $"A solid tile at {tile.Position} broke open.");
            }
        }
    }

    private static List<CharacterAction> ValidateActions(World world, Character character, IEnumerable<CharacterAction>? actions)
    {
        var valid = new List<CharacterAction>();
        foreach (var action in actions ?? [])
        {
            action.TickCost = Math.Max(1, action.TickCost);
            if (action.Type == ActionType.Move)
            {
                if (action.TargetPos.HasValue && CanEnter(world, character.Position, action.TargetPos.Value))
                {
                    valid.Add(action);
                }
                else if (action.TargetPos.HasValue &&
                    new RpPathfindingService().FindPath(world, character, action.TargetPos.Value).NextStep.HasValue)
                {
                    valid.Add(action);
                }
                continue;
            }

            if (action.Type == ActionType.Build)
            {
                if (action.TargetPos.HasValue)
                {
                    valid.Add(action);
                }

                continue;
            }

            if (action.Type is ActionType.Attack or ActionType.Interact or ActionType.Use)
            {
                if (action.Type == ActionType.Use && TryGetAbility(character, action.Payload, out var ability))
                {
                    if (CanUseAbility(world, character, ability, action, out _))
                    {
                        action.TickCost = Math.Max(1, ability.TickCost);
                        valid.Add(action);
                    }

                    continue;
                }

                if (action.TargetId.HasValue &&
                    (IsVisibleCharacter(world, character, action.TargetId.Value) ||
                        IsVisibleItem(world, character, action.TargetId.Value)))
                {
                    valid.Add(action);
                }
                else if (action.TargetPos.HasValue && world.Tiles.ContainsKey(action.TargetPos.Value))
                {
                    valid.Add(action);
                }

                continue;
            }

            if (action.Type == ActionType.Speak && action.TargetId.HasValue && !IsVisibleCharacter(world, character, action.TargetId.Value))
            {
                continue;
            }

            valid.Add(action);
        }

        return valid.Take(3).ToList();
    }

    private static void ResolveAction(World world, Character character, CharacterAction action, List<NarrativeEvent> events)
    {
        switch (action.Type)
        {
            case ActionType.Move when action.TargetPos.HasValue:
                if (TryMoveCharacterToward(world, character, action.TargetPos.Value, out var moveError))
                {
                    AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"moved to {character.Position}", action.Note));
                    break;
                }

                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"could not move toward {action.TargetPos.Value}: {moveError}", action.Note));
                break;
            case ActionType.Speak:
                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"said: \"{action.Payload}\"", action.Note));
                break;
            case ActionType.Interact:
                if (TryResolveGoalObjectInteraction(world, character, action, events))
                {
                    break;
                }

                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"interacted with {DescribeTarget(world, action)}", action.Note));
                break;
            case ActionType.Use:
                if (TryResolveAbilityUse(world, character, action, events))
                {
                    break;
                }

                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"used {action.Payload ?? DescribeTarget(world, action)}", action.Note));
                break;
            case ActionType.Build:
                if (new RpJobService().TryResolveBuild(world, character, action, out var buildDescription))
                {
                    AddEvent(world, events, character.Name, FormatActionEvent(character.Name, buildDescription, action.Note));
                    break;
                }

                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"could not build {DescribeTarget(world, action)}", action.Note));
                break;
            case ActionType.Attack:
                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, $"attacked {DescribeTarget(world, action)}", action.Note));
                break;
            default:
                AddEvent(world, events, character.Name, FormatActionEvent(character.Name, "waited", action.Note));
                break;
        }
    }

    private static void MoveCharacter(World world, Character character, Vec3Int destination)
    {
        if (world.Tiles.TryGetValue(character.Position, out var originTile))
        {
            originTile.OccupantIds.Remove(character.Id);
        }

        character.Position = destination;
        if (world.Tiles.TryGetValue(destination, out var destinationTile) &&
            !destinationTile.OccupantIds.Contains(character.Id))
        {
            destinationTile.OccupantIds.Add(character.Id);
        }
    }

    private static TileSummary BuildTileSummary(World world, Tile tile)
    {
        var parts = new List<string>();
        if (tile.Solidity == TileSolidity.Solid && tile.BulkMaterial.HasValue)
        {
            parts.Add($"{tile.BulkMaterial.Value} solid");
        }

        foreach (var side in tile.Sides.Where(candidate => candidate?.Material != null))
        {
            parts.Add($"{side!.Direction} {side.Material}");
        }

        if (tile.FluidLevel > 0)
        {
            parts.Add($"fluid {tile.FluidLevel}/10");
        }

        return new TileSummary
        {
            Position = tile.Position,
            Description = string.Join(", ", parts),
            IsPassable = tile.Solidity != TileSolidity.Solid,
            OccupantNames = tile.OccupantIds
                .Select(id => world.Characters.TryGetValue(id, out var c) ? c.Name : world.Items.TryGetValue(id, out var item) ? item.Name : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList()
        };
    }

    private static CharSummary BuildCharSummary(Character focal, Character other, IReadOnlyList<RpRelationshipRule> focalRelationshipRules)
    {
        var relationship = focal.KnownCharacters.FirstOrDefault(r => r.CharacterId == other.Id);
        var contextRule = focalRelationshipRules.FirstOrDefault(rule => RelationshipRuleMatchesCharacter(rule, other));
        var damagedParts = other.Body
            .Where(part => part.HpCurrent < part.HpMax * 0.75f)
            .Select(part => part.Name)
            .Take(3)
            .ToList();
        var stamina = other.StaminaCurrent < other.StaminaMax * 0.35f ? "exhausted" : "steady";
        var condition = damagedParts.Count == 0
            ? $"{other.BodyType}, {stamina}"
            : $"{other.BodyType}, {stamina}, injured: {string.Join(", ", damagedParts)}";

        return new CharSummary
        {
            Id = other.Id,
            Name = other.Name,
            Race = other.Race,
            Tags = other.RpTags.ToList(),
            Mood = other.Mood,
            VisibleCondition = condition,
            DispositionToFocal = relationship?.Disposition ?? 0,
            RelationshipType = relationship?.Type ?? contextRule?.Type ?? RpRelationshipType.Unknown,
            Trust = relationship?.Trust ?? contextRule?.Trust ?? 0,
            Fear = relationship?.Fear ?? contextRule?.Fear ?? 0,
            Dependency = relationship?.Dependency ?? contextRule?.Dependency ?? 0,
            Loyalty = relationship?.Loyalty ?? contextRule?.Loyalty ?? 0,
            Manipulation = relationship?.Manipulation ?? contextRule?.Manipulation ?? 0,
            Suspicion = relationship?.Suspicion ?? contextRule?.Suspicion ?? 0,
            KnownSecrets = relationship?.KnownSecrets?.Select(secret => Summarize(secret, 1000)).ToList() ??
                contextRule?.KnownSecrets?.Select(secret => Summarize(secret, 1000)).ToList() ??
                [],
            SharedHistory = relationship?.SharedHistory?.Select(item => Summarize(item, 1000)).ToList() ?? []
        };
    }

    private static bool RelationshipRuleMatchesCharacter(RpRelationshipRule rule, Character character)
    {
        var targets = rule.TargetNameOrTag.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return targets.Any(target => RelationshipTargetMatchesCharacter(target, character));
    }

    private static List<RpAvailableAction> BuildAvailableActions(World world, Character focal)
    {
        var actions = new List<RpAvailableAction>();

        foreach (var direction in new[] { Direction.East, Direction.West, Direction.North, Direction.South, Direction.Ceil, Direction.Floor })
        {
            var target = focal.Position + OffsetFor(direction);
            if (CanEnter(world, focal.Position, target))
            {
                actions.Add(CreateAvailableAction(
                    $"move-{direction.ToString().ToLowerInvariant()}",
                    $"Move {direction}",
                    $"Destination {target} is enterable.",
                    new CharacterAction
                    {
                        Type = ActionType.Move,
                        TargetPos = target,
                        TickCost = 1
                    }));
            }
        }

        foreach (var id in focal.PerceivedState.VisibleCharacterIds)
        {
            if (world.Characters.TryGetValue(id, out var other))
            {
                actions.Add(CreateAvailableAction(
                    $"speak-{other.Id:N}",
                    $"Speak to {other.Name}",
                    "Target is visible.",
                    new CharacterAction
                    {
                        Type = ActionType.Speak,
                        TargetId = other.Id,
                        Payload = $"Hello, {other.Name}.",
                        TickCost = 1
                    }));
                actions.Add(CreateAvailableAction(
                    $"interact-{other.Id:N}",
                    $"Interact with {other.Name}",
                    "Target is visible.",
                    new CharacterAction
                    {
                        Type = ActionType.Interact,
                        TargetId = other.Id,
                        TickCost = 1
                    }));

                if (DistanceManhattan(focal.Position, other.Position) <= 1)
                {
                    actions.Add(CreateAvailableAction(
                        $"attack-{other.Id:N}",
                        $"Attack {other.Name}",
                        "Target is adjacent.",
                        new CharacterAction
                        {
                            Type = ActionType.Attack,
                            TargetId = other.Id,
                            TickCost = 1
                        }));
                }
            }
        }

        foreach (var id in focal.PerceivedState.VisibleItemIds)
        {
            if (!world.Items.TryGetValue(id, out var item) ||
                item.Position == null)
            {
                continue;
            }

            foreach (var affordance in item.GoalAffordances.OrderByDescending(candidate => candidate.Priority).DefaultIfEmpty())
            {
                var label = affordance == null
                    ? $"Interact with {item.Name}"
                    : $"Use {item.Name}: {affordance.NameOrKind()}";
                actions.Add(CreateAvailableAction(
                    $"interact-item-{item.Id:N}-{affordance?.Kind.ToString().ToLowerInvariant() ?? "generic"}",
                    label,
                    $"Item is visible at {item.Position.Value}.",
                    new CharacterAction
                    {
                        Type = ActionType.Interact,
                        TargetId = item.Id,
                        TargetPos = item.Position.Value,
                        Payload = affordance?.Name,
                        TickCost = 1
                    }));
            }
        }

        AddAbilityActions(world, focal, actions);

        actions.Add(CreateAvailableAction(
            "wait",
            "Wait and observe",
            "Use only when no concrete move, speech, or interaction is useful.",
            new CharacterAction
            {
                Type = ActionType.Wait,
                TickCost = 1,
                Note = "Waiting is the best current option."
            }));

        return actions;
    }

    private static void AddAbilityActions(World world, Character focal, List<RpAvailableAction> actions)
    {
        foreach (var ability in focal.RpAbilities.Where(candidate => IsAbilityReady(focal, candidate, out _)))
        {
            switch (ability.TargetKind)
            {
                case RpAbilityTargetKind.Self:
                    actions.Add(CreateAbilityAction(
                        ability,
                        $"Use {ability.Name} on self",
                        "Ability targets self.",
                        new CharacterAction
                        {
                            Type = ActionType.Use,
                            TargetId = focal.Id,
                            TargetPos = focal.Position,
                            Payload = ability.Id,
                            TickCost = Math.Max(1, ability.TickCost)
                        }));
                    break;
                case RpAbilityTargetKind.Character:
                    foreach (var targetId in focal.PerceivedState.VisibleCharacterIds)
                    {
                        if (!world.Characters.TryGetValue(targetId, out var target) ||
                            DistanceManhattan(focal.Position, target.Position) > ability.Range)
                        {
                            continue;
                        }

                        actions.Add(CreateAbilityAction(
                            ability,
                            $"Use {ability.Name} on {target.Name}",
                            $"Target is visible and within range {ability.Range}.",
                            new CharacterAction
                            {
                                Type = ActionType.Use,
                                TargetId = target.Id,
                                Payload = ability.Id,
                                TickCost = Math.Max(1, ability.TickCost)
                            }));
                    }
                    break;
                case RpAbilityTargetKind.Tile:
                    foreach (var position in GetAbilityTargetTiles(world, focal, ability).Take(16))
                    {
                        actions.Add(CreateAbilityAction(
                            ability,
                            $"Use {ability.Name} at {position}",
                            $"Tile is visible and within range {ability.Range}.",
                            new CharacterAction
                            {
                                Type = ActionType.Use,
                                TargetPos = position,
                                Payload = ability.Id,
                                TickCost = Math.Max(1, ability.TickCost)
                            }));
                    }
                    break;
            }
        }
    }

    private static IEnumerable<Vec3Int> GetAbilityTargetTiles(World world, Character focal, RpAbility ability)
    {
        var visible = focal.PerceivedState.VisibleTilePositions.Count == 0
            ? world.Tiles.Keys.Where(pos => DistanceManhattan(pos, focal.Position) <= PerceptionRadius)
            : focal.PerceivedState.VisibleTilePositions;

        return visible
            .Where(pos => world.Tiles.ContainsKey(pos))
            .Where(pos => DistanceManhattan(focal.Position, pos) <= ability.Range)
            .OrderBy(pos => world.Tiles[pos].OccupantIds.Any(id => world.Characters.ContainsKey(id)) ? 0 : 1)
            .ThenBy(pos => DistanceManhattan(focal.Position, pos));
    }

    private static RpAvailableAction CreateAbilityAction(RpAbility ability, string label, string requirement, CharacterAction action)
        => CreateAvailableAction(
            $"ability-{ability.Id}-{action.TargetId?.ToString("N") ?? action.TargetPos?.ToString() ?? "self"}",
            label,
            $"{requirement} Cost: {FormatAbilityCost(ability)}. Cooldown: {ability.CooldownTicks} tick(s).",
            action);

    private static bool TryResolveAbilityUse(World world, Character character, CharacterAction action, List<NarrativeEvent> events)
    {
        if (!TryGetAbility(character, action.Payload, out var ability))
        {
            return false;
        }

        if (!CanUseAbility(world, character, ability, action, out var error))
        {
            AddEvent(world, events, character.Name, $"{character.Name} could not use {ability.Name}: {error}");
            return true;
        }

        SpendAbilityResources(character, ability);
        var affected = ApplyAbilityWorldEffect(world, character, ability, action);
        ability.RemainingCooldownTicks = ability.CooldownTicks;
        UpdatePerception(world);

        var target = DescribeTarget(world, action);
        var effect = string.IsNullOrWhiteSpace(ability.NarrativeEffect)
            ? $"used {ability.Name} on {target}"
            : $"used {ability.Name} on {target}: {ability.NarrativeEffect}";
        if (affected.Count > 0)
        {
            effect += $" Affected: {string.Join("; ", affected)}";
        }

        AddEvent(world, events, character.Name, FormatActionEvent(character.Name, effect, action.Note));
        return true;
    }

    private static List<string> ApplyAbilityWorldEffect(World world, Character caster, RpAbility ability, CharacterAction action)
    {
        var affected = new List<string>();
        if (ability.Damage <= 0)
        {
            return affected;
        }

        if (action.TargetId.HasValue && world.Characters.TryGetValue(action.TargetId.Value, out var target))
        {
            ApplyAbilityDamage(target, ability.Damage);
            affected.Add(FormatAbilityAffectedCharacter(target));
            return affected;
        }

        if (action.TargetPos.HasValue && world.Tiles.TryGetValue(action.TargetPos.Value, out var tile))
        {
            foreach (var affectedCharacter in tile.OccupantIds
                .Where(world.Characters.ContainsKey)
                .Select(id => world.Characters[id])
                .Where(candidate => candidate.Id != caster.Id || ability.TargetKind == RpAbilityTargetKind.Self))
            {
                ApplyAbilityDamage(affectedCharacter, ability.Damage);
                affected.Add(FormatAbilityAffectedCharacter(affectedCharacter));
            }
        }

        return affected;
    }

    private static void ApplyAbilityDamage(Character target, float amount)
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

        if (target.Vitals.HealthCurrent <= 0 ||
            target.Body.Any(part => part.IsCritical && part.HpCurrent <= 0))
        {
            target.Vitals.LifeState = RpLifeState.Unconscious;
        }
    }

    private static string FormatAbilityAffectedCharacter(Character target)
        => $"{target.Name} ({target.Vitals.HealthCurrent:0.#}/{target.Vitals.HealthMax:0.#} HP, {target.Vitals.LifeState})";

    private static bool CanUseAbility(World world, Character character, RpAbility ability, CharacterAction action, out string error)
    {
        if (!IsAbilityReady(character, ability, out error))
        {
            return false;
        }

        if (ability.TargetKind == RpAbilityTargetKind.Self)
        {
            return true;
        }

        if (ability.TargetKind == RpAbilityTargetKind.Character && !action.TargetId.HasValue)
        {
            error = "Ability requires a character target.";
            return false;
        }

        if (ability.TargetKind == RpAbilityTargetKind.Tile && !action.TargetPos.HasValue)
        {
            error = "Ability requires a tile target.";
            return false;
        }

        if (action.TargetId.HasValue)
        {
            if (!world.Characters.TryGetValue(action.TargetId.Value, out var target))
            {
                error = "Target character does not exist.";
                return false;
            }

            if (target.Id != character.Id && !character.PerceivedState.VisibleCharacterIds.Contains(target.Id))
            {
                error = "Target character is not visible.";
                return false;
            }

            if (DistanceManhattan(character.Position, target.Position) > ability.Range)
            {
                error = $"Target is out of range {ability.Range}.";
                return false;
            }

            return true;
        }

        if (action.TargetPos.HasValue)
        {
            if (!world.Tiles.ContainsKey(action.TargetPos.Value))
            {
                error = "Target tile does not exist.";
                return false;
            }

            if (DistanceManhattan(character.Position, action.TargetPos.Value) > ability.Range)
            {
                error = $"Target tile is out of range {ability.Range}.";
                return false;
            }

            return true;
        }

        error = "Ability target is missing.";
        return false;
    }

    private static bool IsAbilityReady(Character character, RpAbility ability, out string error)
    {
        if (character.Vitals.LifeState != RpLifeState.Conscious)
        {
            error = $"{character.Name} is not conscious.";
            return false;
        }

        if (ability.RemainingCooldownTicks > 0)
        {
            error = $"{ability.Name} is on cooldown for {ability.RemainingCooldownTicks} tick(s).";
            return false;
        }

        if (character.Vitals.ManaCurrent < ability.ManaCost)
        {
            error = $"Not enough mana. {ability.Name} needs {ability.ManaCost:0.#}.";
            return false;
        }

        if (character.Vitals.FocusCurrent < ability.FocusCost)
        {
            error = $"Not enough focus. {ability.Name} needs {ability.FocusCost:0.#}.";
            return false;
        }

        if (character.Vitals.StaminaCurrent < ability.StaminaCost)
        {
            error = $"Not enough stamina. {ability.Name} needs {ability.StaminaCost:0.#}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryGetAbility(Character character, string? abilityIdOrName, out RpAbility ability)
    {
        var matchedAbility = character.RpAbilities.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, abilityIdOrName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Name, abilityIdOrName, StringComparison.OrdinalIgnoreCase));
        if (matchedAbility == null)
        {
            ability = new RpAbility();
            return false;
        }

        ability = matchedAbility;
        return true;
    }

    private static void SpendAbilityResources(Character character, RpAbility ability)
    {
        character.Vitals.ManaCurrent = Math.Max(0, character.Vitals.ManaCurrent - ability.ManaCost);
        character.Vitals.FocusCurrent = Math.Max(0, character.Vitals.FocusCurrent - ability.FocusCost);
        character.Vitals.StaminaCurrent = Math.Max(0, character.Vitals.StaminaCurrent - ability.StaminaCost);
        character.StaminaCurrent = character.Vitals.StaminaCurrent;
    }

    private static string FormatAbilityCost(RpAbility ability)
    {
        var costs = new List<string>();
        if (ability.ManaCost > 0)
        {
            costs.Add($"{ability.ManaCost:0.#} mana");
        }

        if (ability.FocusCost > 0)
        {
            costs.Add($"{ability.FocusCost:0.#} focus");
        }

        if (ability.StaminaCost > 0)
        {
            costs.Add($"{ability.StaminaCost:0.#} stamina");
        }

        return costs.Count == 0 ? "none" : string.Join(", ", costs);
    }

    private static RpAvailableAction CreateAvailableAction(string id, string label, string requirement, CharacterAction action)
        => new()
        {
            Id = id,
            Label = label,
            Requirement = requirement,
            Action = action
        };

    private static bool IsVisibleCharacter(World world, Character focal, Guid targetId)
        => world.Characters.ContainsKey(targetId) && focal.PerceivedState.VisibleCharacterIds.Contains(targetId);

    private static bool IsVisibleItem(World world, Character focal, Guid targetId)
        => world.Items.ContainsKey(targetId) && focal.PerceivedState.VisibleItemIds.Contains(targetId);

    private static bool TryResolveGoalObjectInteraction(World world, Character character, CharacterAction action, List<NarrativeEvent> events)
    {
        if (!action.TargetId.HasValue ||
            !world.Items.TryGetValue(action.TargetId.Value, out var item))
        {
            return false;
        }

        var affordance = item.GoalAffordances
            .OrderByDescending(candidate => candidate.Priority)
            .FirstOrDefault(candidate =>
                string.IsNullOrWhiteSpace(action.Payload) ||
                string.Equals(candidate.Name, action.Payload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Kind.ToString(), action.Payload, StringComparison.OrdinalIgnoreCase));
        var result = affordance == null || string.IsNullOrWhiteSpace(affordance.ResultText)
            ? $"used {item.Name}"
            : $"used {item.Name}: {affordance.ResultText}";
        AddEvent(world, events, character.Name, FormatActionEvent(character.Name, result, action.Note));
        return true;
    }

    private static string FormatActionEvent(string actorName, string actionText, string? note)
        => string.IsNullOrWhiteSpace(note)
            ? $"{actorName} {actionText}."
            : $"{actorName} {actionText}. Reason: {note}";

    private static void AppendEventsToPerceivedLogs(World world, IReadOnlyList<NarrativeEvent> events)
    {
        foreach (var character in world.Characters.Values)
        {
            character.PerceivedLog.AddRange(events);
            // Trimming/summarization now handled by RpMemoryCompactionService.CompactCharacterMemory,
            // called right after this method in TickAsync. Do not re-add a hard trim here.
        }
    }

    private static void AddEvent(World world, List<NarrativeEvent> events, string actor, string description)
    {
        events.Add(new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor,
            Description = description
        });
    }

    private static string DescribeTarget(World world, CharacterAction action)
    {
        if (action.TargetId.HasValue)
        {
            if (world.Characters.TryGetValue(action.TargetId.Value, out var character))
            {
                return character.Name;
            }

            if (world.Items.TryGetValue(action.TargetId.Value, out var item))
            {
                return item.Name;
            }
        }

        return action.Payload ?? action.TargetPos?.ToString() ?? "nothing";
    }

    private static int DistanceManhattan(Vec3Int a, Vec3Int b)
        => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);

    private static string Summarize(string value, int maxChars)
        => string.IsNullOrWhiteSpace(value) || value.Length <= maxChars
            ? value
            : value[..maxChars].TrimEnd() + "...";

    /// <summary>
    /// Set once by the host application at startup (wired to the user's
    /// Debug setting). Defaults to false so logging is opt-in.
    /// </summary>
    public static bool DebugLoggingEnabled { get; set; }

    private const long MaxDebugLogBytes = 5 * 1024 * 1024; // 5 MB

    public static void AppendDebugLog(string message)
    {
        if (!DebugLoggingEnabled)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.AppDataDirectory);
            RotateIfTooLarge();
            File.AppendAllText(DebugLogPath, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Debug logging must not break simulation turns.
        }
    }

    private static void RotateIfTooLarge()
    {
        var info = new FileInfo(DebugLogPath);
        if (!info.Exists || info.Length <= MaxDebugLogBytes)
        {
            return;
        }

        var rotatedPath = DebugLogPath + ".old";
        File.Move(DebugLogPath, rotatedPath, overwrite: true);
    }

    private static string Truncate(string? value, int maxChars)
        => string.IsNullOrEmpty(value) || value.Length <= maxChars
            ? value ?? string.Empty
            : value[..maxChars] + "...";

    private static string? ExtractJsonObject(string raw)
    {
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return raw[start..(end + 1)];
    }
}
