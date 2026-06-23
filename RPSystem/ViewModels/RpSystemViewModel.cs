using System.Collections.ObjectModel;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;

namespace ChemCalculationAndManagementApp.ViewModels;

public partial class RpSystemViewModel : ObservableObject
{
    private const string PreviousDefaultRpModelId = "google/gemma-4-31B-turbo-TEE";
    private const string DefaultRpModelId = "Nemotron-3-Nano-Omni-30B-TEE";
    private const string DefaultRpModelName = "Nemotron 3 Nano Omni 30B TEE";
    private static readonly List<Services.AiModel> CachedRpModels = [];
    private static bool hasLoadedRpModelList;
    private readonly RpSimulationService _simulationService;
    private readonly AiModelService _modelService;
    private readonly SettingsService _settingsService;
    private readonly RpWorldSaveService _worldSaveService;
    private readonly RpWorldInspectionService _inspectionService;
    private readonly RpInventoryService _inventoryService;
    private readonly RpMarkdownImportService _markdownImportService;
    private readonly RpInteractionService _interactionService;
    private readonly RpAbilityService _abilityService;
    private readonly RpCaveMapGenerator _caveMapGenerator;
    private readonly RpMapRenderProjectionService _mapRenderProjectionService;
    private CancellationTokenSource? _tickCts;
    private bool _isLoadingWorldContextEditor;
    private bool _isLoadingContextCharacterEditor;
    private bool _isLoadingContextModuleEditor;
    private bool _isLoadingSpeciesTemplateEditor;
    private bool _isLoadingFactionProfileEditor;
    private bool _isLoadingAbilityEditor;
    private bool _isLoadingRelationshipRuleEditor;
    private bool _isLoadingSceneEnvironmentContinuityEditor;

    [ObservableProperty] private World world = RpWorldFactory.CreateStarterWorld();
    [ObservableProperty] private Character? selectedCharacter;
    [ObservableProperty] private Character? playerCharacter;
    [ObservableProperty] private Services.AiModel? selectedModel;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool useLlm = Preferences.Get("RpUseLlm", false);
    [ObservableProperty] private RpSliceMode sliceMode;
    [ObservableProperty] private int sliceCoordinate;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private string playerInput = string.Empty;
    [ObservableProperty] private string selectedCharacterSummary = string.Empty;
    [ObservableProperty] private string selectedTileInfo = "Click a tile to inspect it.";
    [ObservableProperty] private RpViewMode activeRpViewMode;
    [ObservableProperty] private RpMapActionMode mapActionMode = RpMapActionMode.Look;
    [ObservableProperty] private Vec3Int? selectedTilePosition;
    [ObservableProperty] private Item? selectedGroundItem;
    [ObservableProperty] private Item? selectedInventoryItem;
    [ObservableProperty] private RpWorldContextEntry? selectedWorldContext;
    [ObservableProperty] private RpContextModule? selectedContextModule;
    [ObservableProperty] private RpWorldContextCharacter? selectedContextCharacter;
    [ObservableProperty] private RpFactionProfile? selectedFactionProfile;
    [ObservableProperty] private string worldContextName = string.Empty;
    [ObservableProperty] private bool worldContextIsEnabled;
    [ObservableProperty] private string worldContextRulesText = string.Empty;
    [ObservableProperty] private string contextModuleName = string.Empty;
    [ObservableProperty] private bool contextModuleIsEnabled;
    [ObservableProperty] private string contextModuleType = RpContextModuleType.GeneralRules.ToString();
    [ObservableProperty] private string contextModuleVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private int contextModulePriority = 100;
    [ObservableProperty] private string contextModuleSourceLabel = string.Empty;
    [ObservableProperty] private string contextModuleAppliesTo = string.Empty;
    [ObservableProperty] private string contextModuleText = string.Empty;
    [ObservableProperty] private string contextCharacterName = string.Empty;
    [ObservableProperty] private bool contextCharacterIsNamed;
    [ObservableProperty] private string contextCharacterVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private string contextCharacterAppliesTo = string.Empty;
    [ObservableProperty] private string contextCharacterArchetype = "regular";
    [ObservableProperty] private string contextCharacterRace = "Human";
    [ObservableProperty] private string contextCharacterBodyType = BodyTypeKind.Human.ToString();
    [ObservableProperty] private string contextCharacterFactionId = string.Empty;
    [ObservableProperty] private string contextCharacterRole = string.Empty;
    [ObservableProperty] private string contextCharacterPersonality = string.Empty;
    [ObservableProperty] private string contextCharacterStory = string.Empty;
    [ObservableProperty] private string contextCharacterAbilities = string.Empty;
    [ObservableProperty] private string contextCharacterGoal = "Wait and observe.";
    [ObservableProperty] private string contextCharacterLifeGoal = string.Empty;
    [ObservableProperty] private string contextCharacterTags = "creature, sapient";
    [ObservableProperty] private string contextCharacterSpeechStyle = string.Empty;
    [ObservableProperty] private string contextCharacterFirstEncounter = string.Empty;
    [ObservableProperty] private string contextCharacterNegotiation = string.Empty;
    [ObservableProperty] private string contextCharacterEscalation = string.Empty;
    [ObservableProperty] private string contextCharacterDeEscalation = string.Empty;
    [ObservableProperty] private string contextCharacterRelationshipHandling = string.Empty;
    [ObservableProperty] private string contextCharacterDeception = string.Empty;
    [ObservableProperty] private string contextCharacterCombatPreferences = string.Empty;
    [ObservableProperty] private string contextCharacterCapturePreferences = string.Empty;
    [ObservableProperty] private string factionId = string.Empty;
    [ObservableProperty] private string factionName = string.Empty;
    [ObservableProperty] private bool factionIsEnabled;
    [ObservableProperty] private string factionVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private string factionAppliesTo = string.Empty;
    [ObservableProperty] private string factionParentSpecies = string.Empty;
    [ObservableProperty] private string factionPublicDescription = string.Empty;
    [ObservableProperty] private string factionHiddenDoctrine = string.Empty;
    [ObservableProperty] private string factionCulture = string.Empty;
    [ObservableProperty] private string factionHierarchy = string.Empty;
    [ObservableProperty] private string factionGoals = string.Empty;
    [ObservableProperty] private string factionTaboos = string.Empty;
    [ObservableProperty] private string factionOutsiderBehavior = string.Empty;
    [ObservableProperty] private string factionMemberBehavior = string.Empty;
    [ObservableProperty] private string factionAppearance = string.Empty;
    [ObservableProperty] private string factionAnatomyOverrides = string.Empty;
    [ObservableProperty] private string factionMagicRules = string.Empty;
    [ObservableProperty] private string factionResourceRules = string.Empty;
    [ObservableProperty] private string factionTags = string.Empty;
    [ObservableProperty] private string factionRolesText = string.Empty;
    [ObservableProperty] private string factionAbilitiesText = string.Empty;
    [ObservableProperty] private string factionRelationshipRulesText = string.Empty;
    [ObservableProperty] private RpSpeciesTemplate? selectedSpeciesTemplate;
    [ObservableProperty] private string speciesTemplateName = string.Empty;
    [ObservableProperty] private string speciesTemplateRace = string.Empty;
    [ObservableProperty] private string speciesTemplateBodyType = BodyTypeKind.Human.ToString();
    [ObservableProperty] private string speciesBodyLanguageText = string.Empty;
    [ObservableProperty] private string speciesVocalizationsText = string.Empty;
    [ObservableProperty] private string speciesDietRules = string.Empty;
    [ObservableProperty] private string speciesEnergyRules = string.Empty;
    [ObservableProperty] private string speciesMagicRules = string.Empty;
    [ObservableProperty] private string speciesAnatomyModifiersText = string.Empty;
    [ObservableProperty] private string speciesTagsText = string.Empty;
    [ObservableProperty] private RpAbility? selectedContextAbility;
    [ObservableProperty] private string contextAbilityId = string.Empty;
    [ObservableProperty] private string contextAbilityName = string.Empty;
    [ObservableProperty] private string contextAbilityDescription = string.Empty;
    [ObservableProperty] private string contextAbilityTargetKind = RpAbilityTargetKind.Tile.ToString();
    [ObservableProperty] private string contextAbilityDamageType = RpDamageType.Physical.ToString();
    [ObservableProperty] private string contextAbilityResource = RpAbilityResource.None.ToString();
    [ObservableProperty] private float contextAbilityManaCost;
    [ObservableProperty] private float contextAbilityFocusCost;
    [ObservableProperty] private float contextAbilityStaminaCost;
    [ObservableProperty] private float contextAbilityDamage;
    [ObservableProperty] private int contextAbilityRange = 1;
    [ObservableProperty] private int contextAbilityTickCost = 1;
    [ObservableProperty] private int contextAbilityCooldownTicks;
    [ObservableProperty] private string contextAbilityRangeText = string.Empty;
    [ObservableProperty] private string contextAbilityTargetRules = string.Empty;
    [ObservableProperty] private string contextAbilityConstraints = string.Empty;
    [ObservableProperty] private string contextAbilityWorldEffect = string.Empty;
    [ObservableProperty] private string contextAbilityNarrativeEffect = string.Empty;
    [ObservableProperty] private string contextAbilityAllowedUsage = string.Empty;
    [ObservableProperty] private string contextAbilityForbiddenUsage = string.Empty;
    [ObservableProperty] private string contextAbilityTagsText = string.Empty;
    [ObservableProperty] private RpRelationshipRule? selectedRelationshipRule;
    [ObservableProperty] private string relationshipTargetNameOrTag = string.Empty;
    [ObservableProperty] private string relationshipType = RpRelationshipType.Unknown.ToString();
    [ObservableProperty] private int relationshipTrust;
    [ObservableProperty] private int relationshipFear;
    [ObservableProperty] private int relationshipDependency;
    [ObservableProperty] private int relationshipLoyalty;
    [ObservableProperty] private int relationshipManipulation;
    [ObservableProperty] private int relationshipSuspicion;
    [ObservableProperty] private string relationshipKnownSecretsText = string.Empty;
    [ObservableProperty] private string relationshipHandlingRules = string.Empty;
    [ObservableProperty] private string scenePhase = RpScenePhase.Setup.ToString();
    [ObservableProperty] private float sceneEscalationBudget = 1;
    [ObservableProperty] private float sceneEscalationRate = 0.1f;
    [ObservableProperty] private string sceneActiveThreadsText = string.Empty;
    [ObservableProperty] private string sceneForeshadowedText = string.Empty;
    [ObservableProperty] private string sceneUnresolvedPromisesText = string.Empty;
    [ObservableProperty] private string sceneMajorPrerequisitesText = string.Empty;
    [ObservableProperty] private string environmentInteractiveObjectsText = string.Empty;
    [ObservableProperty] private string environmentTellsText = string.Empty;
    [ObservableProperty] private string environmentHazardsText = string.Empty;
    [ObservableProperty] private string environmentTerrainAffordancesText = string.Empty;
    [ObservableProperty] private string environmentCluesText = string.Empty;
    [ObservableProperty] private string environmentDomainOwnerRulesText = string.Empty;
    [ObservableProperty] private string continuityPhysicalChangesText = string.Empty;
    [ObservableProperty] private string continuityEmotionalChangesText = string.Empty;
    [ObservableProperty] private string continuityRelationshipChangesText = string.Empty;
    [ObservableProperty] private string continuityFlagsText = string.Empty;
    [ObservableProperty] private string continuityTriggersText = string.Empty;
    [ObservableProperty] private string continuityIrreversibleEventsText = string.Empty;
    [ObservableProperty] private string continuityPendingConsequencesText = string.Empty;
    [ObservableProperty] private string inventorySummary = "Inventory: empty";
    [ObservableProperty] private string moveNorthKey = Preferences.Get("RpMoveNorthKey", "W");
    [ObservableProperty] private string moveSouthKey = Preferences.Get("RpMoveSouthKey", "S");
    [ObservableProperty] private string moveWestKey = Preferences.Get("RpMoveWestKey", "A");
    [ObservableProperty] private string moveEastKey = Preferences.Get("RpMoveEastKey", "D");
    [ObservableProperty] private string moveUpKey = Preferences.Get("RpMoveUpKey", "PageUp");
    [ObservableProperty] private string moveDownKey = Preferences.Get("RpMoveDownKey", "PageDown");
    [ObservableProperty] private string waitKey = Preferences.Get("RpWaitKey", "Space");
    [ObservableProperty] private string lookModeKey = Preferences.Get("RpLookModeKey", "L");
    [ObservableProperty] private string moveModeKey = Preferences.Get("RpMoveModeKey", "M");
    [ObservableProperty] private int openSpaceLookDepth = Preferences.Get("RpOpenSpaceLookDepth", 5);

    public ObservableCollection<Services.AiModel> AvailableModels { get; } = [];
    public ObservableCollection<Character> Characters { get; } = [];
    public ObservableCollection<string> EventLog { get; } = [];
    public ObservableCollection<Item> GroundItems { get; } = [];
    public ObservableCollection<Item> InventoryItems { get; } = [];
    public ObservableCollection<RpWorldContextEntry> WorldContexts { get; } = [];
    public ObservableCollection<RpContextModule> ContextModules { get; } = [];
    public ObservableCollection<RpWorldContextCharacter> ContextCharacters { get; } = [];
    public ObservableCollection<RpFactionProfile> FactionProfiles { get; } = [];
    public ObservableCollection<RpSpeciesTemplate> SpeciesTemplates { get; } = [];
    public ObservableCollection<RpAbility> ContextAbilities { get; } = [];
    public ObservableCollection<RpRelationshipRule> RelationshipRules { get; } = [];
    public IReadOnlyList<string> BodyTypeOptions { get; } = Enum.GetNames<BodyTypeKind>();
    public IReadOnlyList<string> ContextModuleTypeOptions { get; } = Enum.GetNames<RpContextModuleType>();
    public IReadOnlyList<string> ContextModuleVisibilityOptions { get; } = Enum.GetNames<RpContextVisibility>();
    public IReadOnlyList<string> AbilityTargetKindOptions { get; } = Enum.GetNames<RpAbilityTargetKind>();
    public IReadOnlyList<string> AbilityDamageTypeOptions { get; } = Enum.GetNames<RpDamageType>();
    public IReadOnlyList<string> AbilityResourceOptions { get; } = Enum.GetNames<RpAbilityResource>();
    public IReadOnlyList<string> RelationshipTypeOptions { get; } = Enum.GetNames<RpRelationshipType>();
    public IReadOnlyList<string> ScenePhaseOptions { get; } = Enum.GetNames<RpScenePhase>();

