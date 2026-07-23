using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Top-level coordinator for world context editing. Composes the five child editors.
/// </summary>
public sealed partial class WorldContextEditorViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpWorldSaveService _worldSaveService;
    private readonly RpWorldContextEditorService _contextEditorService;
    private readonly RpAuthoringAssistantService _authoringAssistant;
    private bool _isLoadingWorldContextEditor;

    [ObservableProperty] private RpWorldContextEntry? _selectedWorldContext;
    [ObservableProperty] private string _worldContextName = string.Empty;
    [ObservableProperty] private bool _worldContextIsEnabled;
    [ObservableProperty] private string _worldContextRulesText = string.Empty;

    public ObservableCollection<RpWorldContextEntry> WorldContexts { get; } = [];

    public ContextModuleEditorViewModel ModuleEditor { get; }
    public ContextCharacterEditorViewModel CharacterEditor { get; }
    public FactionProfileEditorViewModel FactionEditor { get; }
    public SpeciesTemplateEditorViewModel SpeciesEditor { get; }
    public SceneEnvironmentContinuityEditorViewModel SceneEnvironmentEditor { get; }

    public string ActiveWorldContextSummary => $"{World.WorldContexts.Count(context => context.IsEnabled)} active / {World.WorldContexts.Count} total";

    public WorldContextEditorViewModel(
        WorldSimulationViewModel simulation,
        RpWorldSaveService worldSaveService,
        RpWorldContextEditorService contextEditorService,
        RpAuthoringAssistantService authoringAssistant)
    {
        _simulation = simulation;
        _worldSaveService = worldSaveService;
        _contextEditorService = contextEditorService;
        _authoringAssistant = authoringAssistant;

        ModuleEditor = new ContextModuleEditorViewModel(simulation, authoringAssistant);
        CharacterEditor = new ContextCharacterEditorViewModel(simulation, null!, worldSaveService, authoringAssistant);
        FactionEditor = new FactionProfileEditorViewModel(simulation, authoringAssistant);
        SpeciesEditor = new SpeciesTemplateEditorViewModel(simulation, authoringAssistant);
        SceneEnvironmentEditor = new SceneEnvironmentContinuityEditorViewModel();
    }

    public World World => _simulation.World;

    public void RefreshWorldContextPicker()
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

    private void RaiseWorldContextChanged()
    {
        OnPropertyChanged(nameof(ActiveWorldContextSummary));
        OnPropertyChanged(nameof(SelectedWorldContext));
        OnPropertyChanged(nameof(WorldContexts));
    }

    public void LoadWorldContextEditor(RpWorldContextEntry? context)
    {
        _isLoadingWorldContextEditor = true;
        try
        {
            SelectedWorldContext = context;
            WorldContextName = context?.Name ?? string.Empty;
            WorldContextIsEnabled = context?.IsEnabled ?? false;
            WorldContextRulesText = context?.RulesText ?? string.Empty;

            ModuleEditor.RefreshContextModules(context?.Modules);
            CharacterEditor.RefreshContextCharacters(context?.Characters);
            SpeciesEditor.RefreshSpeciesTemplates(context?.SpeciesTemplates);
            FactionEditor.RefreshFactionProfiles(context?.Factions);
            SceneEnvironmentEditor.LoadSceneEnvironmentContinuityEditor(context);
        }
        finally
        {
            _isLoadingWorldContextEditor = false;
        }
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
        _simulation.StatusText = $"Added world context: {context.Name}.";
        RpSimulationService.AppendDebugLog($"World context added | id={context.Id} | name={context.Name}");
        await AutoSaveWorldContextAsync("add");
    }

    [RelayCommand]
    public async Task DeleteSelectedWorldContext()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        var removed = SelectedWorldContext;
        World.WorldContexts.Remove(removed);
        WorldContexts.Remove(removed);
        SelectedWorldContext = WorldContexts.FirstOrDefault();
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Deleted world context: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"World context deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("delete");
    }

    [RelayCommand]
    public async Task SaveWorldContextEdits()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        SelectedWorldContext.Name = string.IsNullOrWhiteSpace(WorldContextName) ? "World Context" : WorldContextName.Trim();
        SelectedWorldContext.IsEnabled = WorldContextIsEnabled;
        SelectedWorldContext.RulesText = WorldContextRulesText;
        RefreshWorldContextPicker();
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Saved world context: {SelectedWorldContext.Name}.";
        RpSimulationService.AppendDebugLog($"World context saved | id={SelectedWorldContext.Id} | name={SelectedWorldContext.Name} | enabled={SelectedWorldContext.IsEnabled} | rulesLength={SelectedWorldContext.RulesText?.Length ?? 0}");
        await AutoSaveWorldContextAsync("save");
    }

    [RelayCommand]
    public async Task AddContextModule()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
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
        ModuleEditor.RefreshContextModules(SelectedWorldContext.Modules);
        _simulation.StatusText = $"Added context module: {module.Name}.";
        RpSimulationService.AppendDebugLog($"Context module added | context={SelectedWorldContext.Name} | id={module.Id} | name={module.Name}");
        await AutoSaveWorldContextAsync("module-add");
    }

    [RelayCommand]
    public async Task ScaffoldStandardContextSections()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
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
            if (existingNames.Contains(template.Name)) continue;

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

        ModuleEditor.RefreshContextModules(SelectedWorldContext.Modules);
        RaiseWorldContextChanged();
        _simulation.StatusText = added == 0
            ? "Standard context sections already exist."
            : $"Added {added} standard context section(s).";
        RpSimulationService.AppendDebugLog($"Standard context sections scaffolded | context={SelectedWorldContext.Name} | added={added}");
        await AutoSaveWorldContextAsync("module-scaffold-standard");
    }

    [RelayCommand]
    public async Task DuplicateSelectedContextModule()
    {
        if (SelectedWorldContext == null || ModuleEditor.SelectedContextModule == null)
        {
            _simulation.StatusText = "Select a context module first.";
            return;
        }

        var source = ModuleEditor.SelectedContextModule;
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
        ModuleEditor.RefreshContextModules(SelectedWorldContext.Modules);
        _simulation.StatusText = $"Duplicated context module: {clone.Name}.";
        RpSimulationService.AppendDebugLog($"Context module duplicated | source={source.Id} | clone={clone.Id} | name={clone.Name}");
        await AutoSaveWorldContextAsync("module-duplicate");
    }

    [RelayCommand]
    public async Task CreateModuleFromLegacyContext()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(WorldContextRulesText))
        {
            _simulation.StatusText = "Legacy context text is empty.";
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
        ModuleEditor.RefreshContextModules(SelectedWorldContext.Modules);
        _simulation.StatusText = $"Created module from legacy context text: {module.Name}.";
        RpSimulationService.AppendDebugLog($"Context module created from legacy text | context={SelectedWorldContext.Name} | id={module.Id} | length={module.Text.Length}");
        await AutoSaveWorldContextAsync("module-from-legacy");
    }

    [RelayCommand]
    public async Task ClearLegacyContextText()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        SelectedWorldContext.RulesText = string.Empty;
        WorldContextRulesText = string.Empty;
        RaiseWorldContextChanged();
        _simulation.StatusText = "Cleared legacy context text. Enabled modules remain active.";
        RpSimulationService.AppendDebugLog($"Legacy context text cleared | context={SelectedWorldContext.Name}");
        await AutoSaveWorldContextAsync("legacy-clear");
    }

    [RelayCommand]
    public async Task SaveContextModule()
    {
        if (ModuleEditor.SelectedContextModule == null)
        {
            _simulation.StatusText = "Select a context module first.";
            return;
        }

        ModuleEditor.ApplyContextModuleEditor();
        ModuleEditor.RefreshContextModules(SelectedWorldContext?.Modules);
        _simulation.StatusText = $"Saved context module: {ModuleEditor.SelectedContextModule.Name}.";
        RpSimulationService.AppendDebugLog($"Context module saved | id={ModuleEditor.SelectedContextModule.Id} | name={ModuleEditor.SelectedContextModule.Name} | type={ModuleEditor.SelectedContextModule.Type} | visibility={ModuleEditor.SelectedContextModule.Visibility} | appliesTo={ModuleEditor.SelectedContextModule.AppliesTo} | length={ModuleEditor.SelectedContextModule.Text?.Length ?? 0}");
        await AutoSaveWorldContextAsync("module-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextModule()
    {
        if (SelectedWorldContext == null || ModuleEditor.SelectedContextModule == null)
        {
            _simulation.StatusText = "Select a context module first.";
            return;
        }

        var removed = ModuleEditor.SelectedContextModule;
        SelectedWorldContext.Modules.Remove(removed);
        ModuleEditor.RefreshContextModules(SelectedWorldContext.Modules);
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Deleted context module: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"Context module deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("module-delete");
    }

    [RelayCommand]
    public async Task AddContextCharacter()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
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
        CharacterEditor.RefreshContextCharacters(SelectedWorldContext.Characters);
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Added context character: {profile.Name}.";
        RpSimulationService.AppendDebugLog($"Context character added | context={SelectedWorldContext.Name} | id={profile.Id} | name={profile.Name}");
        await AutoSaveWorldContextAsync("character-add");
    }

    [RelayCommand]
    public async Task SaveContextCharacter()
    {
        if (CharacterEditor.SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        CharacterEditor.ApplyContextCharacterEditor();
        CharacterEditor.RefreshContextCharacters(SelectedWorldContext?.Characters);
        _simulation.StatusText = $"Saved context character: {CharacterEditor.SelectedContextCharacter.Name}.";
        RpSimulationService.AppendDebugLog($"Context character saved | id={CharacterEditor.SelectedContextCharacter.Id} | name={CharacterEditor.SelectedContextCharacter.Name} | named={CharacterEditor.SelectedContextCharacter.IsNamedCharacter} | visibility={CharacterEditor.SelectedContextCharacter.Visibility} | appliesTo={CharacterEditor.SelectedContextCharacter.AppliesTo}");
        await AutoSaveWorldContextAsync("character-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextCharacter()
    {
        if (SelectedWorldContext == null || CharacterEditor.SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        var removed = CharacterEditor.SelectedContextCharacter;
        SelectedWorldContext.Characters.Remove(removed);
        CharacterEditor.RefreshContextCharacters(SelectedWorldContext.Characters);
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Deleted context character: {removed.Name}.";
        RpSimulationService.AppendDebugLog($"Context character deleted | id={removed.Id} | name={removed.Name}");
        await AutoSaveWorldContextAsync("character-delete");
    }

    [RelayCommand]
    public async Task AddFactionProfile()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        var profile = _contextEditorService.AddFactionProfile(SelectedWorldContext);
        FactionEditor.SyncRuntimeFaction(profile, World);
        FactionEditor.RefreshFactionProfiles(SelectedWorldContext.Factions);
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Added faction profile: {profile.Name}.";
        await AutoSaveWorldContextAsync("faction-add");
    }

    [RelayCommand]
    public async Task SaveFactionProfile()
    {
        if (FactionEditor.SelectedFactionProfile == null)
        {
            _simulation.StatusText = "Select a faction profile first.";
            return;
        }

        FactionEditor.ApplyFactionProfileEditor();
        FactionEditor.SyncRuntimeFaction(FactionEditor.SelectedFactionProfile, World);
        FactionEditor.RefreshFactionProfiles(SelectedWorldContext?.Factions);
        _simulation.StatusText = $"Saved faction profile: {FactionEditor.SelectedFactionProfile.Name}.";
        await AutoSaveWorldContextAsync("faction-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedFactionProfile()
    {
        if (SelectedWorldContext == null || FactionEditor.SelectedFactionProfile == null)
        {
            _simulation.StatusText = "Select a faction profile first.";
            return;
        }

        var removed = FactionEditor.SelectedFactionProfile;
        SelectedWorldContext.Factions.Remove(removed);
        FactionEditor.RefreshFactionProfiles(SelectedWorldContext.Factions);
        RaiseWorldContextChanged();
        _simulation.StatusText = $"Deleted faction profile: {removed.Name}.";
        await AutoSaveWorldContextAsync("faction-delete");
    }

    [RelayCommand]
    public async Task AddSpeciesTemplate()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        var template = new RpSpeciesTemplate
        {
            Name = $"Species {SelectedWorldContext.SpeciesTemplates.Count + 1}",
            AppliesToRace = "Human",
            BodyType = BodyTypeKind.Human
        };
        SelectedWorldContext.SpeciesTemplates.Add(template);
        SpeciesEditor.RefreshSpeciesTemplates(SelectedWorldContext.SpeciesTemplates);
        _simulation.StatusText = $"Added species template: {template.Name}.";
        await AutoSaveWorldContextAsync("species-add");
    }

    [RelayCommand]
    public async Task SaveSpeciesTemplate()
    {
        if (SpeciesEditor.SelectedSpeciesTemplate == null)
        {
            _simulation.StatusText = "Select a species template first.";
            return;
        }

        SpeciesEditor.ApplySpeciesTemplateEditor();
        SpeciesEditor.RefreshSpeciesTemplates(SelectedWorldContext?.SpeciesTemplates);
        _simulation.StatusText = $"Saved species template: {SpeciesEditor.SelectedSpeciesTemplate.Name}.";
        await AutoSaveWorldContextAsync("species-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedSpeciesTemplate()
    {
        if (SelectedWorldContext == null || SpeciesEditor.SelectedSpeciesTemplate == null)
        {
            _simulation.StatusText = "Select a species template first.";
            return;
        }

        var removed = SpeciesEditor.SelectedSpeciesTemplate;
        SelectedWorldContext.SpeciesTemplates.Remove(removed);
        SpeciesEditor.RefreshSpeciesTemplates(SelectedWorldContext.SpeciesTemplates);
        _simulation.StatusText = $"Deleted species template: {removed.Name}.";
        await AutoSaveWorldContextAsync("species-delete");
    }

    [RelayCommand]
    public async Task AddContextAbility()
    {
        if (CharacterEditor.SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        var ability = new RpAbility
        {
            Id = $"ability_{CharacterEditor.SelectedContextCharacter.StructuredAbilities.Count + 1}",
            Name = $"Ability {CharacterEditor.SelectedContextCharacter.StructuredAbilities.Count + 1}",
            TargetKind = RpAbilityTargetKind.Character,
            PrimaryResource = RpAbilityResource.None,
            Range = 1,
            TickCost = 1
        };
        CharacterEditor.SelectedContextCharacter.StructuredAbilities.Add(ability);
        CharacterEditor.AbilityEditor.RefreshContextAbilities(CharacterEditor.SelectedContextCharacter.StructuredAbilities);
        _simulation.StatusText = $"Added ability: {ability.Name}.";
        await AutoSaveWorldContextAsync("ability-add");
    }

    [RelayCommand]
    public async Task SaveContextAbility()
    {
        if (CharacterEditor.AbilityEditor.SelectedContextAbility == null)
        {
            _simulation.StatusText = "Select an ability first.";
            return;
        }

        CharacterEditor.AbilityEditor.ApplyContextAbilityEditor();
        CharacterEditor.AbilityEditor.RefreshContextAbilities(CharacterEditor.SelectedContextCharacter?.StructuredAbilities);
        _simulation.StatusText = $"Saved ability: {CharacterEditor.AbilityEditor.SelectedContextAbility.Name}.";
        await AutoSaveWorldContextAsync("ability-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedContextAbility()
    {
        if (CharacterEditor.SelectedContextCharacter == null || CharacterEditor.AbilityEditor.SelectedContextAbility == null)
        {
            _simulation.StatusText = "Select an ability first.";
            return;
        }

        var removed = CharacterEditor.AbilityEditor.SelectedContextAbility;
        CharacterEditor.SelectedContextCharacter.StructuredAbilities.Remove(removed);
        CharacterEditor.AbilityEditor.RefreshContextAbilities(CharacterEditor.SelectedContextCharacter.StructuredAbilities);
        _simulation.StatusText = $"Deleted ability: {removed.Name}.";
        await AutoSaveWorldContextAsync("ability-delete");
    }

    [RelayCommand]
    public async Task AddRelationshipRule()
    {
        if (CharacterEditor.SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        var rule = new RpRelationshipRule
        {
            TargetNameOrTag = "target",
            Type = RpRelationshipType.Unknown
        };
        CharacterEditor.SelectedContextCharacter.RelationshipRules.Add(rule);
        CharacterEditor.RelationshipRuleEditor.RefreshRelationshipRules(CharacterEditor.SelectedContextCharacter.RelationshipRules);
        _simulation.StatusText = "Added relationship rule.";
        await AutoSaveWorldContextAsync("relationship-add");
    }

    [RelayCommand]
    public async Task SaveRelationshipRule()
    {
        if (CharacterEditor.RelationshipRuleEditor.SelectedRelationshipRule == null)
        {
            _simulation.StatusText = "Select a relationship rule first.";
            return;
        }

        CharacterEditor.RelationshipRuleEditor.ApplyRelationshipRuleEditor();
        CharacterEditor.RelationshipRuleEditor.RefreshRelationshipRules(CharacterEditor.SelectedContextCharacter?.RelationshipRules);
        _simulation.StatusText = $"Saved relationship rule for {CharacterEditor.RelationshipRuleEditor.SelectedRelationshipRule.TargetNameOrTag}.";
        await AutoSaveWorldContextAsync("relationship-save");
    }

    [RelayCommand]
    public async Task DeleteSelectedRelationshipRule()
    {
        if (CharacterEditor.SelectedContextCharacter == null || CharacterEditor.RelationshipRuleEditor.SelectedRelationshipRule == null)
        {
            _simulation.StatusText = "Select a relationship rule first.";
            return;
        }

        var removed = CharacterEditor.RelationshipRuleEditor.SelectedRelationshipRule;
        CharacterEditor.SelectedContextCharacter.RelationshipRules.Remove(removed);
        CharacterEditor.RelationshipRuleEditor.RefreshRelationshipRules(CharacterEditor.SelectedContextCharacter.RelationshipRules);
        _simulation.StatusText = $"Deleted relationship rule for {removed.TargetNameOrTag}.";
        await AutoSaveWorldContextAsync("relationship-delete");
    }

    [RelayCommand]
    public async Task SaveSceneEnvironmentContinuity()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        SceneEnvironmentEditor.ApplySceneEnvironmentContinuityEditor(SelectedWorldContext);
        _simulation.StatusText = "Saved scene, environment, and continuity state.";
        await AutoSaveWorldContextAsync("scene-environment-continuity-save");
    }

    [RelayCommand]
    public async Task CreateContextCharacterFromSelected()
    {
        if (SelectedWorldContext == null)
        {
            _simulation.StatusText = "Select a world context first.";
            return;
        }

        if (_simulation.SelectedCharacter == null)
        {
            _simulation.StatusText = "Select a world character first.";
            return;
        }

        var profile = new RpWorldContextCharacter
        {
            Name = _simulation.SelectedCharacter.Name,
            IsNamedCharacter = true,
            Visibility = RpContextVisibility.WorldOnly,
            Archetype = _simulation.SelectedCharacter.Race,
            Race = _simulation.SelectedCharacter.Race,
            BodyType = _simulation.SelectedCharacter.BodyType,
            FactionId = _simulation.SelectedCharacter.FactionId ?? string.Empty,
            RoleInWorld = _simulation.SelectedCharacter.CurrentGoal.Description,
            PersonalityText = string.Join(", ", _simulation.SelectedCharacter.PersonalityTraits),
            StoryText = string.Join(Environment.NewLine, _simulation.SelectedCharacter.Ideals.Concat(_simulation.SelectedCharacter.Desires)),
            AbilityText = string.Join(", ", _simulation.SelectedCharacter.Abilities.Concat(_simulation.SelectedCharacter.RpAbilities.Select(ability => ability.Name))),
            GoalText = _simulation.SelectedCharacter.CurrentGoal.Description,
            LifeGoalText = _simulation.SelectedCharacter.LifeGoal.Description,
            TagsText = string.Join(", ", _simulation.SelectedCharacter.RpTags),
            StructuredAbilities = _simulation.SelectedCharacter.RpAbilities
        };
        SelectedWorldContext.Characters.Add(profile);
        CharacterEditor.RefreshContextCharacters(SelectedWorldContext.Characters);
        _simulation.StatusText = $"Created context profile from {_simulation.SelectedCharacter.Name}.";
        await AutoSaveWorldContextAsync("character-from-world");
    }

    [RelayCommand]
    public async Task SpawnSelectedContextCharacter()
    {
        if (CharacterEditor.SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        await CharacterEditor.SpawnSelectedContextCharacter();
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
            _simulation.StatusText = $"World context changed, but auto-save failed: {ex.Message}";
            RpSimulationService.AppendDebugLog($"World context auto-save failed | reason={reason} | error={ex.Message}");
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
}

internal sealed record RpContextSectionTemplate(
    string Name,
    RpContextModuleType Type,
    RpContextVisibility Visibility,
    string Text);
