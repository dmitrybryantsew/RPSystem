using System.Text.Json.Serialization;

namespace RPSystem.Core.RpSystem;

public readonly record struct Vec3Int(int X, int Y, int Z)
{
    public static readonly Vec3Int Ceil = new(0, 1, 0);
    public static readonly Vec3Int Floor = new(0, -1, 0);
    public static readonly Vec3Int East = new(1, 0, 0);
    public static readonly Vec3Int West = new(-1, 0, 0);
    public static readonly Vec3Int North = new(0, 0, 1);
    public static readonly Vec3Int South = new(0, 0, -1);

    public static readonly Vec3Int[] Directions = [Ceil, Floor, East, West, North, South];

    public static Vec3Int operator +(Vec3Int a, Vec3Int b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3Int operator -(Vec3Int a, Vec3Int b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public Vec3Int[] Neighbors()
    {
        var origin = this;
        var neighbors = new Vec3Int[Directions.Length];
        for (int i = 0; i < Directions.Length; i++)
        {
            neighbors[i] = origin + Directions[i];
        }

        return neighbors;
    }

    public override string ToString() => $"({X},{Y},{Z})";
}

public enum Direction
{
    Ceil = 0,
    Floor = 1,
    East = 2,
    West = 3,
    North = 4,
    South = 5
}

public enum TileSolidity { Solid, Semisolid, Empty }
public enum MaterialState { Solid, Liquid, Gas, Plasma }
public enum MaterialType { Rock, Ore, Crystal, Obsidian, Marble, Wood, Grass, Moss, Bone, Flesh, Water, Blood, Lava, Oil, SapLiquid, SapSolid, Glass, Metal, Concrete, Fire, Air, Void, Ether }
public enum SideFeature { Door, Hatch, Window, Grate }
public enum GoalStatus { Active, Completed, Failed, Suspended }
public enum ActionType { Move, Attack, Interact, Speak, Wait, Use, Build }
public enum RpSliceMode { Horizontal, Vertical }
public enum RpMapActionMode { Look, Move, Talk, Interact, Use, Attack }
public enum RpViewMode { MainMenu, World, Settings, WorldContext, GameOptions, DevMenu, TestMaps }
public enum RpContextModuleType { GeneralRules, CoreIdentity, Lore, WorldLore, CharacterBehavior, Psychology, PhysicalTraits, SpeciesBiology, AbilityMechanics, InteractionProtocols, SceneRules, EnvironmentRules, Continuity, RelationshipRules, FactionIdentity, FactionCulture, FactionHierarchy, FactionRole, FactionAppearance, SafetyFilter, ImportNotes }
public enum RpContextVisibility { Public, CharacterKnown, WorldOnly, HiddenFromPlayer }
public enum RpImportSafetyState { Unreviewed, Allowed, NeedsReview, DisabledPromptInjection, DisabledUnsafe }
public enum RpRelationshipType { Unknown, Ally, Subordinate, Equal, Superior, Rival, Enemy, Dependent, Captor, Captive }
public enum RpAuthoringTargetKind
{
    FactionProfile,
    ContextCharacter,
    RelationshipRule,
    SpeciesTemplate,
    ContextModule
}
public enum RpScenePhase { Setup, FirstContact, Exploration, Negotiation, Escalation, Conflict, Aftermath, Downtime }
public enum BodyTypeKind { Human, Humanoid, Quadruped, Equine, Avian, Serpentine, Construct, Changeling }
public enum BodyPartRole { Core, Head, Neck, Arm, Hand, Leg, Foot, Wing, Tail, Sensor, ToolMount, Horn }
public enum BodyPartSide { None, Left, Right, FrontLeft, FrontRight, RearLeft, RearRight }
public enum RpLifeState { Conscious, Unconscious, Dead }
public enum RpAbilityTargetKind { Self, Tile, Character }
public enum RpDamageType { Physical, Fire, Cold, Lightning, Arcane }
public enum RpAbilityResource { None, Mana, Focus, Stamina }
public enum RpMovementMode { Walk, Fly, Swim, Teleport, Climb, Burrow }
public enum RpTileMovementFeature { RampUp, RampDown, LadderUp, LadderDown }
public enum RpUnitJobType { Patrol, Build, Follow }
public enum RpUnitJobStatus { Active, Completed, Failed, Suspended }
public enum RpBuildKind { FullWall, Floor, SideWall, Door, Window, RampUp, RampDown, LadderUp, LadderDown, Furniture }
public enum RpNeedType { Health, Stamina, Mana, Focus, Safety, Purpose }
public enum RpGoalObjectKind { Workstation, Resource, Storage, RestSpot, DefensivePost, QueenThrone, Beacon, FoodSource, Tool, Exit, Custom }

public sealed class Side
{
    public Direction Direction { get; set; }
    public MaterialType? Material { get; set; }
    public float Health { get; set; }
    public bool IsPassable { get; set; } = true;
    public bool IsOpen { get; set; }
    public SideFeature? Feature { get; set; }
}

public sealed class Tile
{
    public Vec3Int Position { get; set; }
    public TileSolidity Solidity { get; set; } = TileSolidity.Empty;
    public MaterialState? BulkState { get; set; }
    public MaterialType? BulkMaterial { get; set; }
    public float BulkHealth { get; set; }
    public Side?[] Sides { get; set; } = new Side?[6];
    public float Temperature { get; set; } = 293.15f;
    public int FluidLevel { get; set; }
    public List<RpTileMovementFeature> MovementFeatures { get; set; } = [];
    public List<Guid> OccupantIds { get; set; } = [];
}

public sealed class World
{
    public string Name { get; set; } = "Untitled World";
    public string Lore { get; set; } = string.Empty;
    public List<RpWorldContextEntry> WorldContexts { get; set; } = [];
    public List<string> NotablePlaces { get; set; } = [];
    public List<HistoryYear> History { get; set; } = [];
    public Dictionary<string, Faction> Factions { get; set; } = [];
    [JsonIgnore]
    public Dictionary<Vec3Int, Tile> Tiles { get; set; } = [];
    public Dictionary<Guid, Character> Characters { get; set; } = [];
    public Dictionary<Guid, Item> Items { get; set; } = [];
    public WorldClock Clock { get; set; } = new();

    /// <summary>
    /// Stable identity for this world instance. Used as a cheap cache key
    /// for flow fields instead of hashing tile contents.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Incremented every time a tile's shape (solidity, bulk material/state,
    /// movement features, or side passability) changes. Used as a cheap cache
    /// key for flow fields instead of re-hashing every tile on every lookup.
    /// Code that mutates tile shape MUST increment this.
    /// </summary>
    public int TerrainVersion { get; set; }

    /// <summary>
    /// Every NarrativeEvent produced by every tick, world-wide, awaiting
    /// long-term compaction into World.History. Bounded by
    /// RpMemoryCompactionService.MaxRawGlobalEventLog — once that many events
    /// accumulate, the oldest are compacted into a HistoryYear entry even if
    /// the normal tick-count interval hasn't been reached yet, so this can
    /// never grow unbounded even under very fast ticking.
    /// </summary>
    public List<NarrativeEvent> GlobalEventLog { get; set; } = [];

    /// <summary>
    /// TickCount at which GlobalEventLog was last compacted into World.History.
    /// Used by RpMemoryCompactionService to decide when the next compaction is due.
    /// </summary>
    public long LastHistoryCompactionTick { get; set; }

    /// <summary>Null when no conversation is in progress. Only one at a time — see
    /// RpConversationService for how starting a new one interacts with an existing one.</summary>
    public RpConversationSession? ActiveConversation { get; set; }
}

/// <summary>
/// An in-progress focused exchange between the player character and one NPC.
/// While active, RpSimulationService.TickAsync gives the partner character's
/// LLM call the full Transcript instead of the usual world-event window, and
/// skips new LLM/coded-AI decisions for every other character.
/// </summary>
public sealed class RpConversationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerCharacterId { get; set; }
    public Guid PartnerCharacterId { get; set; }
    /// <summary>Every event belonging to this conversation, in order, never
    /// trimmed by the normal 25-item PerceivedLog cap. Bounded only by
    /// RpConversationService.MaxTranscriptLength as a hard safety cap.</summary>
    public List<NarrativeEvent> Transcript { get; set; } = [];
    public long StartedTick { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RpWorldContextEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "World Context";
    public bool IsEnabled { get; set; } = true;
    public string RulesText { get; set; } = string.Empty;
    public List<RpContextModule> Modules { get; set; } = [];
    public List<RpWorldContextCharacter> Characters { get; set; } = [];
    public List<RpSpeciesTemplate> SpeciesTemplates { get; set; } = [];
    public List<RpFactionProfile> Factions { get; set; } = [];
    public RpSceneState SceneState { get; set; } = new();
    public RpEnvironmentRuleSet EnvironmentRules { get; set; } = new();
    public RpContinuityState Continuity { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => IsEnabled ? Name : $"{Name} (off)";
}

public sealed class RpContextModule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Context Module";
    public bool IsEnabled { get; set; } = true;
    public RpContextModuleType Type { get; set; } = RpContextModuleType.GeneralRules;
    public RpContextVisibility Visibility { get; set; } = RpContextVisibility.WorldOnly;
    public int Priority { get; set; } = 100;
    public string Text { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string AppliesTo { get; set; } = string.Empty;
    public RpImportSafetyState ImportSafety { get; set; } = RpImportSafetyState.Unreviewed;
    public string SanitizationNote { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => $"{Priority:000} {Name} [{Type}, {Visibility}] {(IsEnabled ? string.Empty : "(off)")}".Trim();
}

public sealed class RpWorldContextCharacter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Character";
    public bool IsNamedCharacter { get; set; }
    public RpContextVisibility Visibility { get; set; } = RpContextVisibility.WorldOnly;
    public string AppliesTo { get; set; } = string.Empty;
    public string Archetype { get; set; } = "regular";
    public string Race { get; set; } = "Human";
    public BodyTypeKind BodyType { get; set; } = BodyTypeKind.Human;
    public string FactionId { get; set; } = string.Empty;
    public string RoleInWorld { get; set; } = string.Empty;
    public string PersonalityText { get; set; } = string.Empty;
    public string StoryText { get; set; } = string.Empty;
    public string AbilityText { get; set; } = string.Empty;
    public string GoalText { get; set; } = "Wait and observe.";
    public string LifeGoalText { get; set; } = string.Empty;
    public string TagsText { get; set; } = "creature, sapient";
    public RpCharacterBehaviorProtocol BehaviorProtocol { get; set; } = new();
    public List<RpAbility> StructuredAbilities { get; set; } = [];
    public List<RpRelationshipRule> RelationshipRules { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{(IsNamedCharacter ? Name : $"{Name} ({Archetype})")} [{Visibility}]";
}

public sealed class RpCharacterBehaviorProtocol
{
    public string SpeechStyle { get; set; } = string.Empty;
    public string FirstEncounterBehavior { get; set; } = string.Empty;
    public string NegotiationStyle { get; set; } = string.Empty;
    public string EscalationPattern { get; set; } = string.Empty;
    public string DeEscalationPattern { get; set; } = string.Empty;
    public string RelationshipHandling { get; set; } = string.Empty;
    public string DeceptionMode { get; set; } = string.Empty;
    public string CombatPreferences { get; set; } = string.Empty;
    public string CapturePreferences { get; set; } = string.Empty;
}

public sealed class RpSpeciesTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Species";
    public string AppliesToRace { get; set; } = string.Empty;
    public BodyTypeKind BodyType { get; set; } = BodyTypeKind.Human;
    public Dictionary<string, string> BodyLanguage { get; set; } = [];
    public Dictionary<string, string> Vocalizations { get; set; } = [];
    public string DietRules { get; set; } = string.Empty;
    public string EnergyRules { get; set; } = string.Empty;
    public string MagicRules { get; set; } = string.Empty;
    public Dictionary<string, string> AnatomyModifiers { get; set; } = [];
    public List<string> Tags { get; set; } = [];
}

public sealed class RpRelationshipRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TargetNameOrTag { get; set; } = string.Empty;
    public RpRelationshipType Type { get; set; } = RpRelationshipType.Unknown;
    public int Trust { get; set; }
    public int Fear { get; set; }
    public int Dependency { get; set; }
    public int Loyalty { get; set; }
    public int Manipulation { get; set; }
    public int Suspicion { get; set; }
    public List<string> KnownSecrets { get; set; } = [];
    public string HandlingRules { get; set; } = string.Empty;
}

public sealed class RpFactionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FactionId { get; set; } = string.Empty;
    public string Name { get; set; } = "Faction";
    public bool IsEnabled { get; set; } = true;
    public RpContextVisibility Visibility { get; set; } = RpContextVisibility.WorldOnly;
    public string AppliesTo { get; set; } = string.Empty;
    public string ParentSpeciesOrRace { get; set; } = string.Empty;
    public string PublicDescription { get; set; } = string.Empty;
    public string HiddenDoctrine { get; set; } = string.Empty;
    public string CultureText { get; set; } = string.Empty;
    public string HierarchyText { get; set; } = string.Empty;
    public string GoalsText { get; set; } = string.Empty;
    public string TaboosText { get; set; } = string.Empty;
    public string OutsiderBehavior { get; set; } = string.Empty;
    public string MemberBehavior { get; set; } = string.Empty;
    public string AppearanceText { get; set; } = string.Empty;
    public string AnatomyOverridesText { get; set; } = string.Empty;
    public string MagicRules { get; set; } = string.Empty;
    public string ResourceRules { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public List<RpFactionRole> Roles { get; set; } = [];
    public List<RpAbility> Abilities { get; set; } = [];
    public List<RpRelationshipRule> RelationshipRules { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{Name} [{FactionId}] {(IsEnabled ? string.Empty : "(off)")}".Trim();
}

public sealed class RpFactionRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Role";
    public bool IsEnabled { get; set; } = true;
    public string AppliesToRoleOrTag { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultStatsText { get; set; } = string.Empty;
    public string BehaviorText { get; set; } = string.Empty;
    public string EquipmentText { get; set; } = string.Empty;
    public string TagsText { get; set; } = string.Empty;
    public List<RpAbility> Abilities { get; set; } = [];

    [JsonIgnore]
    public string DisplayName => $"{Name} {(IsEnabled ? string.Empty : "(off)")}".Trim();
}

public sealed class RpSceneState
{
    public RpScenePhase Phase { get; set; } = RpScenePhase.Setup;
    public List<string> ActiveThreads { get; set; } = [];
    public List<string> ForeshadowedElements { get; set; } = [];
    public List<string> UnresolvedPromises { get; set; } = [];
    public float EscalationBudget { get; set; } = 1;
    public float EscalationRatePerTick { get; set; } = 0.1f;
    public List<string> MajorActionPrerequisites { get; set; } = [];
}

public sealed class RpEnvironmentRuleSet
{
    public List<RpInteractiveObjectRule> InteractiveObjects { get; set; } = [];
    public List<string> EnvironmentalTells { get; set; } = [];
    public List<string> Hazards { get; set; } = [];
    public List<string> TerrainAffordances { get; set; } = [];
    public List<string> Clues { get; set; } = [];
    public List<string> DomainOwnerAwarenessRules { get; set; } = [];
}

public sealed class RpInteractiveObjectRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string AppliesToTileOrTag { get; set; } = string.Empty;
    public string Interaction { get; set; } = string.Empty;
    public string WorldEffect { get; set; } = string.Empty;
    public string NarrativeEffect { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
}

public sealed class RpContinuityState
{
    public List<string> PersistentPhysicalChanges { get; set; } = [];
    public List<string> EmotionalStateChanges { get; set; } = [];
    public List<string> RelationshipChanges { get; set; } = [];
    public List<string> Flags { get; set; } = [];
    public List<string> Triggers { get; set; } = [];
    public List<string> IrreversibleEvents { get; set; } = [];
    public List<string> PendingConsequences { get; set; } = [];
}

/// <summary>
/// A single proposed change to the focal character's relationship with another
/// character. Deltas are relative (added to current value), not absolute —
/// this prevents the model from claiming a relationship state it doesn't know
/// the true current value of.
/// </summary>
public sealed class RpRelationshipDelta
{
    /// <summary>Must match a character Name or Guid (as string) that is currently
    /// visible to the focal character, or a character already present in
    /// focal.KnownCharacters. Deltas targeting anyone else are dropped.</summary>
    public string TargetNameOrId { get; set; } = string.Empty;
    public int TrustChange { get; set; }
    public int FearChange { get; set; }
    public int DependencyChange { get; set; }
    public int LoyaltyChange { get; set; }
    public int ManipulationChange { get; set; }
    public int SuspicionChange { get; set; }
    /// <summary>Optional — if set, overwrites the relationship Type outright
    /// (e.g. Unknown -> Ally). Type changes are not clamped, but see
    /// RpConsistencyArbiterService for the one guard rail that applies.</summary>
    public RpRelationshipType? NewType { get; set; }
    /// <summary>Free-text reason, echoed into the narrative event log for
    /// debugging/inspection. Not shown to other characters.</summary>
    public string? Reason { get; set; }
}

/// <summary>A secret the focal character learned about another character during
/// this exchange. Distinct from RelationshipDelta because secrets are additive
/// and never clamped/decayed.</summary>
public sealed class RpSecretDisclosure
{
    public string TargetNameOrId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
}

/// <summary>Structured continuity changes proposed for this tick. Every field is
/// optional; only non-null/non-empty fields are applied. Each "Add*" field adds
/// one string entry to the corresponding list on RpContinuityState.</summary>
public sealed class RpContinuityUpdate
{
    public string? AddPersistentPhysicalChange { get; set; }
    public string? AddEmotionalStateChange { get; set; }
    public string? AddRelationshipChangeNote { get; set; }
    public string? AddFlag { get; set; }
    public string? AddTrigger { get; set; }
    public string? AddIrreversibleEvent { get; set; }
    public string? AddPendingConsequence { get; set; }
    /// <summary>Exact string match against an existing entry in
    /// RpContinuityState.PendingConsequences to remove (the consequence has now
    /// happened / been resolved). No-op if not found.</summary>
    public string? ResolvePendingConsequence { get; set; }
}

/// <summary>Structured scene-pacing changes proposed for this tick.</summary>
public sealed class RpSceneUpdate
{
    /// <summary>If set, requests the scene phase move to this value. The arbiter
    /// only allows moving to the next phase in RpScenePhase declaration order, or
    /// staying the same, or moving backward (de-escalating) — never skipping
    /// forward more than one step. See RpConsistencyArbiterService.</summary>
    public RpScenePhase? AdvanceToPhase { get; set; }
    /// <summary>How much of the scene's escalation budget this beat consumes,
    /// 0.0-1.0. Clamped to remaining budget by the arbiter — see below.</summary>
    public float EscalationDelta { get; set; }
    public string? AddActiveThread { get; set; }
    public string? ResolveActiveThread { get; set; }
    public string? AddForeshadowedElement { get; set; }
    public string? AddUnresolvedPromise { get; set; }
    public string? ResolveUnresolvedPromise { get; set; }
}

public sealed class HistoryYear
{
    public int Year { get; set; }
    public List<string> Events { get; set; } = [];
    public string? Summary { get; set; }
}

public sealed class WorldClock
{
    public int Year { get; set; } = 1;
    public int Month { get; set; } = 1;
    public int Day { get; set; } = 1;
    public int Hour { get; set; } = 9;
    public int Minute { get; set; }
    public int Second { get; set; }
    public long TickCount { get; set; }
    public int SecondsPerTick { get; set; } = 6;

    public string Display => $"Year {Year}, Day {Day}, {Hour:00}:{Minute:00}:{Second:00} (T{TickCount}, {SecondsPerTick}s)";
}

public sealed class Faction
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, int> DispositionTo { get; set; } = [];
}

public sealed class Character
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Unnamed";
    public string Race { get; set; } = "Human";
    public Vec3Int Position { get; set; }
    public List<string> PersonalityTraits { get; set; } = [];
    public List<string> Ideals { get; set; } = [];
    public List<string> Desires { get; set; } = [];
    public List<string> Abilities { get; set; } = [];
    public List<string> RpTags { get; set; } = [];
    public string Mood { get; set; } = "calm";
    public List<RpNeedState> Needs { get; set; } = [];
    public BodyTypeKind BodyType { get; set; } = BodyTypeKind.Human;
    public List<BodyPart> Body { get; set; } = [];
    public RpVitals Vitals { get; set; } = new();
    public List<RpAbility> RpAbilities { get; set; } = [];
    public RpActionSpeeds ActionSpeeds { get; set; } = new();
    public RpMovementProfile Movement { get; set; } = new();
    public float StaminaMax { get; set; } = 100;
    public float StaminaCurrent { get; set; } = 100;
    public Goal CurrentGoal { get; set; } = new();
    public Goal LifeGoal { get; set; } = new();
    public Goal? HiddenGoal { get; set; }
    public List<RpUnitJob> Jobs { get; set; } = [];
    public Queue<CharacterAction> ActionQueue { get; set; } = new();
    public string? FactionId { get; set; }
    public List<Relationship> KnownCharacters { get; set; } = [];
    public List<Guid> Inventory { get; set; } = [];
    public Guid? HeldItemId { get; set; }
    public List<Guid> WornItemIds { get; set; } = [];
    public List<NarrativeEvent> PerceivedLog { get; set; } = [];
    public PerceivedWorldState PerceivedState { get; set; } = new();

    /// <summary>
    /// Compressed short-term memory. Populated by RpMemoryCompactionService when
    /// PerceivedLog overflows its raw cap. Each entry is a summary of a batch of
    /// events that were removed from PerceivedLog, oldest batch first. Never
    /// truncated by tick-level code directly — only RpMemoryCompactionService
    /// manages its size (see MaxMemorySummaries).
    /// </summary>
    public List<string> MemorySummaries { get; set; } = [];

    /// <summary>
    /// Higher values act earlier within a tick. Defaults to 0 for everyone
    /// (today's behavior is unaffected beyond the tiebreak), but gives future
    /// initiative/speed systems a place to plug in without changing TickAsync.
    /// </summary>
    public int TurnPriority { get; set; }
}

public sealed class RpNeedState
{
    public RpNeedType Type { get; set; }
    public float Value { get; set; } = 1;
    public float Urgency { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class RpUnitJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public RpUnitJobType Type { get; set; }
    public RpUnitJobStatus Status { get; set; } = RpUnitJobStatus.Active;
    public int Priority { get; set; } = 100;
    public string Name { get; set; } = string.Empty;
    public Guid? TargetCharacterId { get; set; }
    public Vec3Int? TargetPosition { get; set; }
    public List<Vec3Int> Waypoints { get; set; } = [];
    public int CurrentWaypointIndex { get; set; }
    public bool Repeat { get; set; } = true;
    public int FollowDistance { get; set; } = 1;
    public RpBuildKind BuildKind { get; set; } = RpBuildKind.FullWall;
    public Direction? BuildDirection { get; set; }
    public TileSolidity BuildSolidity { get; set; } = TileSolidity.Empty;
    public MaterialType BuildMaterial { get; set; } = MaterialType.Air;
    public MaterialState BuildState { get; set; } = MaterialState.Gas;
    public string BuildItemName { get; set; } = string.Empty;
    public string BuildItemDescription { get; set; } = string.Empty;
    public List<string> BuildItemTags { get; set; } = [];
    public string Note { get; set; } = string.Empty;
}

public sealed class RpMovementProfile
{
    public List<RpMovementMode> Modes { get; set; } = [RpMovementMode.Walk];
    public int TeleportRange { get; set; }
    public int MaxPathSearchNodes { get; set; } = 4096;
}

public sealed class RpActionSpeeds
{
    public float MoveSpeed { get; set; } = 1;
    public float AttackSpeed { get; set; } = 1;
    public float CastSpeed { get; set; } = 1;
    public float MoveProgress { get; set; }
    public float AttackProgress { get; set; }
    public float CastProgress { get; set; }
}

public sealed class RpVitals
{
    public float HealthMax { get; set; } = 100;
    public float HealthCurrent { get; set; } = 100;
    public float ManaMax { get; set; }
    public float ManaCurrent { get; set; }
    public float FocusMax { get; set; }
    public float FocusCurrent { get; set; }
    public float StaminaMax { get; set; } = 100;
    public float StaminaCurrent { get; set; } = 100;
    public float ManaRegenPerTick { get; set; }
    public float FocusRegenPerTick { get; set; } = 2;
    public float StaminaRegenPerTick { get; set; } = 3;
    public RpLifeState LifeState { get; set; } = RpLifeState.Conscious;
}

public sealed class RpAbility
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RpAbilityTargetKind TargetKind { get; set; } = RpAbilityTargetKind.Tile;
    public RpDamageType DamageType { get; set; } = RpDamageType.Physical;
    public RpAbilityResource PrimaryResource { get; set; } = RpAbilityResource.None;
    public float ManaCost { get; set; }
    public float FocusCost { get; set; }
    public float StaminaCost { get; set; }
    public float Damage { get; set; }
    public int Range { get; set; } = 1;
    public int TickCost { get; set; } = 1;
    public int CooldownTicks { get; set; }
    public int RemainingCooldownTicks { get; set; }
    public string RangeText { get; set; } = string.Empty;
    public string TargetRules { get; set; } = string.Empty;
    public string Constraints { get; set; } = string.Empty;
    public string WorldEffect { get; set; } = string.Empty;
    public string NarrativeEffect { get; set; } = string.Empty;
    public string AllowedUsage { get; set; } = string.Empty;
    public string ForbiddenUsage { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}

public sealed class BodyPart
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BodyTypeKind BodyType { get; set; } = BodyTypeKind.Human;
    public BodyPartRole Role { get; set; } = BodyPartRole.Core;
    public BodyPartSide Side { get; set; } = BodyPartSide.None;
    public string? ParentPartId { get; set; }
    public float HpMax { get; set; } = 10;
    public float HpCurrent { get; set; } = 10;
    public bool IsCritical { get; set; }
    public List<string> Functions { get; set; } = [];
    public List<string> WearSlots { get; set; } = [];
    public bool CanHoldItem { get; set; }
    public Guid? HeldItemId { get; set; }
    public List<Guid> WornItemIds { get; set; } = [];
}

public sealed class Goal
{
    public string Description { get; set; } = "Wait and observe.";
    public bool IsDeceiving { get; set; }
    public List<string> SubGoals { get; set; } = [];
    public GoalStatus Status { get; set; } = GoalStatus.Active;
}

public sealed class Relationship
{
    public Guid CharacterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Disposition { get; set; }
    public RpRelationshipType Type { get; set; } = RpRelationshipType.Unknown;
    public int Trust { get; set; }
    public int Fear { get; set; }
    public int Dependency { get; set; }
    public int Loyalty { get; set; }
    public int Manipulation { get; set; }
    public int Suspicion { get; set; }
    public List<string> KnownSecrets { get; set; } = [];
    public List<string> SharedHistory { get; set; } = [];
}

public sealed class CharacterAction
{
    public ActionType Type { get; set; } = ActionType.Wait;
    public Vec3Int? TargetPos { get; set; }
    public Guid? TargetId { get; set; }
    public string? Payload { get; set; }
    public int TickCost { get; set; } = 1;
    public string? Note { get; set; }
}

public sealed class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MaterialType Material { get; set; }
    public float Weight { get; set; }
    public float Condition { get; set; } = 1;
    public List<string> Tags { get; set; } = [];
    public List<RpGoalObjectAffordance> GoalAffordances { get; set; } = [];
    public Vec3Int? Position { get; set; }
    public Guid? OwnerId { get; set; }
}