    public string WorldTitle => $"{World.Name} - {World.Clock.Display}";
    public string SliceModeText => SliceMode == RpSliceMode.Horizontal ? "Horizontal X/Z" : "Vertical X/Y";
    public string SliceCoordinateText => SliceMode == RpSliceMode.Horizontal ? $"Y {SliceCoordinate}" : $"Z {SliceCoordinate}";
    public string LlmStateText => UseLlm ? "LLM on" : "Local wait";
    public string PlayerCharacterText => PlayerCharacter == null ? "No player" : $"Player: {PlayerCharacter.Name}";
    public string PlayerHealthText => PlayerCharacter == null ? "HP -/-" : $"HP {PlayerCharacter.Vitals.HealthCurrent:0.#}/{PlayerCharacter.Vitals.HealthMax:0.#}";
    public string PlayerManaText => PlayerCharacter == null ? "Mana -/-" : $"Mana {PlayerCharacter.Vitals.ManaCurrent:0.#}/{PlayerCharacter.Vitals.ManaMax:0.#}";
    public string PlayerFocusText => PlayerCharacter == null ? "Focus -/-" : $"Focus {PlayerCharacter.Vitals.FocusCurrent:0.#}/{PlayerCharacter.Vitals.FocusMax:0.#}";
    public string PlayerStaminaText => PlayerCharacter == null ? "Stamina -/-" : $"Stamina {PlayerCharacter.Vitals.StaminaCurrent:0.#}/{PlayerCharacter.Vitals.StaminaMax:0.#}";
    public double PlayerHealthProgress => Ratio(PlayerCharacter?.Vitals.HealthCurrent, PlayerCharacter?.Vitals.HealthMax);
    public double PlayerManaProgress => Ratio(PlayerCharacter?.Vitals.ManaCurrent, PlayerCharacter?.Vitals.ManaMax);
    public double PlayerFocusProgress => Ratio(PlayerCharacter?.Vitals.FocusCurrent, PlayerCharacter?.Vitals.FocusMax);
    public double PlayerStaminaProgress => Ratio(PlayerCharacter?.Vitals.StaminaCurrent, PlayerCharacter?.Vitals.StaminaMax);
    public bool IsMainMenuView => ActiveRpViewMode == RpViewMode.MainMenu;
    public bool IsWorldView => ActiveRpViewMode == RpViewMode.World;
    public bool IsSettingsView => ActiveRpViewMode == RpViewMode.Settings;
    public bool IsWorldContextView => ActiveRpViewMode == RpViewMode.WorldContext;
    public bool IsGameOptionsView => ActiveRpViewMode == RpViewMode.GameOptions;
    public bool IsDevMenuView => ActiveRpViewMode == RpViewMode.DevMenu;
    public bool IsTestMapsView => ActiveRpViewMode == RpViewMode.TestMaps;
    public string ActiveViewText => ActiveRpViewMode switch
    {
        RpViewMode.MainMenu => "Main Menu",
        RpViewMode.Settings => "Settings",
        RpViewMode.WorldContext => "World Context",
        RpViewMode.GameOptions => "Game Options",
        RpViewMode.DevMenu => "Developer Menu",
        RpViewMode.TestMaps => "Test Maps",
        _ => "World"
    };
    public string MapModeText => MapActionMode.ToString();
    public string ActiveWorldContextSummary => $"{World.WorldContexts.Count(context => context.IsEnabled)} active / {World.WorldContexts.Count} total";
    public string ActiveContextModuleSummary => SelectedWorldContext == null
        ? "No context selected"
        : $"{SelectedWorldContext.Modules.Count(module => module.IsEnabled)} active module(s) / {SelectedWorldContext.Modules.Count} total";
    public string ActiveContextCharacterSummary => SelectedWorldContext == null
        ? "No context selected"
        : $"{SelectedWorldContext.Characters.Count} character profile(s)";
    public string ActiveFactionProfileSummary
    {
        get
        {
            if (SelectedWorldContext == null)
            {
                return "No context selected";
            }

            var factions = SelectedWorldContext.Factions ?? [];
            return $"{factions.Count(faction => faction.IsEnabled)} active faction(s) / {factions.Count} total";
        }
    }
    public string ActiveSpeciesTemplateSummary => SelectedWorldContext == null
        ? "No context selected"
        : $"{SelectedWorldContext.SpeciesTemplates.Count} species template(s)";
    public string ActiveAbilitySummary => SelectedContextCharacter == null
        ? "No character profile selected"
        : $"{SelectedContextCharacter.StructuredAbilities.Count} structured ability/abilities";
    public string ActiveRelationshipRuleSummary => SelectedContextCharacter == null
        ? "No character profile selected"
        : $"{SelectedContextCharacter.RelationshipRules.Count} relationship rule(s)";

    public RpSystemViewModel(
        RpSimulationService simulationService,
        AiModelService modelService,
        SettingsService settingsService,
        RpWorldSaveService worldSaveService,
        RpWorldInspectionService inspectionService,
        RpInventoryService inventoryService,
        RpMarkdownImportService markdownImportService,
        RpInteractionService interactionService,
        RpAbilityService abilityService,
        RpCaveMapGenerator caveMapGenerator,
        RpMapRenderProjectionService mapRenderProjectionService)
    {
        _simulationService = simulationService;
        _modelService = modelService;
        _settingsService = settingsService;
        _worldSaveService = worldSaveService;
        _inspectionService = inspectionService;
        _inventoryService = inventoryService;
        _markdownImportService = markdownImportService;
        _interactionService = interactionService;
        _abilityService = abilityService;
        _caveMapGenerator = caveMapGenerator;
        _mapRenderProjectionService = mapRenderProjectionService;
        RpSimulationService.AppendDebugLog($"RP settings loaded | useLlm={UseLlm} | storedRpUseLlm={Preferences.Get("RpUseLlm", false)}");
        SeedDefaultModel();
        ResetCollections();
        _ = LoadSavedWorldOnStartupAsync();
    }

    partial void OnWorldChanged(World value)
    {
        ResetCollections();
        RaiseWorldChanged();
    }

    partial void OnSelectedCharacterChanged(Character? value)
    {
        UpdateSelectedCharacterSummary();
    }

    partial void OnPlayerCharacterChanged(Character? value)
    {
        OnPropertyChanged(nameof(PlayerCharacterText));
        RaisePlayerStatsChanged();
        RefreshInventory();
        RaiseWorldChanged();
    }

    partial void OnSliceModeChanged(RpSliceMode value)
    {
        OnPropertyChanged(nameof(SliceModeText));
        OnPropertyChanged(nameof(SliceCoordinateText));
        RaiseWorldChanged();
    }

    partial void OnSliceCoordinateChanged(int value)
    {
        OnPropertyChanged(nameof(SliceCoordinateText));
        RaiseWorldChanged();
    }

    partial void OnUseLlmChanged(bool value)
    {
        Preferences.Set("RpUseLlm", value);
        RpSimulationService.AppendDebugLog($"RP settings toggle changed | useLlm={value}");
        OnPropertyChanged(nameof(LlmStateText));
        StatusText = value
            ? "RP LLM turns enabled."
            : "RP LLM turns disabled. NPCs will wait locally.";
    }

    partial void OnActiveRpViewModeChanged(RpViewMode value)
    {
        OnPropertyChanged(nameof(IsMainMenuView));
        OnPropertyChanged(nameof(IsWorldView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsWorldContextView));
        OnPropertyChanged(nameof(IsGameOptionsView));
        OnPropertyChanged(nameof(IsDevMenuView));
        OnPropertyChanged(nameof(IsTestMapsView));
        OnPropertyChanged(nameof(ActiveViewText));
    }

    partial void OnMapActionModeChanged(RpMapActionMode value)
    {
        OnPropertyChanged(nameof(MapModeText));
    }

    partial void OnMoveNorthKeyChanged(string value) => Preferences.Set("RpMoveNorthKey", NormalizeKeyName(value, "W"));
    partial void OnMoveSouthKeyChanged(string value) => Preferences.Set("RpMoveSouthKey", NormalizeKeyName(value, "S"));
    partial void OnMoveWestKeyChanged(string value) => Preferences.Set("RpMoveWestKey", NormalizeKeyName(value, "A"));
    partial void OnMoveEastKeyChanged(string value) => Preferences.Set("RpMoveEastKey", NormalizeKeyName(value, "D"));
    partial void OnMoveUpKeyChanged(string value) => Preferences.Set("RpMoveUpKey", NormalizeKeyName(value, "PageUp"));
    partial void OnMoveDownKeyChanged(string value) => Preferences.Set("RpMoveDownKey", NormalizeKeyName(value, "PageDown"));
    partial void OnWaitKeyChanged(string value) => Preferences.Set("RpWaitKey", NormalizeKeyName(value, "Space"));
    partial void OnLookModeKeyChanged(string value) => Preferences.Set("RpLookModeKey", NormalizeKeyName(value, "L"));
    partial void OnMoveModeKeyChanged(string value) => Preferences.Set("RpMoveModeKey", NormalizeKeyName(value, "M"));
    partial void OnOpenSpaceLookDepthChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 20);
        if (clamped != value)
        {
            OpenSpaceLookDepth = clamped;
            return;
        }

        Preferences.Set("RpOpenSpaceLookDepth", clamped);
        RaiseWorldChanged();
    }

    partial void OnSelectedTilePositionChanged(Vec3Int? value)
    {
        RefreshGroundItems();
    }

    partial void OnSelectedWorldContextChanged(RpWorldContextEntry? value)
    {
        LoadWorldContextEditor(value);
        RefreshContextModules();
        RefreshContextCharacters();
        RefreshSpeciesTemplates();
        RefreshFactionProfiles();
        LoadSceneEnvironmentContinuityEditor(value);
        OnPropertyChanged(nameof(ActiveWorldContextSummary));
        OnPropertyChanged(nameof(ActiveContextModuleSummary));
        OnPropertyChanged(nameof(ActiveContextCharacterSummary));
        OnPropertyChanged(nameof(ActiveSpeciesTemplateSummary));
        OnPropertyChanged(nameof(ActiveFactionProfileSummary));
    }

    partial void OnSelectedContextModuleChanged(RpContextModule? value)
    {
        LoadContextModuleEditor(value);
        OnPropertyChanged(nameof(ActiveContextModuleSummary));
    }

    partial void OnSelectedContextCharacterChanged(RpWorldContextCharacter? value)
    {
        LoadContextCharacterEditor(value);
        RefreshContextAbilities();
        RefreshRelationshipRules();
        OnPropertyChanged(nameof(ActiveContextCharacterSummary));
        OnPropertyChanged(nameof(ActiveAbilitySummary));
        OnPropertyChanged(nameof(ActiveRelationshipRuleSummary));
    }

    partial void OnSelectedFactionProfileChanged(RpFactionProfile? value)
    {
        LoadFactionProfileEditor(value);
        OnPropertyChanged(nameof(ActiveFactionProfileSummary));
    }

    partial void OnSelectedSpeciesTemplateChanged(RpSpeciesTemplate? value)
    {
        LoadSpeciesTemplateEditor(value);
        OnPropertyChanged(nameof(ActiveSpeciesTemplateSummary));
    }

    partial void OnSelectedContextAbilityChanged(RpAbility? value)
    {
        LoadContextAbilityEditor(value);
        OnPropertyChanged(nameof(ActiveAbilitySummary));
    }

    partial void OnSelectedRelationshipRuleChanged(RpRelationshipRule? value)
    {
        LoadRelationshipRuleEditor(value);
        OnPropertyChanged(nameof(ActiveRelationshipRuleSummary));
    }

    partial void OnWorldContextNameChanged(string value)
    {
        if (_isLoadingWorldContextEditor || SelectedWorldContext == null)
        {
            return;
        }

        SelectedWorldContext.Name = string.IsNullOrWhiteSpace(value) ? "World Context" : value.Trim();
        RefreshWorldContextPicker();
    }

    partial void OnWorldContextIsEnabledChanged(bool value)
    {
        if (_isLoadingWorldContextEditor || SelectedWorldContext == null)
        {
            return;
        }

        SelectedWorldContext.IsEnabled = value;
        RefreshWorldContextPicker();
    }

    partial void OnWorldContextRulesTextChanged(string value)
    {
        if (_isLoadingWorldContextEditor || SelectedWorldContext == null)
        {
            return;
        }

        SelectedWorldContext.RulesText = value;
    }

    partial void OnContextModuleNameChanged(string value) => ApplyContextModuleEditor();
    partial void OnContextModuleIsEnabledChanged(bool value) => ApplyContextModuleEditor();
    partial void OnContextModuleTypeChanged(string value) => ApplyContextModuleEditor();
    partial void OnContextModuleVisibilityChanged(string value) => ApplyContextModuleEditor();
    partial void OnContextModulePriorityChanged(int value) => ApplyContextModuleEditor();
    partial void OnContextModuleSourceLabelChanged(string value) => ApplyContextModuleEditor();
    partial void OnContextModuleAppliesToChanged(string value) => ApplyContextModuleEditor();
    partial void OnContextModuleTextChanged(string value) => ApplyContextModuleEditor();

    partial void OnContextCharacterNameChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterIsNamedChanged(bool value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterVisibilityChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterAppliesToChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterArchetypeChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterRaceChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterBodyTypeChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterFactionIdChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterRoleChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterPersonalityChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterStoryChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterAbilitiesChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterGoalChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterLifeGoalChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterTagsChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterSpeechStyleChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterFirstEncounterChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterNegotiationChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterEscalationChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterDeEscalationChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterRelationshipHandlingChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterDeceptionChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterCombatPreferencesChanged(string value) => ApplyContextCharacterEditor();
    partial void OnContextCharacterCapturePreferencesChanged(string value) => ApplyContextCharacterEditor();
    partial void OnFactionIdChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionNameChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionIsEnabledChanged(bool value) => ApplyFactionProfileEditor();
    partial void OnFactionVisibilityChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionAppliesToChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionParentSpeciesChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionPublicDescriptionChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionHiddenDoctrineChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionCultureChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionHierarchyChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionGoalsChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionTaboosChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionOutsiderBehaviorChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionMemberBehaviorChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionAppearanceChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionAnatomyOverridesChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionMagicRulesChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionResourceRulesChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionTagsChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionRolesTextChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionAbilitiesTextChanged(string value) => ApplyFactionProfileEditor();
    partial void OnFactionRelationshipRulesTextChanged(string value) => ApplyFactionProfileEditor();
    partial void OnSpeciesTemplateNameChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesTemplateRaceChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesTemplateBodyTypeChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesBodyLanguageTextChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesVocalizationsTextChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesDietRulesChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesEnergyRulesChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesMagicRulesChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesAnatomyModifiersTextChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnSpeciesTagsTextChanged(string value) => ApplySpeciesTemplateEditor();
    partial void OnContextAbilityIdChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityNameChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityDescriptionChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityTargetKindChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityDamageTypeChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityResourceChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityManaCostChanged(float value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityFocusCostChanged(float value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityStaminaCostChanged(float value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityDamageChanged(float value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityRangeChanged(int value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityTickCostChanged(int value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityCooldownTicksChanged(int value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityRangeTextChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityTargetRulesChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityConstraintsChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityWorldEffectChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityNarrativeEffectChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityAllowedUsageChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityForbiddenUsageChanged(string value) => ApplyContextAbilityEditor();
    partial void OnContextAbilityTagsTextChanged(string value) => ApplyContextAbilityEditor();
    partial void OnRelationshipTargetNameOrTagChanged(string value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipTypeChanged(string value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipTrustChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipFearChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipDependencyChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipLoyaltyChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipManipulationChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipSuspicionChanged(int value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipKnownSecretsTextChanged(string value) => ApplyRelationshipRuleEditor();
    partial void OnRelationshipHandlingRulesChanged(string value) => ApplyRelationshipRuleEditor();
    partial void OnScenePhaseChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneEscalationBudgetChanged(float value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneEscalationRateChanged(float value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneActiveThreadsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneForeshadowedTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneUnresolvedPromisesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnSceneMajorPrerequisitesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentInteractiveObjectsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentTellsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentHazardsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentTerrainAffordancesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentCluesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnEnvironmentDomainOwnerRulesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityPhysicalChangesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityEmotionalChangesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityRelationshipChangesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityFlagsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityTriggersTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityIrreversibleEventsTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();
    partial void OnContinuityPendingConsequencesTextChanged(string value) => ApplySceneEnvironmentContinuityEditor();

    [RelayCommand]
    public async Task LoadModels()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            StatusText = hasLoadedRpModelList && CachedRpModels.Count > 0
                ? "Refreshing models from service cache..."
                : "Loading models...";
            var models = await _modelService.GetModelsAsync();
            CachedRpModels.Clear();
            CachedRpModels.AddRange(EnsureDefaultModel(models));
            hasLoadedRpModelList = true;
            ApplyModelList(CachedRpModels);
            StatusText = AvailableModels.Count == 0 ? "No models loaded. Check Settings." : $"Loaded {AvailableModels.Count} models.";
        }
        catch (Exception ex)
        {
            StatusText = $"Model load failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task StepTick()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        _tickCts?.Cancel();
        _tickCts = new CancellationTokenSource();

        try
        {
            var events = await AdvanceWorldTickCoreAsync(_tickCts.Token);
            StatusText = $"Tick {World.Clock.TickCount}. {events.Count} event(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Tick cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Tick failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<IReadOnlyList<NarrativeEvent>> AdvanceWorldTickCoreAsync(CancellationToken cancellationToken)
    {
        EnsureDefaultModelSelected();
        var events = await _simulationService.TickAsync(
            World,
            UseLlm,
            _settingsService.AiProvider,
            GetCurrentProviderApiKey(),
            SelectedModel?.Id ?? SelectedModel?.Name ?? string.Empty,
            PlayerCharacter?.Id,
            cancellationToken);

        foreach (var evt in events)
        {
            EventLog.Insert(0, FormatEvent(evt));
        }

        TrimEventLog();
        RaiseWorldChanged();
        return events;
    }

    private async Task AdvanceWorldAfterPlayerActionAsync(string actionStatus)
    {
        if (IsBusy)
        {
            StatusText = actionStatus;
            return;
        }

        IsBusy = true;
        _tickCts?.Cancel();
        _tickCts = new CancellationTokenSource();
        try
        {
            var events = await AdvanceWorldTickCoreAsync(_tickCts.Token);
            StatusText = $"{actionStatus} Tick {World.Clock.TickCount} advanced by {World.Clock.SecondsPerTick} seconds. {events.Count} event(s).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Tick cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Action tick failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task RunFiveTicks()
    {
        for (int i = 0; i < 5; i++)
        {
            if (IsBusy && i > 0)
            {
                return;
            }

            await StepTick();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _tickCts?.Cancel();
    }

    [RelayCommand]
    public void ResetWorld()
    {
        _tickCts?.Cancel();
        World = RpWorldFactory.CreateStarterWorld();
        SliceMode = RpSliceMode.Horizontal;
        SliceCoordinate = 0;
        StatusText = "Starter world reset.";
    }

    [RelayCommand]
    public async Task SaveWorld()
    {
        try
        {
            await _worldSaveService.SaveAsync(World);
            StatusText = $"World saved: {_worldSaveService.DefaultSavePath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task LoadWorld()
    {
        try
        {
            var loadedWorld = await _worldSaveService.LoadAsync();
            if (loadedWorld == null)
            {
                StatusText = "No saved RP world found.";
                return;
            }

            World = loadedWorld;
            StatusText = $"World loaded: {_worldSaveService.DefaultSavePath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
        }
    }

    private async Task LoadSavedWorldOnStartupAsync()
    {
        try
        {
            var loadedWorld = await _worldSaveService.LoadAsync();
            if (loadedWorld == null)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                World = loadedWorld;
                StatusText = $"World loaded: {_worldSaveService.DefaultSavePath}";
                RpSimulationService.AppendDebugLog("RP world auto-loaded on startup.");
            });
        }
        catch (Exception ex)
        {
            RpSimulationService.AppendDebugLog($"RP world auto-load failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ImportMarkdown()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Import RP markdown",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".md", ".txt" } },
                    { DevicePlatform.Android, new[] { "text/*" } },
                    { DevicePlatform.iOS, new[] { "public.plain-text", "net.daringfireball.markdown" } },
                    { DevicePlatform.MacCatalyst, new[] { "public.plain-text", "net.daringfireball.markdown" } }
                })
            });

            if (result?.FullPath == null)
            {
                return;
            }

            var content = await File.ReadAllTextAsync(result.FullPath);
            var import = _markdownImportService.ImportIntoWorld(
                World,
                Path.GetFileNameWithoutExtension(result.FileName),
                content,
                PlayerCharacter?.Position ?? new Vec3Int(1, 0, 0));
            if (import.ImportedCharacter != null)
            {
                Characters.Add(import.ImportedCharacter);
                SelectedCharacter = import.ImportedCharacter;
            }

            if (import.ImportedContext != null)
            {
                EnsureWorldContextModules(import.ImportedContext);
                WorldContexts.Add(import.ImportedContext);
                SelectedWorldContext = import.ImportedContext;
            }

            StatusText = import.Message;
            RaiseWorldChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ResetControls()
    {
        MoveNorthKey = "W";
        MoveSouthKey = "S";
        MoveWestKey = "A";
        MoveEastKey = "D";
        MoveUpKey = "PageUp";
        MoveDownKey = "PageDown";
        WaitKey = "Space";
        LookModeKey = "L";
        MoveModeKey = "M";
        StatusText = "RP controls reset to defaults.";
    }

    [RelayCommand]
    public void SetSelectedAsPlayer()
    {
        if (SelectedCharacter == null)
        {
            StatusText = "Select a character first.";
            return;
        }

        PlayerCharacter = SelectedCharacter;
        StatusText = $"{PlayerCharacter.Name} is now the player character.";
    }

    [RelayCommand]
    public Task MovePlayerNorth() => MovePlayer(Direction.North);

    [RelayCommand]
    public Task MovePlayerSouth() => MovePlayer(Direction.South);

    [RelayCommand]
    public Task MovePlayerEast() => MovePlayer(Direction.East);

    [RelayCommand]
    public Task MovePlayerWest() => MovePlayer(Direction.West);

    [RelayCommand]
    public Task MovePlayerUp() => MovePlayer(Direction.Ceil);

    [RelayCommand]
    public Task MovePlayerDown() => MovePlayer(Direction.Floor);

    [RelayCommand]
    public void ShowMainMenu()
    {
        ActiveRpViewMode = RpViewMode.MainMenu;
    }

    [RelayCommand]
    public void ShowWorldView()
    {
        ActiveRpViewMode = RpViewMode.World;
    }

    [RelayCommand]
    public void ShowSettingsView()
    {
        ActiveRpViewMode = RpViewMode.Settings;
    }

    [RelayCommand]
    public void ShowWorldContextView()
    {
        ActiveRpViewMode = RpViewMode.WorldContext;
    }

    [RelayCommand]
    public void ShowGameOptionsView()
    {
        ActiveRpViewMode = RpViewMode.GameOptions;
    }

    [RelayCommand]
    public void ShowDevMenu()
    {
        ActiveRpViewMode = RpViewMode.DevMenu;
    }

    [RelayCommand]
    public void ShowTestMaps()
    {
        ActiveRpViewMode = RpViewMode.TestMaps;
    }

    [RelayCommand]
    public void LoadCavernStarterTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateCavernStarterWorld(), "Loaded starter cavern test map.");
    }

    [RelayCommand]
    public void LoadGlasshouseTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateGlasshouseOutpostWorld(), "Loaded glasshouse movement test map.");
    }

    [RelayCommand]
    public void LoadGeneratedRampCaveTestMap()
    {
        var result = _caveMapGenerator.Generate(new RpCaveMapGenerationOptions
        {
            Name = "Generated Ramp Cave",
            Width = 31,
            Depth = 31,
            Levels = 3,
            Seed = 1337,
            ConnectorKind = RpVerticalConnectorKind.Ramp
        });
        LoadTestWorld(result.World, $"Loaded generated ramp cave: {result.OpenTileCount} open tile(s), {result.VerticalConnectors.Count} connector(s).");
    }

    [RelayCommand]
    public void LoadGeneratedLadderCaveTestMap()
    {
        var result = _caveMapGenerator.Generate(new RpCaveMapGenerationOptions
        {
            Name = "Generated Ladder Cave",
            Width = 31,
            Depth = 31,
            Levels = 3,
            Seed = 2027,
            ConnectorKind = RpVerticalConnectorKind.Ladder
        });

        var player = result.World.Characters.Values.FirstOrDefault(character => character.RpTags.Contains("player", StringComparer.OrdinalIgnoreCase));
        if (player != null)
        {
            player.Movement.Modes = [RpMovementMode.Climb];
            if (!player.RpTags.Contains("climber", StringComparer.OrdinalIgnoreCase))
            {
                player.RpTags.Add("climber");
            }
        }

        LoadTestWorld(result.World, $"Loaded generated ladder cave: {result.OpenTileCount} open tile(s), {result.VerticalConnectors.Count} connector(s).");
    }

    [RelayCommand]
    public void LoadPathfindingStress10TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(10), "Loaded pathfinding stress map with 10 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadPathfindingStress50TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(50), "Loaded pathfinding stress map with 50 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadPathfindingStress100TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(100), "Loaded pathfinding stress map with 100 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadVerticalPathfindingTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateVerticalPathfindingTestWorld(), "Loaded 3-slice pathfinding map. Watch Y 0, Y 1, and Y 2 while ticking.");
    }

    [RelayCommand]
    public void LoadGlassAtriumFlightTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateGlassAtriumFlightTestWorld(), "Loaded glass atrium flight map. Center cells on upper slices are open space, not floors.");
    }

    private void LoadTestWorld(World testWorld, string status)
    {
        _tickCts?.Cancel();
        World = testWorld;
        SliceMode = RpSliceMode.Horizontal;
        SliceCoordinate = PlayerCharacter?.Position.Y ?? 0;
        MapActionMode = RpMapActionMode.Look;
        ActiveRpViewMode = RpViewMode.World;
        SelectedTilePosition = PlayerCharacter?.Position;
        StatusText = status;
    }

    [RelayCommand]
    public async Task AddWorldContext()
    {
        var context = new RpWorldContextEntry
        {
            Name = $"World Context {World.WorldContexts.Count + 1}",
            IsEnabled = true,
            RulesText = "Describe a global rule, setting constraint, tone instruction, or simulation assumption here."
        };
        World.WorldContexts.Add(context);
        WorldContexts.Add(context);
        SelectedWorldContext = context;
        RaiseWorldContextChanged();
        StatusText = $"Added world context: {context.Name}.";
        RpSimulationService.AppendDebugLog($"World context added | id={context.Id} | name={context.Name}");
        await AutoSaveWorldContextAsync("add");
    }

    [RelayCommand]
    public async Task DeleteSelectedWorldContext()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var removed = SelectedWorldContext;
        World.WorldContexts.Remove(removed);
        WorldContexts.Remove(removed);
        SelectedWorldContext = WorldContexts.FirstOrDefault();
        RaiseWorldContextChanged();
        StatusText = $"Deleted world context: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"World context deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("delete");
    }

    [RelayCommand]
    public async Task SaveWorldContextEdits()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        SelectedWorldContext.Name = string.IsNullOrWhiteSpace(WorldContextName)
            ? "World Context"
            : WorldContextName.Trim();
        SelectedWorldContext.IsEnabled = WorldContextIsEnabled;
        SelectedWorldContext.RulesText = WorldContextRulesText;
        RefreshWorldContextPicker();
        RaiseWorldContextChanged();
        StatusText = $"Saved world context: {SelectedWorldContext.Name}.";
        RpSimulationService.AppendDebugLog($"World context saved | id={SelectedWorldContext.Id} | name={SelectedWorldContext.Name} | enabled={SelectedWorldContext.IsEnabled} | rulesLength={SelectedWorldContext.RulesText?.Length ?? 0}");
        await AutoSaveWorldContextAsync("save");
    }

    [RelayCommand]
    public async Task AddContextModule()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var module = new RpContextModule
        {
            Name = $"Context Module {SelectedWorldContext.Modules.Count + 1}",
            IsEnabled = true,
            Type = RpContextModuleType.GeneralRules,
            Visibility = RpContextVisibility.WorldOnly,
            Priority = 100 + SelectedWorldContext.Modules.Count,
            Text = "Describe one focused rule module here."
        };
        SelectedWorldContext.Modules.Add(module);
        RefreshContextModules();
        SelectedContextModule = module;
        StatusText = $"Added context module: {module.Name}.";
        RpSimulationService.AppendDebugLog($"Context module added | context={SelectedWorldContext.Name} | id={module.Id} | name={module.Name}");
        await AutoSaveWorldContextAsync("module-add");
    }

    [RelayCommand]
    public async Task ScaffoldStandardContextSections()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var existingNames = SelectedWorldContext.Modules
            .Select(module => module.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var priority = SelectedWorldContext.Modules.Count == 0
            ? 100
            : SelectedWorldContext.Modules.Max(module => module.Priority) + 10;

        foreach (var template in StandardContextSectionTemplates())
        {
            if (existingNames.Contains(template.Name))
            {
                continue;
            }

            SelectedWorldContext.Modules.Add(new RpContextModule
            {
                Name = template.Name,
                IsEnabled = true,
                Type = template.Type,
                Visibility = template.Visibility,
                Priority = priority,
                SourceLabel = "Standard RP scaffold",
                Text = template.Text
            });
            priority += 10;
            added++;
        }

        RefreshContextModules();
        RaiseWorldContextChanged();
        StatusText = added == 0
            ? "Standard context sections already exist."
            : $"Added {added} standard context section(s).";
        RpSimulationService.AppendDebugLog($"Standard context sections scaffolded | context={SelectedWorldContext.Name} | added={added}");
        await AutoSaveWorldContextAsync("module-scaffold-standard");
    }

    [RelayCommand]
    public async Task DuplicateSelectedContextModule()
    {
        if (SelectedWorldContext == null || SelectedContextModule == null)
        {
            StatusText = "Select a context module first.";
            return;
        }

        var source = SelectedContextModule;
        var clone = new RpContextModule
        {
            Name = $"{source.Name} Copy",
            IsEnabled = source.IsEnabled,
            Type = source.Type,
            Visibility = source.Visibility,
            Priority = source.Priority + 1,
            SourceLabel = source.SourceLabel,
            AppliesTo = source.AppliesTo,
            Text = source.Text
        };
        SelectedWorldContext.Modules.Add(clone);
        RefreshContextModules();
        SelectedContextModule = clone;
        StatusText = $"Duplicated context module: {clone.Name}.";
        RpSimulationService.AppendDebugLog($"Context module duplicated | source={source.Id} | clone={clone.Id} | name={clone.Name}");
        await AutoSaveWorldContextAsync("module-duplicate");
    }

    [RelayCommand]
    public async Task CreateModuleFromLegacyContext()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(WorldContextRulesText))
        {
            StatusText = "Legacy context text is empty.";
            return;
        }

        var module = new RpContextModule
        {
            Name = $"{SelectedWorldContext.Name} Legacy Text",
            IsEnabled = true,
            Type = RpContextModuleType.ImportNotes,
            Visibility = RpContextVisibility.WorldOnly,
            Priority = SelectedWorldContext.Modules.Count == 0
                ? 100
                : SelectedWorldContext.Modules.Max(existing => existing.Priority) + 10,
            SourceLabel = "Legacy context text",
            Text = WorldContextRulesText
        };
        SelectedWorldContext.Modules.Add(module);
        RefreshContextModules();
        SelectedContextModule = module;
        StatusText = $"Created module from legacy context text: {module.Name}.";
        RpSimulationService.AppendDebugLog($"Context module created from legacy text | context={SelectedWorldContext.Name} | id={module.Id} | length={module.Text.Length}");
        await AutoSaveWorldContextAsync("module-from-legacy");
    }

    [RelayCommand]
    public async Task ClearLegacyContextText()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        SelectedWorldContext.RulesText = string.Empty;
        WorldContextRulesText = string.Empty;
        RaiseWorldContextChanged();
        StatusText = "Cleared legacy context text. Enabled modules remain active.";
        RpSimulationService.AppendDebugLog($"Legacy context text cleared | context={SelectedWorldContext.Name}");
        await AutoSaveWorldContextAsync("legacy-clear");
    }

    [RelayCommand]
    public async Task SaveContextModule()
    {
        if (SelectedContextModule == null)
        {
            StatusText = "Select a context module first.";
            return;
        }

        ApplyContextModuleEditor();
        RefreshContextModules();
        StatusText = $"Saved context module: {SelectedContextModule.Name}.";
        RpSimulationService.AppendDebugLog($"Context module saved | id={SelectedContextModule.Id} | name={SelectedContextModule.Name} | type={SelectedContextModule.Type} | visibility={SelectedContextModule.Visibility} | appliesTo={SelectedContextModule.AppliesTo} | length={SelectedContextModule.Text?.Length ?? 0}");
        await AutoSaveWorldContextAsync("module-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextModule()
    {
        if (SelectedWorldContext == null || SelectedContextModule == null)
        {
            StatusText = "Select a context module first.";
            return;
        }

        var removed = SelectedContextModule;
        SelectedWorldContext.Modules.Remove(removed);
        ContextModules.Remove(removed);
        SelectedContextModule = ContextModules.FirstOrDefault();
        RaiseWorldContextChanged();
        StatusText = $"Deleted context module: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"Context module deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("module-delete");
    }

    [RelayCommand]
    public async Task AddContextCharacter()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var profile = new RpWorldContextCharacter
        {
            Name = $"Context Character {SelectedWorldContext.Characters.Count + 1}",
            Visibility = RpContextVisibility.WorldOnly,
            Archetype = "regular",
            Race = "Human",
            BodyType = BodyTypeKind.Human,
            TagsText = "creature, sapient",
            GoalText = "Wait and observe.",
            BehaviorProtocol = new RpCharacterBehaviorProtocol()
        };
        SelectedWorldContext.Characters.Add(profile);
        ContextCharacters.Add(profile);
        SelectedContextCharacter = profile;
        RaiseWorldContextChanged();
        StatusText = $"Added context character: {profile.Name}.";
        RpSimulationService.AppendDebugLog($"Context character added | context={SelectedWorldContext.Name} | id={profile.Id} | name={profile.Name}");
        await AutoSaveWorldContextAsync("character-add");
    }

    [RelayCommand]
    public async Task SaveContextCharacter()
    {
        if (SelectedContextCharacter == null)
        {
            StatusText = "Select a context character first.";
            return;
        }

        ApplyContextCharacterEditor();
        RefreshContextCharacters();
        StatusText = $"Saved context character: {SelectedContextCharacter.Name}.";
        RpSimulationService.AppendDebugLog($"Context character saved | id={SelectedContextCharacter.Id} | name={SelectedContextCharacter.Name} | named={SelectedContextCharacter.IsNamedCharacter} | visibility={SelectedContextCharacter.Visibility} | appliesTo={SelectedContextCharacter.AppliesTo}");
        await AutoSaveWorldContextAsync("character-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextCharacter()
    {
        if (SelectedWorldContext == null || SelectedContextCharacter == null)
        {
            StatusText = "Select a context character first.";
            return;
        }

        var removed = SelectedContextCharacter;
        SelectedWorldContext.Characters.Remove(removed);
        ContextCharacters.Remove(removed);
        SelectedContextCharacter = ContextCharacters.FirstOrDefault();
        RaiseWorldContextChanged();
        StatusText = $"Deleted context character: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"Context character deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("character-delete");
    }

    [RelayCommand]
    public async Task AddFactionProfile()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var profile = new RpWorldContextEditorService().AddFactionProfile(SelectedWorldContext);
        SyncRuntimeFaction(profile);
        FactionProfiles.Add(profile);
        SelectedFactionProfile = profile;
        RaiseWorldContextChanged();
        StatusText = $"Added faction profile: {profile.Name}.";
        await AutoSaveWorldContextAsync("faction-add");
    }

    [RelayCommand]
    public async Task SaveFactionProfile()
    {
        if (SelectedFactionProfile == null)
        {
            StatusText = "Select a faction profile first.";
            return;
        }

        ApplyFactionProfileEditor();
        SyncRuntimeFaction(SelectedFactionProfile);
        RefreshFactionProfiles();
        StatusText = $"Saved faction profile: {SelectedFactionProfile.Name}.";
        await AutoSaveWorldContextAsync("faction-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedFactionProfile()
    {
        if (SelectedWorldContext == null || SelectedFactionProfile == null)
        {
            StatusText = "Select a faction profile first.";
            return;
        }

        var removed = SelectedFactionProfile;
        SelectedWorldContext.Factions.Remove(removed);
        FactionProfiles.Remove(removed);
        SelectedFactionProfile = FactionProfiles.FirstOrDefault();
        RaiseWorldContextChanged();
        StatusText = $"Deleted faction profile: {removed.Name}.";
        await AutoSaveWorldContextAsync("faction-delete");
    }

    [RelayCommand]
    public async Task CreateContextCharacterFromSelected()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        if (SelectedCharacter == null)
        {
            StatusText = "Select a world character first.";
            return;
        }

        var profile = new RpWorldContextCharacter
        {
            Name = SelectedCharacter.Name,
            IsNamedCharacter = true,
            Visibility = RpContextVisibility.WorldOnly,
            Archetype = SelectedCharacter.Race,
            Race = SelectedCharacter.Race,
            BodyType = SelectedCharacter.BodyType,
            FactionId = SelectedCharacter.FactionId ?? string.Empty,
            RoleInWorld = SelectedCharacter.CurrentGoal.Description,
            PersonalityText = string.Join(", ", SelectedCharacter.PersonalityTraits),
            StoryText = string.Join(Environment.NewLine, SelectedCharacter.Ideals.Concat(SelectedCharacter.Desires)),
            AbilityText = string.Join(", ", SelectedCharacter.Abilities.Concat(SelectedCharacter.RpAbilities.Select(ability => ability.Name))),
            GoalText = SelectedCharacter.CurrentGoal.Description,
            LifeGoalText = SelectedCharacter.LifeGoal.Description,
            TagsText = string.Join(", ", SelectedCharacter.RpTags),
            StructuredAbilities = SelectedCharacter.RpAbilities
        };
        SelectedWorldContext.Characters.Add(profile);
        ContextCharacters.Add(profile);
        SelectedContextCharacter = profile;
        StatusText = $"Created context profile from {SelectedCharacter.Name}.";
        await AutoSaveWorldContextAsync("character-from-world");
    }

    [RelayCommand]
    public async Task SpawnSelectedContextCharacter()
    {
        if (SelectedContextCharacter == null)
        {
            StatusText = "Select a context character first.";
            return;
        }

        var spawnPosition = FindSpawnPosition();
        var character = CreateCharacterFromProfile(SelectedContextCharacter, spawnPosition);
        World.Characters[character.Id] = character;
        if (World.Tiles.TryGetValue(character.Position, out var tile) && !tile.OccupantIds.Contains(character.Id))
        {
            tile.OccupantIds.Add(character.Id);
        }

        RpSimulationService.UpdatePerception(World);
        Characters.Add(character);
        SelectedCharacter = character;
        RaiseWorldChanged();
        StatusText = $"Spawned {character.Name} at {character.Position}.";
        RpSimulationService.AppendDebugLog($"Context character spawned | profile={SelectedContextCharacter.Id} | character={character.Id} | name={character.Name} | position={character.Position}");
        await _worldSaveService.SaveAsync(World);
    }

    [RelayCommand]
    public async Task AddSpeciesTemplate()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        var template = new RpSpeciesTemplate
        {
            Name = $"Species {SelectedWorldContext.SpeciesTemplates.Count + 1}",
            AppliesToRace = "Human",
            BodyType = BodyTypeKind.Human
        };
        SelectedWorldContext.SpeciesTemplates.Add(template);
        SpeciesTemplates.Add(template);
        SelectedSpeciesTemplate = template;
        StatusText = $"Added species template: {template.Name}.";
        await AutoSaveWorldContextAsync("species-add");
    }

    [RelayCommand]
    public async Task SaveSpeciesTemplate()
    {
        if (SelectedSpeciesTemplate == null)
        {
            StatusText = "Select a species template first.";
            return;
        }

        ApplySpeciesTemplateEditor();
        RefreshSpeciesTemplates();
        StatusText = $"Saved species template: {SelectedSpeciesTemplate.Name}.";
        await AutoSaveWorldContextAsync("species-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedSpeciesTemplate()
    {
        if (SelectedWorldContext == null || SelectedSpeciesTemplate == null)
        {
            StatusText = "Select a species template first.";
            return;
        }

        var removed = SelectedSpeciesTemplate;
        SelectedWorldContext.SpeciesTemplates.Remove(removed);
        SpeciesTemplates.Remove(removed);
        SelectedSpeciesTemplate = SpeciesTemplates.FirstOrDefault();
        StatusText = $"Deleted species template: {removed.Name}.";
        await AutoSaveWorldContextAsync("species-delete");
    }

    [RelayCommand]
    public async Task AddContextAbility()
    {
        if (SelectedContextCharacter == null)
        {
            StatusText = "Select a context character first.";
            return;
        }

        var ability = new RpAbility
        {
            Id = $"ability_{SelectedContextCharacter.StructuredAbilities.Count + 1}",
            Name = $"Ability {SelectedContextCharacter.StructuredAbilities.Count + 1}",
            TargetKind = RpAbilityTargetKind.Character,
            PrimaryResource = RpAbilityResource.None,
            Range = 1,
            TickCost = 1
        };
        SelectedContextCharacter.StructuredAbilities.Add(ability);
        ContextAbilities.Add(ability);
        SelectedContextAbility = ability;
        StatusText = $"Added ability: {ability.Name}.";
        await AutoSaveWorldContextAsync("ability-add");
    }

    [RelayCommand]
    public async Task SaveContextAbility()
    {
        if (SelectedContextAbility == null)
        {
            StatusText = "Select an ability first.";
            return;
        }

        ApplyContextAbilityEditor();
        RefreshContextAbilities();
        StatusText = $"Saved ability: {SelectedContextAbility.Name}.";
        await AutoSaveWorldContextAsync("ability-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextAbility()
    {
        if (SelectedContextCharacter == null || SelectedContextAbility == null)
        {
            StatusText = "Select an ability first.";
            return;
        }

        var removed = SelectedContextAbility;
        SelectedContextCharacter.StructuredAbilities.Remove(removed);
        ContextAbilities.Remove(removed);
        SelectedContextAbility = ContextAbilities.FirstOrDefault();
        StatusText = $"Deleted ability: {removed.Name}.";
        await AutoSaveWorldContextAsync("ability-delete");
    }

    [RelayCommand]
    public async Task AddRelationshipRule()
    {
        if (SelectedContextCharacter == null)
        {
            StatusText = "Select a context character first.";
            return;
        }

        var rule = new RpRelationshipRule
        {
            TargetNameOrTag = "target",
            Type = RpRelationshipType.Unknown
        };
        SelectedContextCharacter.RelationshipRules.Add(rule);
        RelationshipRules.Add(rule);
        SelectedRelationshipRule = rule;
        StatusText = "Added relationship rule.";
        await AutoSaveWorldContextAsync("relationship-add");
    }

    [RelayCommand]
    public async Task SaveRelationshipRule()
    {
        if (SelectedRelationshipRule == null)
        {
            StatusText = "Select a relationship rule first.";
            return;
        }

        ApplyRelationshipRuleEditor();
        RefreshRelationshipRules();
        StatusText = $"Saved relationship rule for {SelectedRelationshipRule.TargetNameOrTag}.";
        await AutoSaveWorldContextAsync("relationship-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedRelationshipRule()
    {
        if (SelectedContextCharacter == null || SelectedRelationshipRule == null)
        {
            StatusText = "Select a relationship rule first.";
            return;
        }

        var removed = SelectedRelationshipRule;
        SelectedContextCharacter.RelationshipRules.Remove(removed);
        RelationshipRules.Remove(removed);
        SelectedRelationshipRule = RelationshipRules.FirstOrDefault();
        StatusText = $"Deleted relationship rule for {removed.TargetNameOrTag}.";
        await AutoSaveWorldContextAsync("relationship-delete");
    }

    [RelayCommand]
    public async Task SaveSceneEnvironmentContinuity()
    {
        if (SelectedWorldContext == null)
        {
            StatusText = "Select a world context first.";
            return;
        }

        ApplySceneEnvironmentContinuityEditor();
        StatusText = "Saved scene, environment, and continuity state.";
        await AutoSaveWorldContextAsync("scene-environment-continuity-save");
    }

    [RelayCommand]
    public void SetLookMode()
    {
        MapActionMode = RpMapActionMode.Look;
        StatusText = "Look mode: hover tiles, click to inspect.";
    }

    [RelayCommand]
    public void SetMoveMode()
    {
        MapActionMode = RpMapActionMode.Move;
        StatusText = "Move mode: click an adjacent tile to move.";
    }

    [RelayCommand]
    public void SetTalkMode()
    {
        MapActionMode = RpMapActionMode.Talk;
        StatusText = "Talk mode: click a character tile.";
    }

    [RelayCommand]
    public void SetInteractMode()
    {
        MapActionMode = RpMapActionMode.Interact;
        StatusText = "Interact mode: click a tile or object.";
    }

    [RelayCommand]
    public void SetUseMode()
    {
        MapActionMode = RpMapActionMode.Use;
        StatusText = "Use mode: click a target tile or object.";
    }

    [RelayCommand]
    public void SetAttackMode()
    {
        MapActionMode = RpMapActionMode.Attack;
        StatusText = "Attack mode: click a target tile.";
    }

    [RelayCommand]
    public void LookAtPlayerTile()
    {
        if (PlayerCharacter == null)
        {
            SelectedTileInfo = "Choose a player character first.";
            return;
        }

        LookAtTile(PlayerCharacter.Position);
    }

    [RelayCommand]
    public void ToggleSliceMode()
    {
        SliceMode = SliceMode == RpSliceMode.Horizontal ? RpSliceMode.Vertical : RpSliceMode.Horizontal;
        SliceCoordinate = 0;
    }

    [RelayCommand]
    public void SliceUp()
    {
        SliceCoordinate++;
    }

    [RelayCommand]
    public void SliceDown()
    {
        SliceCoordinate--;
    }

    [RelayCommand]
    public void AddPlayerInput()
    {
        if (string.IsNullOrWhiteSpace(PlayerInput))
        {
            return;
        }

        var evt = new NarrativeEvent
        {
            Tick = World.Clock.TickCount,
            ActorName = "Narrator",
            Description = PlayerInput.Trim()
        };

        foreach (var character in World.Characters.Values)
        {
            character.PerceivedLog.Add(evt);
            if (character.PerceivedLog.Count > 25)
            {
                character.PerceivedLog = character.PerceivedLog.TakeLast(25).ToList();
            }
        }

        EventLog.Insert(0, FormatEvent(evt));
        PlayerInput = string.Empty;
        TrimEventLog();
        StatusText = "Narrator event added.";
    }

    public string GetTileGlyph(Vec3Int position)
        => _inspectionService.GetTileGlyph(World, position);

    public bool TryGetVisibleBounds(out int minA, out int maxA, out int minB, out int maxB)
        => _inspectionService.TryGetVisibleBounds(World, SliceMode, SliceCoordinate, out minA, out maxA, out minB, out maxB);

    public RpMapRenderSnapshot GetMapRenderSnapshot()
        => _mapRenderProjectionService.CreateSnapshot(
            World,
            SliceMode,
            SliceCoordinate,
            OpenSpaceLookDepth,
            SelectedTilePosition,
            PlayerCharacter?.Position,
            SelectedCharacter?.Position);

    public Vec3Int PositionFromSlice(int a, int b)
        => SliceMode == RpSliceMode.Horizontal
            ? new Vec3Int(a, SliceCoordinate, b)
            : new Vec3Int(a, b, SliceCoordinate);

    public void LookAtTile(Vec3Int position)
    {
        SelectedTilePosition = position;
        SelectedTileInfo = _inspectionService.InspectTile(World, position).Description;
        StatusText = $"Looked at {position}.";
        RefreshGroundItems();
    }

    public void HandleMapTileClick(Vec3Int position)
    {
        LookAtTile(position);
        if (MapActionMode == RpMapActionMode.Look)
        {
            return;
        }

        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (MapActionMode == RpMapActionMode.Talk)
        {
            TalkToInspectedCharacter();
            return;
        }

        if (MapActionMode == RpMapActionMode.Interact)
        {
            InteractWithInspectedTile();
            return;
        }

        if (MapActionMode == RpMapActionMode.Use)
        {
            UseHeldItemOnInspectedTile();
            return;
        }

        if (MapActionMode == RpMapActionMode.Attack)
        {
            AttackInspectedTarget();
            return;
        }

        if (position == PlayerCharacter.Position)
        {
            return;
        }

        var delta = position - PlayerCharacter.Position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) + Math.Abs(delta.Z) != 1)
        {
            StatusText = "Move mode only supports adjacent tiles for now.";
            return;
        }

        _ = TryMovePlayerTo(position, "clicked");
    }

    public bool TryHandleKey(string keyName)
    {
        var normalized = NormalizeKeyName(keyName, string.Empty);
        if (KeyMatches(normalized, MoveNorthKey, "Up", "NumberPad8"))
        {
            _ = MovePlayerNorth();
            return true;
        }

        if (KeyMatches(normalized, MoveSouthKey, "Down", "NumberPad2"))
        {
            _ = MovePlayerSouth();
            return true;
        }

        if (KeyMatches(normalized, MoveWestKey, "Left", "NumberPad4"))
        {
            _ = MovePlayerWest();
            return true;
        }

        if (KeyMatches(normalized, MoveEastKey, "Right", "NumberPad6"))
        {
            _ = MovePlayerEast();
            return true;
        }

        if (KeyMatches(normalized, MoveUpKey, "NumberPad9"))
        {
            _ = MovePlayerUp();
            return true;
        }

        if (KeyMatches(normalized, MoveDownKey, "NumberPad3"))
        {
            _ = MovePlayerDown();
            return true;
        }

        if (KeyMatches(normalized, WaitKey))
        {
            _ = StepTick();
            return true;
        }

        if (KeyMatches(normalized, LookModeKey))
        {
            SetLookMode();
            return true;
        }

        if (KeyMatches(normalized, MoveModeKey))
        {
            SetMoveMode();
            return true;
        }

        return false;
    }

    [RelayCommand]
    public async Task MoveToInspectedTile()
    {
        if (SelectedTilePosition == null)
        {
            StatusText = "Inspect a tile first.";
            return;
        }

        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        var delta = SelectedTilePosition.Value - PlayerCharacter.Position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) + Math.Abs(delta.Z) != 1)
        {
            StatusText = "Move Here only supports adjacent tiles for now.";
            return;
        }

        await TryMovePlayerTo(SelectedTilePosition.Value, "to inspected tile");
    }

    [RelayCommand]
    public void TalkToInspectedCharacter()
    {
        var evt = _interactionService.CreateTalkEvent(World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        StatusText = status;
    }

    [RelayCommand]
    public void InteractWithInspectedTile()
    {
        var evt = _interactionService.CreateInteractEvent(World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        StatusText = status;
    }

    [RelayCommand]
    public void UseHeldItemOnInspectedTile()
    {
        var evt = _interactionService.CreateUseEvent(World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        StatusText = status;
    }

    [RelayCommand]
    public void AttackInspectedTarget()
    {
        var evt = _interactionService.CreateAttackEvent(World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        StatusText = status;
    }

    [RelayCommand]
    public async Task CastFireballAtInspectedTile()
    {
        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedTilePosition == null)
        {
            StatusText = "Inspect a tile first.";
            return;
        }

        if (_abilityService.TryCastFireball(World, PlayerCharacter, SelectedTilePosition.Value, out var evt, out var status))
        {
            AddEventToAll(evt);
            LookAtTile(SelectedTilePosition.Value);
            RaiseWorldChanged();
            await AdvanceWorldAfterPlayerActionAsync(status);
            return;
        }

        StatusText = status;
    }

    [RelayCommand]
    public void PickUpSelectedGroundItem()
    {
        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedGroundItem == null)
        {
            StatusText = "Select a ground item first.";
            return;
        }

        if (!_inventoryService.TryPickUp(World, PlayerCharacter, SelectedGroundItem, out var error))
        {
            StatusText = error;
            return;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} picked up {SelectedGroundItem.Name}."
        });
        RefreshInventory();
        RefreshGroundItems();
        LookAtTile(PlayerCharacter.Position);
    }

    [RelayCommand]
    public void DropSelectedInventoryItem()
    {
        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedInventoryItem == null)
        {
            StatusText = "Select an inventory item first.";
            return;
        }

        if (!_inventoryService.TryDrop(World, PlayerCharacter, SelectedInventoryItem, out var error))
        {
            StatusText = error;
            return;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} dropped {SelectedInventoryItem.Name}."
        });
        RefreshInventory();
        RefreshGroundItems();
        LookAtTile(PlayerCharacter.Position);
    }

    [RelayCommand]
    public void HoldSelectedInventoryItem()
    {
        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedInventoryItem == null)
        {
            StatusText = "Select an inventory item first.";
            return;
        }

        if (!_inventoryService.TryHold(PlayerCharacter, SelectedInventoryItem, out var error))
        {
            StatusText = error;
            return;
        }

        RefreshInventory();
        StatusText = $"Holding {SelectedInventoryItem.Name}.";
    }

    private void ResetCollections()
    {
        Characters.Clear();
        EnsureWorldContexts();
        WorldContexts.Clear();
        foreach (var context in World.WorldContexts)
        {
            WorldContexts.Add(context);
        }

        SelectedWorldContext = WorldContexts.FirstOrDefault(context => context.IsEnabled) ?? WorldContexts.FirstOrDefault();

        foreach (var character in World.Characters.Values)
        {
            RpBodyFactory.EnsureBody(character);
            RpCreatureService.EnsureCreatureStats(character);
        }

        RpSimulationService.UpdatePerception(World);

        foreach (var character in World.Characters.Values.OrderBy(c => c.Name))
        {
            Characters.Add(character);
        }

        SelectedCharacter = Characters.FirstOrDefault(c => string.Equals(c.FactionId, "player", StringComparison.OrdinalIgnoreCase)) ??
            Characters.FirstOrDefault();
        PlayerCharacter = SelectedCharacter;
        EventLog.Clear();
        foreach (var evt in World.Characters.Values.SelectMany(c => c.PerceivedLog).DistinctBy(e => $"{e.Tick}:{e.ActorName}:{e.Description}").OrderByDescending(e => e.Tick).Take(30))
        {
            EventLog.Add(FormatEvent(evt));
        }

        RefreshInventory();
        RefreshGroundItems();
    }

    private void RaiseWorldChanged()
    {
        OnPropertyChanged(nameof(WorldTitle));
        OnPropertyChanged(nameof(SliceModeText));
        OnPropertyChanged(nameof(SliceCoordinateText));
        OnPropertyChanged(nameof(PlayerCharacterText));
        RaisePlayerStatsChanged();
        OnPropertyChanged(nameof(IsMainMenuView));
        OnPropertyChanged(nameof(IsWorldView));
        OnPropertyChanged(nameof(IsSettingsView));
        OnPropertyChanged(nameof(IsWorldContextView));
        OnPropertyChanged(nameof(IsGameOptionsView));
        OnPropertyChanged(nameof(IsDevMenuView));
        OnPropertyChanged(nameof(IsTestMapsView));
        OnPropertyChanged(nameof(ActiveViewText));
        OnPropertyChanged(nameof(InventorySummary));
        RaiseWorldContextChanged();
        UpdateSelectedCharacterSummary();
    }

    private void RaiseWorldContextChanged()
    {
        OnPropertyChanged(nameof(ActiveWorldContextSummary));
        OnPropertyChanged(nameof(ActiveContextModuleSummary));
        OnPropertyChanged(nameof(ActiveContextCharacterSummary));
        OnPropertyChanged(nameof(ActiveFactionProfileSummary));
        OnPropertyChanged(nameof(SelectedWorldContext));
        OnPropertyChanged(nameof(SelectedContextModule));
        OnPropertyChanged(nameof(SelectedContextCharacter));
        OnPropertyChanged(nameof(SelectedFactionProfile));
        OnPropertyChanged(nameof(WorldContexts));
        OnPropertyChanged(nameof(ContextModules));
        OnPropertyChanged(nameof(ContextCharacters));
        OnPropertyChanged(nameof(FactionProfiles));
    }

    private void EnsureWorldContexts()
    {
        World.WorldContexts ??= [];
        if (World.WorldContexts.Count > 0)
        {
            foreach (var context in World.WorldContexts)
            {
                EnsureWorldContextModules(context);
            }

            return;
        }

        World.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Default Simulation Rules",
            IsEnabled = true,
            RulesText = "Characters must choose actions that are consistent with the current world state, their body, resources, relationships, goals, and availableActions.",
            Modules =
            [
                new RpContextModule
                {
                    Name = "Default Simulation Rules",
                    IsEnabled = true,
                    Type = RpContextModuleType.GeneralRules,
                    Visibility = RpContextVisibility.WorldOnly,
                    Priority = 100,
                    Text = "Characters must choose actions that are consistent with the current world state, their body, resources, relationships, goals, and availableActions."
                }
            ]
        });
    }

    private static void EnsureWorldContextModules(RpWorldContextEntry context)
    {
        context.Modules ??= [];
        context.Characters ??= [];
        context.SpeciesTemplates ??= [];
        context.Factions ??= [];
        context.SceneState ??= new RpSceneState();
        context.EnvironmentRules ??= new RpEnvironmentRuleSet();
        context.Continuity ??= new RpContinuityState();
        foreach (var character in context.Characters)
        {
            character.BehaviorProtocol ??= new RpCharacterBehaviorProtocol();
            character.StructuredAbilities ??= [];
            character.RelationshipRules ??= [];
        }

        foreach (var faction in context.Factions)
        {
            faction.Roles ??= [];
            faction.Abilities ??= [];
            faction.RelationshipRules ??= [];
            foreach (var role in faction.Roles)
            {
                role.Abilities ??= [];
            }
        }

        if (context.Modules.Count == 0 && !string.IsNullOrWhiteSpace(context.RulesText))
        {
            context.Modules.Add(new RpContextModule
            {
                Name = "Legacy Rules",
                IsEnabled = true,
                Type = RpContextModuleType.GeneralRules,
                Visibility = RpContextVisibility.WorldOnly,
                Priority = 100,
                Text = context.RulesText,
                SourceLabel = "Migrated from RulesText"
            });
        }
    }

    private static IReadOnlyList<RpContextSectionTemplate> StandardContextSectionTemplates()
        =>
        [
            new("Core Identity", RpContextModuleType.CoreIdentity, RpContextVisibility.WorldOnly,
                "Name, role, archetype, faction position, public face, private role, and non-negotiable identity constraints."),
            new("Physical Traits", RpContextModuleType.PhysicalTraits, RpContextVisibility.Public,
                "Visible body traits, movement limits, body language, equipment implications, and anatomy-relevant constraints."),
            new("Psychology", RpContextModuleType.Psychology, RpContextVisibility.HiddenFromPlayer,
                "Motives, fears, insecurities, priorities, habits, stress reactions, and internal decision pressures."),
            new("Abilities", RpContextModuleType.AbilityMechanics, RpContextVisibility.WorldOnly,
                "Structured powers or skills: name, resource cost, range or target, constraints, cooldown or tick cost, world effect, narrative effect, allowed usage, and forbidden usage."),
            new("Interaction Protocols", RpContextModuleType.InteractionProtocols, RpContextVisibility.CharacterKnown,
                "Speech style, first encounter behavior, negotiation style, escalation and de-escalation pattern, relationship handling, deception mode, combat preferences, and capture preferences."),
            new("Species Biology", RpContextModuleType.SpeciesBiology, RpContextVisibility.WorldOnly,
                "Species templates, body language dictionaries, vocalization dictionaries, diet, energy, magic rules, and anatomy/body-part modifiers."),
            new("Relationship Dynamics", RpContextModuleType.RelationshipRules, RpContextVisibility.WorldOnly,
                "Rules for subordinates, equals, superiors, allies, rivals, enemies, dependents, trust, fear, dependency, loyalty, manipulation, suspicion, and known secrets."),
            new("Scene Rules", RpContextModuleType.SceneRules, RpContextVisibility.WorldOnly,
                "Active scene phase, pacing rules, escalation budget/rate, active threads, foreshadowed elements, unresolved promises, and major action prerequisites."),
            new("Environment Rules", RpContextModuleType.EnvironmentRules, RpContextVisibility.WorldOnly,
                "Interactive objects, environmental tells, hazards, terrain affordances, clues, and domain-owner awareness/control rules."),
            new("Continuity Trackers", RpContextModuleType.Continuity, RpContextVisibility.WorldOnly,
                "Persistent physical changes, emotional state changes, relationship changes, flags, triggers, irreversible events, and pending consequences."),
            new("World Lore", RpContextModuleType.WorldLore, RpContextVisibility.Public,
                "Setting facts, factions, places, history, constraints, and common knowledge that should remain consistent."),
            new("Faction Identity", RpContextModuleType.FactionIdentity, RpContextVisibility.WorldOnly,
                "Faction identity, public reputation, private doctrine, internal motives, goals, taboos, and outsider/member behavior. Use AppliesTo like faction:example_id."),
            new("Faction Culture", RpContextModuleType.FactionCulture, RpContextVisibility.WorldOnly,
                "Customs, social norms, values, body-language conventions, speech conventions, and internal expectations for a specific faction."),
            new("Faction Hierarchy", RpContextModuleType.FactionHierarchy, RpContextVisibility.WorldOnly,
                "Ranks, castes, command structure, authority boundaries, role obligations, and leadership rules."),
            new("Faction Roles", RpContextModuleType.FactionRole, RpContextVisibility.WorldOnly,
                "Role/caste defaults: behavior, stats, abilities, equipment, tags, duties, and relationship to leadership."),
            new("Faction Appearance", RpContextModuleType.FactionAppearance, RpContextVisibility.Public,
                "Faction-specific visual markers, anatomy variants, forms, colors, uniforms, tells, and recognizable traits."),
            new("Import Review Notes", RpContextModuleType.ImportNotes, RpContextVisibility.WorldOnly,
                "Manual notes about source quality, disabled sections, rewritten unsafe content, prompt-injection fragments, and later cleanup tasks.")
        ];

    private void LoadWorldContextEditor(RpWorldContextEntry? context)
    {
        _isLoadingWorldContextEditor = true;
        try
        {
            WorldContextName = context?.Name ?? string.Empty;
            WorldContextIsEnabled = context?.IsEnabled ?? false;
            WorldContextRulesText = context?.RulesText ?? string.Empty;
        }
        finally
        {
            _isLoadingWorldContextEditor = false;
        }
    }

    private void RefreshContextModules()
    {
        ContextModules.Clear();
        if (SelectedWorldContext != null)
        {
            EnsureWorldContextModules(SelectedWorldContext);
            foreach (var module in SelectedWorldContext.Modules.OrderBy(module => module.Priority).ThenBy(module => module.Name))
            {
                ContextModules.Add(module);
            }
        }

        SelectedContextModule = ContextModules.FirstOrDefault(module => module.IsEnabled) ?? ContextModules.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveContextModuleSummary));
    }

    private void LoadContextModuleEditor(RpContextModule? module)
    {
        _isLoadingContextModuleEditor = true;
        try
        {
            ContextModuleName = module?.Name ?? string.Empty;
            ContextModuleIsEnabled = module?.IsEnabled ?? false;
            ContextModuleType = module?.Type.ToString() ?? RpContextModuleType.GeneralRules.ToString();
            ContextModuleVisibility = module?.Visibility.ToString() ?? RpContextVisibility.WorldOnly.ToString();
            ContextModulePriority = module?.Priority ?? 100;
            ContextModuleSourceLabel = module?.SourceLabel ?? string.Empty;
            ContextModuleAppliesTo = module?.AppliesTo ?? string.Empty;
            ContextModuleText = module?.Text ?? string.Empty;
        }
        finally
        {
            _isLoadingContextModuleEditor = false;
        }
    }

    private void ApplyContextModuleEditor()
    {
        if (_isLoadingContextModuleEditor || SelectedContextModule == null)
        {
            return;
        }

        SelectedContextModule.Name = string.IsNullOrWhiteSpace(ContextModuleName) ? "Context Module" : ContextModuleName.Trim();
        SelectedContextModule.IsEnabled = ContextModuleIsEnabled;
        SelectedContextModule.Type = Enum.TryParse<RpContextModuleType>(ContextModuleType, true, out var type)
            ? type
            : RpContextModuleType.GeneralRules;
        SelectedContextModule.Visibility = Enum.TryParse<RpContextVisibility>(ContextModuleVisibility, true, out var visibility)
            ? visibility
            : RpContextVisibility.WorldOnly;
        SelectedContextModule.Priority = ContextModulePriority;
        SelectedContextModule.SourceLabel = ContextModuleSourceLabel.Trim();
        SelectedContextModule.AppliesTo = ContextModuleAppliesTo.Trim();
        SelectedContextModule.Text = ContextModuleText;
    }

    private void RefreshContextCharacters()
    {
        ContextCharacters.Clear();
        foreach (var character in SelectedWorldContext?.Characters ?? [])
        {
            ContextCharacters.Add(character);
        }

        SelectedContextCharacter = ContextCharacters.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveContextCharacterSummary));
    }

    private void LoadContextCharacterEditor(RpWorldContextCharacter? profile)
    {
        _isLoadingContextCharacterEditor = true;
        try
        {
            ContextCharacterName = profile?.Name ?? string.Empty;
            ContextCharacterIsNamed = profile?.IsNamedCharacter ?? false;
            ContextCharacterVisibility = profile?.Visibility.ToString() ?? RpContextVisibility.WorldOnly.ToString();
            ContextCharacterAppliesTo = profile?.AppliesTo ?? string.Empty;
            ContextCharacterArchetype = profile?.Archetype ?? "regular";
            ContextCharacterRace = profile?.Race ?? "Human";
            ContextCharacterBodyType = profile?.BodyType.ToString() ?? BodyTypeKind.Human.ToString();
            ContextCharacterFactionId = profile?.FactionId ?? string.Empty;
            ContextCharacterRole = profile?.RoleInWorld ?? string.Empty;
            ContextCharacterPersonality = profile?.PersonalityText ?? string.Empty;
            ContextCharacterStory = profile?.StoryText ?? string.Empty;
            ContextCharacterAbilities = profile?.AbilityText ?? string.Empty;
            ContextCharacterGoal = profile?.GoalText ?? "Wait and observe.";
            ContextCharacterLifeGoal = profile?.LifeGoalText ?? string.Empty;
            ContextCharacterTags = profile?.TagsText ?? "creature, sapient";
            ContextCharacterSpeechStyle = profile?.BehaviorProtocol?.SpeechStyle ?? string.Empty;
            ContextCharacterFirstEncounter = profile?.BehaviorProtocol?.FirstEncounterBehavior ?? string.Empty;
            ContextCharacterNegotiation = profile?.BehaviorProtocol?.NegotiationStyle ?? string.Empty;
            ContextCharacterEscalation = profile?.BehaviorProtocol?.EscalationPattern ?? string.Empty;
            ContextCharacterDeEscalation = profile?.BehaviorProtocol?.DeEscalationPattern ?? string.Empty;
            ContextCharacterRelationshipHandling = profile?.BehaviorProtocol?.RelationshipHandling ?? string.Empty;
            ContextCharacterDeception = profile?.BehaviorProtocol?.DeceptionMode ?? string.Empty;
            ContextCharacterCombatPreferences = profile?.BehaviorProtocol?.CombatPreferences ?? string.Empty;
            ContextCharacterCapturePreferences = profile?.BehaviorProtocol?.CapturePreferences ?? string.Empty;
        }
        finally
        {
            _isLoadingContextCharacterEditor = false;
        }
    }

    private void ApplyContextCharacterEditor()
    {
        if (_isLoadingContextCharacterEditor || SelectedContextCharacter == null)
        {
            return;
        }

        SelectedContextCharacter.Name = string.IsNullOrWhiteSpace(ContextCharacterName) ? "New Character" : ContextCharacterName.Trim();
        SelectedContextCharacter.IsNamedCharacter = ContextCharacterIsNamed;
        SelectedContextCharacter.Visibility = Enum.TryParse<RpContextVisibility>(ContextCharacterVisibility, true, out var visibility)
            ? visibility
            : RpContextVisibility.WorldOnly;
        SelectedContextCharacter.AppliesTo = ContextCharacterAppliesTo.Trim();
        SelectedContextCharacter.Archetype = string.IsNullOrWhiteSpace(ContextCharacterArchetype) ? "regular" : ContextCharacterArchetype.Trim();
        SelectedContextCharacter.Race = string.IsNullOrWhiteSpace(ContextCharacterRace) ? "Human" : ContextCharacterRace.Trim();
        SelectedContextCharacter.BodyType = Enum.TryParse<BodyTypeKind>(ContextCharacterBodyType, true, out var bodyType)
            ? bodyType
            : BodyTypeKind.Human;
        SelectedContextCharacter.FactionId = ContextCharacterFactionId.Trim();
        SelectedContextCharacter.RoleInWorld = ContextCharacterRole.Trim();
        SelectedContextCharacter.PersonalityText = ContextCharacterPersonality;
        SelectedContextCharacter.StoryText = ContextCharacterStory;
        SelectedContextCharacter.AbilityText = ContextCharacterAbilities;
        SelectedContextCharacter.GoalText = string.IsNullOrWhiteSpace(ContextCharacterGoal) ? "Wait and observe." : ContextCharacterGoal.Trim();
        SelectedContextCharacter.LifeGoalText = ContextCharacterLifeGoal.Trim();
        SelectedContextCharacter.TagsText = string.IsNullOrWhiteSpace(ContextCharacterTags) ? "creature, sapient" : ContextCharacterTags.Trim();
        SelectedContextCharacter.BehaviorProtocol ??= new RpCharacterBehaviorProtocol();
        SelectedContextCharacter.BehaviorProtocol.SpeechStyle = ContextCharacterSpeechStyle;
        SelectedContextCharacter.BehaviorProtocol.FirstEncounterBehavior = ContextCharacterFirstEncounter;
        SelectedContextCharacter.BehaviorProtocol.NegotiationStyle = ContextCharacterNegotiation;
        SelectedContextCharacter.BehaviorProtocol.EscalationPattern = ContextCharacterEscalation;
        SelectedContextCharacter.BehaviorProtocol.DeEscalationPattern = ContextCharacterDeEscalation;
        SelectedContextCharacter.BehaviorProtocol.RelationshipHandling = ContextCharacterRelationshipHandling;
        SelectedContextCharacter.BehaviorProtocol.DeceptionMode = ContextCharacterDeception;
        SelectedContextCharacter.BehaviorProtocol.CombatPreferences = ContextCharacterCombatPreferences;
        SelectedContextCharacter.BehaviorProtocol.CapturePreferences = ContextCharacterCapturePreferences;
    }

    private void RefreshFactionProfiles()
    {
        FactionProfiles.Clear();
        foreach (var faction in SelectedWorldContext?.Factions ?? [])
        {
            FactionProfiles.Add(faction);
        }

        SelectedFactionProfile = FactionProfiles.FirstOrDefault(faction => faction.IsEnabled) ?? FactionProfiles.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveFactionProfileSummary));
    }

    private void LoadFactionProfileEditor(RpFactionProfile? faction)
    {
        _isLoadingFactionProfileEditor = true;
        try
        {
            FactionId = faction?.FactionId ?? string.Empty;
            FactionName = faction?.Name ?? string.Empty;
            FactionIsEnabled = faction?.IsEnabled ?? false;
            FactionVisibility = faction?.Visibility.ToString() ?? RpContextVisibility.WorldOnly.ToString();
            FactionAppliesTo = faction?.AppliesTo ?? string.Empty;
            FactionParentSpecies = faction?.ParentSpeciesOrRace ?? string.Empty;
            FactionPublicDescription = faction?.PublicDescription ?? string.Empty;
            FactionHiddenDoctrine = faction?.HiddenDoctrine ?? string.Empty;
            FactionCulture = faction?.CultureText ?? string.Empty;
            FactionHierarchy = faction?.HierarchyText ?? string.Empty;
            FactionGoals = faction?.GoalsText ?? string.Empty;
            FactionTaboos = faction?.TaboosText ?? string.Empty;
            FactionOutsiderBehavior = faction?.OutsiderBehavior ?? string.Empty;
            FactionMemberBehavior = faction?.MemberBehavior ?? string.Empty;
            FactionAppearance = faction?.AppearanceText ?? string.Empty;
            FactionAnatomyOverrides = faction?.AnatomyOverridesText ?? string.Empty;
            FactionMagicRules = faction?.MagicRules ?? string.Empty;
            FactionResourceRules = faction?.ResourceRules ?? string.Empty;
            FactionTags = faction?.TagsText ?? string.Empty;
            FactionRolesText = FormatFactionRoles(faction?.Roles);
            FactionAbilitiesText = FormatAbilities(faction?.Abilities);
            FactionRelationshipRulesText = FormatRelationshipRules(faction?.RelationshipRules);
        }
        finally
        {
            _isLoadingFactionProfileEditor = false;
        }
    }

    private void ApplyFactionProfileEditor()
    {
        if (_isLoadingFactionProfileEditor || SelectedFactionProfile == null)
        {
            return;
        }

        SelectedFactionProfile.FactionId = string.IsNullOrWhiteSpace(FactionId) ? Slugify(FactionName, "faction") : FactionId.Trim();
        SelectedFactionProfile.Name = string.IsNullOrWhiteSpace(FactionName) ? "Faction" : FactionName.Trim();
        SelectedFactionProfile.IsEnabled = FactionIsEnabled;
        SelectedFactionProfile.Visibility = Enum.TryParse<RpContextVisibility>(FactionVisibility, true, out var visibility)
            ? visibility
            : RpContextVisibility.WorldOnly;
        SelectedFactionProfile.AppliesTo = FactionAppliesTo.Trim();
        SelectedFactionProfile.ParentSpeciesOrRace = FactionParentSpecies.Trim();
        SelectedFactionProfile.PublicDescription = FactionPublicDescription;
        SelectedFactionProfile.HiddenDoctrine = FactionHiddenDoctrine;
        SelectedFactionProfile.CultureText = FactionCulture;
        SelectedFactionProfile.HierarchyText = FactionHierarchy;
        SelectedFactionProfile.GoalsText = FactionGoals;
        SelectedFactionProfile.TaboosText = FactionTaboos;
        SelectedFactionProfile.OutsiderBehavior = FactionOutsiderBehavior;
        SelectedFactionProfile.MemberBehavior = FactionMemberBehavior;
        SelectedFactionProfile.AppearanceText = FactionAppearance;
        SelectedFactionProfile.AnatomyOverridesText = FactionAnatomyOverrides;
        SelectedFactionProfile.MagicRules = FactionMagicRules;
        SelectedFactionProfile.ResourceRules = FactionResourceRules;
        SelectedFactionProfile.TagsText = FactionTags;
        SelectedFactionProfile.Roles = ParseFactionRoles(FactionRolesText);
        SelectedFactionProfile.Abilities = ParseAbilities(FactionAbilitiesText);
        SelectedFactionProfile.RelationshipRules = ParseRelationshipRules(FactionRelationshipRulesText);
        OnPropertyChanged(nameof(ActiveFactionProfileSummary));
    }

    private void RefreshSpeciesTemplates()
    {
        SpeciesTemplates.Clear();
        foreach (var template in SelectedWorldContext?.SpeciesTemplates ?? [])
        {
            SpeciesTemplates.Add(template);
        }

        SelectedSpeciesTemplate = SpeciesTemplates.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveSpeciesTemplateSummary));
    }

    private void LoadSpeciesTemplateEditor(RpSpeciesTemplate? template)
    {
        _isLoadingSpeciesTemplateEditor = true;
        try
        {
            SpeciesTemplateName = template?.Name ?? string.Empty;
            SpeciesTemplateRace = template?.AppliesToRace ?? string.Empty;
            SpeciesTemplateBodyType = template?.BodyType.ToString() ?? BodyTypeKind.Human.ToString();
            SpeciesBodyLanguageText = FormatDictionary(template?.BodyLanguage);
            SpeciesVocalizationsText = FormatDictionary(template?.Vocalizations);
            SpeciesDietRules = template?.DietRules ?? string.Empty;
            SpeciesEnergyRules = template?.EnergyRules ?? string.Empty;
            SpeciesMagicRules = template?.MagicRules ?? string.Empty;
            SpeciesAnatomyModifiersText = FormatDictionary(template?.AnatomyModifiers);
            SpeciesTagsText = FormatLines(template?.Tags);
        }
        finally
        {
            _isLoadingSpeciesTemplateEditor = false;
        }
    }

    private void ApplySpeciesTemplateEditor()
    {
        if (_isLoadingSpeciesTemplateEditor || SelectedSpeciesTemplate == null)
        {
            return;
        }

        SelectedSpeciesTemplate.Name = string.IsNullOrWhiteSpace(SpeciesTemplateName) ? "Species" : SpeciesTemplateName.Trim();
        SelectedSpeciesTemplate.AppliesToRace = SpeciesTemplateRace.Trim();
        SelectedSpeciesTemplate.BodyType = Enum.TryParse<BodyTypeKind>(SpeciesTemplateBodyType, true, out var bodyType)
            ? bodyType
            : BodyTypeKind.Human;
        SelectedSpeciesTemplate.BodyLanguage = ParseDictionary(SpeciesBodyLanguageText);
        SelectedSpeciesTemplate.Vocalizations = ParseDictionary(SpeciesVocalizationsText);
        SelectedSpeciesTemplate.DietRules = SpeciesDietRules;
        SelectedSpeciesTemplate.EnergyRules = SpeciesEnergyRules;
        SelectedSpeciesTemplate.MagicRules = SpeciesMagicRules;
        SelectedSpeciesTemplate.AnatomyModifiers = ParseDictionary(SpeciesAnatomyModifiersText);
        SelectedSpeciesTemplate.Tags = ParseLines(SpeciesTagsText);
    }

    private void RefreshContextAbilities()
    {
        ContextAbilities.Clear();
        SelectedContextCharacter?.StructuredAbilities?.ForEach(ContextAbilities.Add);
        SelectedContextAbility = ContextAbilities.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveAbilitySummary));
    }

    private void LoadContextAbilityEditor(RpAbility? ability)
    {
        _isLoadingAbilityEditor = true;
        try
        {
            ContextAbilityId = ability?.Id ?? string.Empty;
            ContextAbilityName = ability?.Name ?? string.Empty;
            ContextAbilityDescription = ability?.Description ?? string.Empty;
            ContextAbilityTargetKind = ability?.TargetKind.ToString() ?? RpAbilityTargetKind.Tile.ToString();
            ContextAbilityDamageType = ability?.DamageType.ToString() ?? RpDamageType.Physical.ToString();
            ContextAbilityResource = ability?.PrimaryResource.ToString() ?? RpAbilityResource.None.ToString();
            ContextAbilityManaCost = ability?.ManaCost ?? 0;
            ContextAbilityFocusCost = ability?.FocusCost ?? 0;
            ContextAbilityStaminaCost = ability?.StaminaCost ?? 0;
            ContextAbilityDamage = ability?.Damage ?? 0;
            ContextAbilityRange = ability?.Range ?? 1;
            ContextAbilityTickCost = ability?.TickCost ?? 1;
            ContextAbilityCooldownTicks = ability?.CooldownTicks ?? 0;
            ContextAbilityRangeText = ability?.RangeText ?? string.Empty;
            ContextAbilityTargetRules = ability?.TargetRules ?? string.Empty;
            ContextAbilityConstraints = ability?.Constraints ?? string.Empty;
            ContextAbilityWorldEffect = ability?.WorldEffect ?? string.Empty;
            ContextAbilityNarrativeEffect = ability?.NarrativeEffect ?? string.Empty;
            ContextAbilityAllowedUsage = ability?.AllowedUsage ?? string.Empty;
            ContextAbilityForbiddenUsage = ability?.ForbiddenUsage ?? string.Empty;
            ContextAbilityTagsText = FormatLines(ability?.Tags);
        }
        finally
        {
            _isLoadingAbilityEditor = false;
        }
    }

    private void ApplyContextAbilityEditor()
    {
        if (_isLoadingAbilityEditor || SelectedContextAbility == null)
        {
            return;
        }

        SelectedContextAbility.Id = string.IsNullOrWhiteSpace(ContextAbilityId) ? ContextAbilityName.Trim().ToLowerInvariant().Replace(' ', '_') : ContextAbilityId.Trim();
        SelectedContextAbility.Name = string.IsNullOrWhiteSpace(ContextAbilityName) ? "Ability" : ContextAbilityName.Trim();
        SelectedContextAbility.Description = ContextAbilityDescription;
        SelectedContextAbility.TargetKind = Enum.TryParse<RpAbilityTargetKind>(ContextAbilityTargetKind, true, out var targetKind) ? targetKind : RpAbilityTargetKind.Tile;
        SelectedContextAbility.DamageType = Enum.TryParse<RpDamageType>(ContextAbilityDamageType, true, out var damageType) ? damageType : RpDamageType.Physical;
        SelectedContextAbility.PrimaryResource = Enum.TryParse<RpAbilityResource>(ContextAbilityResource, true, out var resource) ? resource : RpAbilityResource.None;
        SelectedContextAbility.ManaCost = Math.Max(0, ContextAbilityManaCost);
        SelectedContextAbility.FocusCost = Math.Max(0, ContextAbilityFocusCost);
        SelectedContextAbility.StaminaCost = Math.Max(0, ContextAbilityStaminaCost);
        SelectedContextAbility.Damage = Math.Max(0, ContextAbilityDamage);
        SelectedContextAbility.Range = Math.Max(0, ContextAbilityRange);
        SelectedContextAbility.TickCost = Math.Max(1, ContextAbilityTickCost);
        SelectedContextAbility.CooldownTicks = Math.Max(0, ContextAbilityCooldownTicks);
        SelectedContextAbility.RangeText = ContextAbilityRangeText;
        SelectedContextAbility.TargetRules = ContextAbilityTargetRules;
        SelectedContextAbility.Constraints = ContextAbilityConstraints;
        SelectedContextAbility.WorldEffect = ContextAbilityWorldEffect;
        SelectedContextAbility.NarrativeEffect = ContextAbilityNarrativeEffect;
        SelectedContextAbility.AllowedUsage = ContextAbilityAllowedUsage;
        SelectedContextAbility.ForbiddenUsage = ContextAbilityForbiddenUsage;
        SelectedContextAbility.Tags = ParseLines(ContextAbilityTagsText);
    }

    private void RefreshRelationshipRules()
    {
        RelationshipRules.Clear();
        SelectedContextCharacter?.RelationshipRules?.ForEach(RelationshipRules.Add);
        SelectedRelationshipRule = RelationshipRules.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveRelationshipRuleSummary));
    }

    private void LoadRelationshipRuleEditor(RpRelationshipRule? rule)
    {
        _isLoadingRelationshipRuleEditor = true;
        try
        {
            RelationshipTargetNameOrTag = rule?.TargetNameOrTag ?? string.Empty;
            RelationshipType = rule?.Type.ToString() ?? RpRelationshipType.Unknown.ToString();
            RelationshipTrust = rule?.Trust ?? 0;
            RelationshipFear = rule?.Fear ?? 0;
            RelationshipDependency = rule?.Dependency ?? 0;
            RelationshipLoyalty = rule?.Loyalty ?? 0;
            RelationshipManipulation = rule?.Manipulation ?? 0;
            RelationshipSuspicion = rule?.Suspicion ?? 0;
            RelationshipKnownSecretsText = FormatLines(rule?.KnownSecrets);
            RelationshipHandlingRules = rule?.HandlingRules ?? string.Empty;
        }
        finally
        {
            _isLoadingRelationshipRuleEditor = false;
        }
    }

    private void ApplyRelationshipRuleEditor()
    {
        if (_isLoadingRelationshipRuleEditor || SelectedRelationshipRule == null)
        {
            return;
        }

        SelectedRelationshipRule.TargetNameOrTag = string.IsNullOrWhiteSpace(RelationshipTargetNameOrTag) ? "target" : RelationshipTargetNameOrTag.Trim();
        SelectedRelationshipRule.Type = Enum.TryParse<RpRelationshipType>(RelationshipType, true, out var type) ? type : RpRelationshipType.Unknown;
        SelectedRelationshipRule.Trust = RelationshipTrust;
        SelectedRelationshipRule.Fear = RelationshipFear;
        SelectedRelationshipRule.Dependency = RelationshipDependency;
        SelectedRelationshipRule.Loyalty = RelationshipLoyalty;
        SelectedRelationshipRule.Manipulation = RelationshipManipulation;
        SelectedRelationshipRule.Suspicion = RelationshipSuspicion;
        SelectedRelationshipRule.KnownSecrets = ParseLines(RelationshipKnownSecretsText);
        SelectedRelationshipRule.HandlingRules = RelationshipHandlingRules;
    }

    private void LoadSceneEnvironmentContinuityEditor(RpWorldContextEntry? context)
    {
        _isLoadingSceneEnvironmentContinuityEditor = true;
        try
        {
            var scene = context?.SceneState ?? new RpSceneState();
            ScenePhase = scene.Phase.ToString();
            SceneEscalationBudget = scene.EscalationBudget;
            SceneEscalationRate = scene.EscalationRatePerTick;
            SceneActiveThreadsText = FormatLines(scene.ActiveThreads);
            SceneForeshadowedText = FormatLines(scene.ForeshadowedElements);
            SceneUnresolvedPromisesText = FormatLines(scene.UnresolvedPromises);
            SceneMajorPrerequisitesText = FormatLines(scene.MajorActionPrerequisites);

            var environment = context?.EnvironmentRules ?? new RpEnvironmentRuleSet();
            EnvironmentInteractiveObjectsText = FormatInteractiveObjects(environment.InteractiveObjects);
            EnvironmentTellsText = FormatLines(environment.EnvironmentalTells);
            EnvironmentHazardsText = FormatLines(environment.Hazards);
            EnvironmentTerrainAffordancesText = FormatLines(environment.TerrainAffordances);
            EnvironmentCluesText = FormatLines(environment.Clues);
            EnvironmentDomainOwnerRulesText = FormatLines(environment.DomainOwnerAwarenessRules);

            var continuity = context?.Continuity ?? new RpContinuityState();
            ContinuityPhysicalChangesText = FormatLines(continuity.PersistentPhysicalChanges);
            ContinuityEmotionalChangesText = FormatLines(continuity.EmotionalStateChanges);
            ContinuityRelationshipChangesText = FormatLines(continuity.RelationshipChanges);
            ContinuityFlagsText = FormatLines(continuity.Flags);
            ContinuityTriggersText = FormatLines(continuity.Triggers);
            ContinuityIrreversibleEventsText = FormatLines(continuity.IrreversibleEvents);
            ContinuityPendingConsequencesText = FormatLines(continuity.PendingConsequences);
        }
        finally
        {
            _isLoadingSceneEnvironmentContinuityEditor = false;
        }
    }

    private void ApplySceneEnvironmentContinuityEditor()
    {
        if (_isLoadingSceneEnvironmentContinuityEditor || SelectedWorldContext == null)
        {
            return;
        }

        SelectedWorldContext.SceneState ??= new RpSceneState();
        SelectedWorldContext.SceneState.Phase = Enum.TryParse<RpScenePhase>(ScenePhase, true, out var phase) ? phase : RpScenePhase.Setup;
        SelectedWorldContext.SceneState.EscalationBudget = Math.Max(0, SceneEscalationBudget);
        SelectedWorldContext.SceneState.EscalationRatePerTick = Math.Max(0, SceneEscalationRate);
        SelectedWorldContext.SceneState.ActiveThreads = ParseLines(SceneActiveThreadsText);
        SelectedWorldContext.SceneState.ForeshadowedElements = ParseLines(SceneForeshadowedText);
        SelectedWorldContext.SceneState.UnresolvedPromises = ParseLines(SceneUnresolvedPromisesText);
        SelectedWorldContext.SceneState.MajorActionPrerequisites = ParseLines(SceneMajorPrerequisitesText);

        SelectedWorldContext.EnvironmentRules ??= new RpEnvironmentRuleSet();
        SelectedWorldContext.EnvironmentRules.InteractiveObjects = ParseInteractiveObjects(EnvironmentInteractiveObjectsText);
        SelectedWorldContext.EnvironmentRules.EnvironmentalTells = ParseLines(EnvironmentTellsText);
        SelectedWorldContext.EnvironmentRules.Hazards = ParseLines(EnvironmentHazardsText);
        SelectedWorldContext.EnvironmentRules.TerrainAffordances = ParseLines(EnvironmentTerrainAffordancesText);
        SelectedWorldContext.EnvironmentRules.Clues = ParseLines(EnvironmentCluesText);
        SelectedWorldContext.EnvironmentRules.DomainOwnerAwarenessRules = ParseLines(EnvironmentDomainOwnerRulesText);

        SelectedWorldContext.Continuity ??= new RpContinuityState();
        SelectedWorldContext.Continuity.PersistentPhysicalChanges = ParseLines(ContinuityPhysicalChangesText);
        SelectedWorldContext.Continuity.EmotionalStateChanges = ParseLines(ContinuityEmotionalChangesText);
        SelectedWorldContext.Continuity.RelationshipChanges = ParseLines(ContinuityRelationshipChangesText);
        SelectedWorldContext.Continuity.Flags = ParseLines(ContinuityFlagsText);
        SelectedWorldContext.Continuity.Triggers = ParseLines(ContinuityTriggersText);
        SelectedWorldContext.Continuity.IrreversibleEvents = ParseLines(ContinuityIrreversibleEventsText);
        SelectedWorldContext.Continuity.PendingConsequences = ParseLines(ContinuityPendingConsequencesText);
    }

    private Character CreateCharacterFromProfile(RpWorldContextCharacter profile, Vec3Int position)
        => new RpCharacterCompositionService().CreateCharacterFromProfile(World, SelectedWorldContext, profile, position);

    private Vec3Int FindSpawnPosition()
    {
        var origin = PlayerCharacter?.Position ?? SelectedCharacter?.Position ?? new Vec3Int(0, 0, 0);
        var candidates = new[] { origin }
            .Concat(origin.Neighbors())
            .Concat(World.Tiles.Keys.OrderBy(pos => Math.Abs(pos.X - origin.X) + Math.Abs(pos.Y - origin.Y) + Math.Abs(pos.Z - origin.Z)));

        foreach (var position in candidates)
        {
            if (!World.Tiles.TryGetValue(position, out var tile) ||
                tile.Solidity == TileSolidity.Solid ||
                tile.OccupantIds.Any(id => World.Characters.ContainsKey(id)))
            {
                continue;
            }

            return position;
        }

        return origin;
    }

    private static string FormatLines(IEnumerable<string>? lines)
        => lines == null ? string.Empty : string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrWhiteSpace(line)));

    private static List<string> ParseLines(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string FormatDictionary(Dictionary<string, string>? values)
        => values == null || values.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, values.Select(pair => $"{pair.Key} = {pair.Value}"));

    private static Dictionary<string, string> ParseDictionary(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in ParseLines(value))
        {
            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                separator = line.IndexOf(':');
            }

            if (separator < 0)
            {
                result[line] = string.Empty;
                continue;
            }

            var key = line[..separator].Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            result[key] = line[(separator + 1)..].Trim();
        }

        return result;
    }

    private static string FormatInteractiveObjects(IEnumerable<RpInteractiveObjectRule>? rules)
        => rules == null
            ? string.Empty
            : string.Join(Environment.NewLine, rules.Select(rule =>
                $"{rule.Name} | {rule.AppliesToTileOrTag} | {rule.Interaction} | {rule.WorldEffect} | {rule.NarrativeEffect} | {rule.Constraints}"));

    private static string FormatFactionRoles(IEnumerable<RpFactionRole>? roles)
        => new RpWorldContextEditorService().FormatFactionRoles(roles);

    private static string FormatAbilities(IEnumerable<RpAbility>? abilities)
        => abilities == null
            ? string.Empty
            : string.Join(Environment.NewLine, abilities.Select(ability =>
                $"{ability.Id} | {ability.Name} | {ability.TargetKind} | {ability.PrimaryResource} | {ability.ManaCost:0.###} | {ability.FocusCost:0.###} | {ability.StaminaCost:0.###} | {ability.Damage:0.###} | {ability.Range} | {ability.TickCost} | {ability.CooldownTicks} | {ability.Description} | {string.Join(',', ability.Tags ?? [])}"));

    private static string FormatRelationshipRules(IEnumerable<RpRelationshipRule>? rules)
        => rules == null
            ? string.Empty
            : string.Join(Environment.NewLine, rules.Select(rule =>
                $"{rule.TargetNameOrTag} | {rule.Type} | {rule.Trust} | {rule.Fear} | {rule.Dependency} | {rule.Loyalty} | {rule.Manipulation} | {rule.Suspicion} | {string.Join(';', rule.KnownSecrets ?? [])} | {rule.HandlingRules}"));

    private static List<RpInteractiveObjectRule> ParseInteractiveObjects(string? value)
    {
        var result = new List<RpInteractiveObjectRule>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            result.Add(new RpInteractiveObjectRule
            {
                Name = parts.ElementAtOrDefault(0) ?? string.Empty,
                AppliesToTileOrTag = parts.ElementAtOrDefault(1) ?? string.Empty,
                Interaction = parts.ElementAtOrDefault(2) ?? string.Empty,
                WorldEffect = parts.ElementAtOrDefault(3) ?? string.Empty,
                NarrativeEffect = parts.ElementAtOrDefault(4) ?? string.Empty,
                Constraints = parts.ElementAtOrDefault(5) ?? string.Empty
            });
        }

        return result;
    }

    private static List<RpFactionRole> ParseFactionRoles(string? value)
        => new RpWorldContextEditorService().ParseFactionRoles(value);

    private static List<RpAbility> ParseAbilities(string? value)
    {
        var result = new List<RpAbility>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var name = parts.ElementAtOrDefault(1);
            result.Add(new RpAbility
            {
                Id = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? Slugify(name, "ability") : parts.ElementAtOrDefault(0) ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(name) ? "Ability" : name,
                TargetKind = Enum.TryParse<RpAbilityTargetKind>(parts.ElementAtOrDefault(2), true, out var targetKind) ? targetKind : RpAbilityTargetKind.Character,
                PrimaryResource = Enum.TryParse<RpAbilityResource>(parts.ElementAtOrDefault(3), true, out var resource) ? resource : RpAbilityResource.None,
                ManaCost = ParseFloat(parts.ElementAtOrDefault(4)),
                FocusCost = ParseFloat(parts.ElementAtOrDefault(5)),
                StaminaCost = ParseFloat(parts.ElementAtOrDefault(6)),
                Damage = ParseFloat(parts.ElementAtOrDefault(7)),
                Range = Math.Max(0, ParseInt(parts.ElementAtOrDefault(8), 1)),
                TickCost = Math.Max(1, ParseInt(parts.ElementAtOrDefault(9), 1)),
                CooldownTicks = Math.Max(0, ParseInt(parts.ElementAtOrDefault(10), 0)),
                Description = parts.ElementAtOrDefault(11) ?? string.Empty,
                Tags = SplitList(parts.ElementAtOrDefault(12))
            });
        }

        return result;
    }

    private static List<RpRelationshipRule> ParseRelationshipRules(string? value)
    {
        var result = new List<RpRelationshipRule>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            result.Add(new RpRelationshipRule
            {
                TargetNameOrTag = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? "target" : parts.ElementAtOrDefault(0) ?? "target",
                Type = Enum.TryParse<RpRelationshipType>(parts.ElementAtOrDefault(1), true, out var type) ? type : RpRelationshipType.Unknown,
                Trust = ParseInt(parts.ElementAtOrDefault(2), 0),
                Fear = ParseInt(parts.ElementAtOrDefault(3), 0),
                Dependency = ParseInt(parts.ElementAtOrDefault(4), 0),
                Loyalty = ParseInt(parts.ElementAtOrDefault(5), 0),
                Manipulation = ParseInt(parts.ElementAtOrDefault(6), 0),
                Suspicion = ParseInt(parts.ElementAtOrDefault(7), 0),
                KnownSecrets = SplitList(parts.ElementAtOrDefault(8)),
                HandlingRules = parts.ElementAtOrDefault(9) ?? string.Empty
            });
        }

        return result;
    }

    private static List<string> SplitList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var result) ? result : fallback;

    private static float ParseFloat(string? value)
        => float.TryParse(value, out var result) ? result : 0;

    private static string Slugify(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = source
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        while (slug.Contains("__", StringComparison.Ordinal))
        {
            slug = slug.Replace("__", "_", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
    }

    private void SyncRuntimeFaction(RpFactionProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FactionId))
        {
            return;
        }

        World.Factions ??= [];
        World.Factions[profile.FactionId] = new Faction
        {
            Id = profile.FactionId,
            Name = profile.Name,
            Description = profile.PublicDescription
        };
    }

    private void RefreshWorldContextPicker()
    {
        var selected = SelectedWorldContext;
        WorldContexts.Clear();
        foreach (var context in World.WorldContexts)
        {
            WorldContexts.Add(context);
        }

        SelectedWorldContext = selected != null && WorldContexts.Contains(selected)
            ? selected
            : WorldContexts.FirstOrDefault();
        RaiseWorldContextChanged();
    }

    private async Task AutoSaveWorldContextAsync(string reason)
    {
        try
        {
            await _worldSaveService.SaveAsync(World);
            RpSimulationService.AppendDebugLog($"World context auto-save complete | reason={reason} | path={_worldSaveService.DefaultSavePath}");
        }
        catch (Exception ex)
        {
            StatusText = $"World context changed, but auto-save failed: {ex.Message}";
            RpSimulationService.AppendDebugLog($"World context auto-save failed | reason={reason} | error={ex.Message}");
        }
    }

    private void RaisePlayerStatsChanged()
    {
        OnPropertyChanged(nameof(PlayerHealthText));
        OnPropertyChanged(nameof(PlayerManaText));
        OnPropertyChanged(nameof(PlayerFocusText));
        OnPropertyChanged(nameof(PlayerStaminaText));
        OnPropertyChanged(nameof(PlayerHealthProgress));
        OnPropertyChanged(nameof(PlayerManaProgress));
        OnPropertyChanged(nameof(PlayerFocusProgress));
        OnPropertyChanged(nameof(PlayerStaminaProgress));
    }

    private void UpdateSelectedCharacterSummary()
    {
        SelectedCharacterSummary = SelectedCharacter == null
            ? "No character selected."
            : $"{SelectedCharacter.Name} ({SelectedCharacter.Race}) at {SelectedCharacter.Position}\nMood: {SelectedCharacter.Mood}\nState: {SelectedCharacter.Vitals.LifeState}, HP {SelectedCharacter.Vitals.HealthCurrent:0.#}/{SelectedCharacter.Vitals.HealthMax:0.#}, Mana {SelectedCharacter.Vitals.ManaCurrent:0.#}/{SelectedCharacter.Vitals.ManaMax:0.#}, Focus {SelectedCharacter.Vitals.FocusCurrent:0.#}/{SelectedCharacter.Vitals.FocusMax:0.#}, Stamina {SelectedCharacter.Vitals.StaminaCurrent:0.#}/{SelectedCharacter.Vitals.StaminaMax:0.#}\nTags: {FormatTags(SelectedCharacter)}\nAbilities: {FormatAbilities(SelectedCharacter)}\nGoal: {SelectedCharacter.CurrentGoal.Description}\nQueue: {SelectedCharacter.ActionQueue.Count}\n{RpBodyFactory.DescribeBody(SelectedCharacter)}";
    }

    private static string FormatAbilities(Character character)
        => character.RpAbilities.Count == 0
            ? "none"
            : string.Join(", ", character.RpAbilities.Select(ability => $"{ability.Name} ({ability.ManaCost:0.#} mana)"));

    private static string FormatTags(Character character)
        => character.RpTags.Count == 0 ? "none" : string.Join(", ", character.RpTags);

    private static double Ratio(float? current, float? max)
    {
        var maxValue = max.GetValueOrDefault();
        return maxValue <= 0 ? 0 : Math.Clamp(current.GetValueOrDefault() / maxValue, 0, 1);
    }

    private string GetCurrentProviderApiKey()
        => (_settingsService.AiProvider switch
        {
            "OpenAIProxy" => _settingsService.OpenAiProxyApiKey?.Trim(),
            "ZAI" => _settingsService.ZaiApiKey?.Trim(),
            "Chutes" => _settingsService.ChutesApiKey?.Trim(),
            _ => _settingsService.OpenRouterApiKey?.Trim()
        }) ?? string.Empty;

    private void SeedDefaultModel()
    {
        if (CachedRpModels.Count == 0)
        {
            CachedRpModels.Add(CreateDefaultRpModel());
        }

        ApplyModelList(CachedRpModels);
    }

    private void ApplyModelList(IEnumerable<Services.AiModel> models)
    {
        AvailableModels.Clear();
        foreach (var model in EnsureDefaultModel(models))
        {
            AvailableModels.Add(model);
        }

        EnsureDefaultModelSelected();
    }

    private void EnsureDefaultModelSelected()
    {
        if (SelectedModel == null || IsPreviousDefaultRpModel(SelectedModel))
        {
            SelectedModel = AvailableModels.FirstOrDefault(model => IsDefaultRpModel(model)) ?? AvailableModels.FirstOrDefault();
        }
    }

    private static List<Services.AiModel> EnsureDefaultModel(IEnumerable<Services.AiModel> models)
    {
        var result = models
            .Where(model => !string.IsNullOrWhiteSpace(model.Id) || !string.IsNullOrWhiteSpace(model.Name))
            .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (!result.Any(IsDefaultRpModel))
        {
            result.Insert(0, CreateDefaultRpModel());
        }

        return result;
    }

    private static Services.AiModel CreateDefaultRpModel()
        => new()
        {
            Id = DefaultRpModelId,
            Name = DefaultRpModelName,
            Provider = "Chutes"
        };

    private static bool IsDefaultRpModel(Services.AiModel model)
        => ContainsDefaultRpModel(model.Id) || ContainsDefaultRpModel(model.Name);

    private static bool IsPreviousDefaultRpModel(Services.AiModel model)
        => string.Equals(model.Id, PreviousDefaultRpModelId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model.Name, PreviousDefaultRpModelId, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsDefaultRpModel(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
            value.Contains(DefaultRpModelId, StringComparison.OrdinalIgnoreCase);

    private static string FormatEvent(NarrativeEvent evt)
        => $"T{evt.Tick} {evt.ActorName}: {evt.Description}";

    private Task MovePlayer(Direction direction)
    {
        if (IsBusy)
        {
            StatusText = "Wait for the current AI/world turn to finish.";
            return Task.CompletedTask;
        }

        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return Task.CompletedTask;
        }

        var target = PlayerCharacter.Position + RpSimulationService.OffsetFor(direction);
        return TryMovePlayerTo(target, direction.ToString());
    }

    public async Task TryMovePlayerTo(Vec3Int target, string directionOrReason)
    {
        if (IsBusy)
        {
            StatusText = "Wait for the current AI/world turn to finish.";
            return;
        }

        if (PlayerCharacter == null)
        {
            StatusText = "Choose a player character first.";
            return;
        }

        if (!RpSimulationService.TryMoveCharacter(World, PlayerCharacter, target, out var error))
        {
            StatusText = error;
            return;
        }

        SelectedCharacter = PlayerCharacter;
        if (SliceMode == RpSliceMode.Horizontal)
        {
            SliceCoordinate = PlayerCharacter.Position.Y;
        }
        else
        {
            SliceCoordinate = PlayerCharacter.Position.Z;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} moved {directionOrReason} to {PlayerCharacter.Position}."
        });
        StatusText = $"{PlayerCharacter.Name} moved to {PlayerCharacter.Position}.";
        RefreshInventory();
        RefreshGroundItems();
        RaiseWorldChanged();
        await AdvanceWorldAfterPlayerActionAsync(StatusText);
    }

    private void RefreshGroundItems()
    {
        GroundItems.Clear();
        foreach (var item in _inventoryService.GetGroundItems(World, SelectedTilePosition))
        {
            GroundItems.Add(item);
        }

        if (SelectedGroundItem == null || !GroundItems.Contains(SelectedGroundItem))
        {
            SelectedGroundItem = GroundItems.FirstOrDefault();
        }
    }

    private void RefreshInventory()
    {
        InventoryItems.Clear();
        if (PlayerCharacter == null)
        {
            InventorySummary = _inventoryService.GetInventorySummary(World, PlayerCharacter);
            SelectedInventoryItem = null;
            return;
        }

        foreach (var item in _inventoryService.GetInventoryItems(World, PlayerCharacter))
        {
            InventoryItems.Add(item);
        }

        if (SelectedInventoryItem == null || !InventoryItems.Contains(SelectedInventoryItem))
        {
            SelectedInventoryItem = InventoryItems.FirstOrDefault();
        }

        InventorySummary = _inventoryService.GetInventorySummary(World, PlayerCharacter);
    }

    private static bool KeyMatches(string key, string configured, params string[] aliases)
    {
        var configuredNormalized = NormalizeKeyName(configured, string.Empty);
        return string.Equals(key, configuredNormalized, StringComparison.OrdinalIgnoreCase) ||
            aliases.Any(alias => string.Equals(key, NormalizeKeyName(alias, alias), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKeyName(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Equals("PgUp", StringComparison.OrdinalIgnoreCase) ? "PageUp" :
            normalized.Equals("PgDn", StringComparison.OrdinalIgnoreCase) ? "PageDown" :
            normalized.Equals("Esc", StringComparison.OrdinalIgnoreCase) ? "Escape" :
            normalized.Length == 1 ? normalized.ToUpperInvariant() :
            normalized;
    }

    private void AddEventIfAny(NarrativeEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.Description))
        {
            AddEventToAll(evt);
        }
    }

    private void AddEventToAll(NarrativeEvent evt)
    {
        foreach (var character in World.Characters.Values)
        {
            character.PerceivedLog.Add(evt);
            if (character.PerceivedLog.Count > 25)
            {
                character.PerceivedLog = character.PerceivedLog.TakeLast(25).ToList();
            }
        }

        EventLog.Insert(0, FormatEvent(evt));
        TrimEventLog();
    }

    private void TrimEventLog()
    {
        while (EventLog.Count > 80)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }
}

internal sealed record RpContextSectionTemplate(
    string Name,
    RpContextModuleType Type,
    RpContextVisibility Visibility,
    string Text);