public sealed class RpGoalObjectAffordance
{
    public RpGoalObjectKind Kind { get; set; } = RpGoalObjectKind.Custom;
    public string Name { get; set; } = string.Empty;
    public List<string> GoalKeywords { get; set; } = [];
    public List<string> DesireKeywords { get; set; } = [];
    public List<RpNeedType> NeedTypes { get; set; } = [];
    public int Priority { get; set; } = 50;
    public int InteractionRange { get; set; } = 1;
    public string ResultText { get; set; } = string.Empty;
}

public sealed class PerceivedWorldState
{
    public List<Vec3Int> VisibleTilePositions { get; set; } = [];
    public List<Guid> VisibleCharacterIds { get; set; } = [];
    public List<Guid> VisibleItemIds { get; set; } = [];
    public string? LastHeardSound { get; set; }
    public string? LastSmell { get; set; }
}

public sealed class NarrativeEvent
{
    public long Tick { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public sealed class LlmSnapshot
{
    public string WorldName { get; set; } = string.Empty;
    public string CurrentDate { get; set; } = string.Empty;
    public string WorldLoreSummary { get; set; } = string.Empty;
    public List<RpWorldContextEntry> ActiveWorldContexts { get; set; } = [];
    public List<RpWorldContextCharacter> FocalContextProfiles { get; set; } = [];
    public List<RpSpeciesTemplate> FocalSpeciesTemplates { get; set; } = [];
    public List<RpFactionProfile> FocalFactionProfiles { get; set; } = [];
    public List<RpRelationshipRule> FocalRelationshipRules { get; set; } = [];
    public Character FocalCharacter { get; set; } = new();
    public List<NarrativeEvent> RecentEvents { get; set; } = [];
    public List<TileSummary> NearbyTiles { get; set; } = [];
    public List<CharSummary> NearbyChars { get; set; } = [];
    public List<RpAvailableAction> AvailableActions { get; set; } = [];

    // --- NEW for Phase 1 ---
    /// <summary>Null if GetActiveSceneContext(world) returned null.</summary>
    public RpSceneState? ActiveSceneState { get; set; }
    /// <summary>Null if GetActiveSceneContext(world) returned null.</summary>
    public RpContinuityState? ActiveContinuity { get; set; }

    // --- NEW for Phase 2 ---
    /// <summary>Most recent compressed memory summaries for the focal character,
    /// oldest-relevant-first, capped to a handful of entries so this stays cheap.</summary>
    public List<string> RecentMemorySummaries { get; set; } = [];

    /// <summary>Compact digest of long-term world history, one string per
    /// compacted epoch, most recent last.</summary>
    public List<string> HistoryDigest { get; set; } = [];
}

public sealed class RpAvailableAction
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public CharacterAction Action { get; set; } = new();
}

public sealed class TileSummary
{
    public Vec3Int Position { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsPassable { get; set; }
    public List<string> OccupantNames { get; set; } = [];
}

public sealed class CharSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string Mood { get; set; } = string.Empty;
    public string VisibleCondition { get; set; } = string.Empty;
    public int DispositionToFocal { get; set; }
    public RpRelationshipType RelationshipType { get; set; } = RpRelationshipType.Unknown;
    public int Trust { get; set; }
    public int Fear { get; set; }
    public int Dependency { get; set; }
    public int Loyalty { get; set; }
    public int Manipulation { get; set; }
    public int Suspicion { get; set; }
    public List<string> KnownSecrets { get; set; } = [];
    public List<string> SharedHistory { get; set; } = [];
}

public sealed class LlmActionResponse
{
    public string? Thoughts { get; set; }
    public string? Note { get; set; }
    public string? Speech { get; set; }
    public List<CharacterAction> Actions { get; set; } = [];

    // --- NEW for Phase 1 ---
    public List<RpRelationshipDelta> RelationshipDeltas { get; set; } = [];
    public List<RpSecretDisclosure> SecretsLearned { get; set; } = [];
    public RpContinuityUpdate? ContinuityUpdate { get; set; }
    public RpSceneUpdate? SceneUpdate { get; set; }
}

public sealed class RpAuthoringDraftResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public RpImportSafetyState SafetyState { get; set; } = RpImportSafetyState.Unreviewed;
    /// <summary>Keys are editor ObservableProperty names (e.g. "FactionPublicDescription"),
    /// values are the drafted text ready to assign directly to that property.</summary>
    public Dictionary<string, string> Fields { get; set; } = [];
}
