using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single context character profile. Owns ability and relationship rule sub-editors.
/// </summary>
public sealed partial class ContextCharacterEditorViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpCharacterCompositionService _composition;
    private readonly RpWorldSaveService _worldSaveService;
    private readonly RpAuthoringAssistantService _authoringAssistant;
    private bool _isLoading;

    [ObservableProperty] private string _aiIdeaPrompt = string.Empty;
    [ObservableProperty] private bool _isDrafting;
    [ObservableProperty] private string _aiDraftStatus = string.Empty;

    [ObservableProperty] private RpWorldContextCharacter? _selectedContextCharacter;

    [ObservableProperty] private string _contextCharacterName = string.Empty;
    [ObservableProperty] private bool _contextCharacterIsNamed;
    [ObservableProperty] private string _contextCharacterVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private string _contextCharacterAppliesTo = string.Empty;
    [ObservableProperty] private string _contextCharacterArchetype = "regular";
    [ObservableProperty] private string _contextCharacterRace = "Human";
    [ObservableProperty] private string _contextCharacterBodyType = BodyTypeKind.Human.ToString();
    [ObservableProperty] private string _contextCharacterFactionId = string.Empty;
    [ObservableProperty] private string _contextCharacterRole = string.Empty;
    [ObservableProperty] private string _contextCharacterPersonality = string.Empty;
    [ObservableProperty] private string _contextCharacterStory = string.Empty;
    [ObservableProperty] private string _contextCharacterAbilities = string.Empty;
    [ObservableProperty] private string _contextCharacterGoal = "Wait and observe.";
    [ObservableProperty] private string _contextCharacterLifeGoal = string.Empty;
    [ObservableProperty] private string _contextCharacterTags = "creature, sapient";
    [ObservableProperty] private string _contextCharacterSpeechStyle = string.Empty;
    [ObservableProperty] private string _contextCharacterFirstEncounter = string.Empty;
    [ObservableProperty] private string _contextCharacterNegotiation = string.Empty;
    [ObservableProperty] private string _contextCharacterEscalation = string.Empty;
    [ObservableProperty] private string _contextCharacterDeEscalation = string.Empty;
    [ObservableProperty] private string _contextCharacterRelationshipHandling = string.Empty;
    [ObservableProperty] private string _contextCharacterDeception = string.Empty;
    [ObservableProperty] private string _contextCharacterCombatPreferences = string.Empty;
    [ObservableProperty] private string _contextCharacterCapturePreferences = string.Empty;

    public ObservableCollection<RpWorldContextCharacter> ContextCharacters { get; } = [];

    public ContextAbilityEditorViewModel AbilityEditor { get; }
    public RelationshipRuleEditorViewModel RelationshipRuleEditor { get; }

    public string ActiveContextCharacterSummary => SelectedContextCharacter == null
        ? "No character profile selected"
        : $"{SelectedContextCharacter.Name} ({SelectedContextCharacter.Race})";

    public ContextCharacterEditorViewModel(
        WorldSimulationViewModel simulation,
        RpCharacterCompositionService composition,
        RpWorldSaveService worldSaveService,
        RpAuthoringAssistantService authoringAssistant)
    {
        _simulation = simulation;
        _composition = composition;
        _worldSaveService = worldSaveService;
        _authoringAssistant = authoringAssistant;
        AbilityEditor = new ContextAbilityEditorViewModel();
        RelationshipRuleEditor = new RelationshipRuleEditorViewModel(simulation, authoringAssistant);
    }

    public void RefreshContextCharacters(List<RpWorldContextCharacter>? characters)
    {
        ContextCharacters.Clear();
        if (characters != null)
        {
            foreach (var character in characters)
            {
                ContextCharacters.Add(character);
            }
        }
        SelectedContextCharacter = ContextCharacters.FirstOrDefault();
    }

    public void LoadContextCharacterEditor(RpWorldContextCharacter? profile)
    {
        _isLoading = true;
        try
        {
            SelectedContextCharacter = profile;
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

            AbilityEditor.LoadContextAbilityEditor(null);
            RelationshipRuleEditor.LoadRelationshipRuleEditor(null);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ApplyContextCharacterEditor()
    {
        if (_isLoading || SelectedContextCharacter == null) return;

        SelectedContextCharacter.Name = string.IsNullOrWhiteSpace(ContextCharacterName) ? "New Character" : ContextCharacterName.Trim();
        SelectedContextCharacter.IsNamedCharacter = ContextCharacterIsNamed;
        SelectedContextCharacter.Visibility = Enum.TryParse<RpContextVisibility>(ContextCharacterVisibility, true, out var visibility)
            ? visibility : RpContextVisibility.WorldOnly;
        SelectedContextCharacter.AppliesTo = ContextCharacterAppliesTo.Trim();
        SelectedContextCharacter.Archetype = string.IsNullOrWhiteSpace(ContextCharacterArchetype) ? "regular" : ContextCharacterArchetype.Trim();
        SelectedContextCharacter.Race = string.IsNullOrWhiteSpace(ContextCharacterRace) ? "Human" : ContextCharacterRace.Trim();
        SelectedContextCharacter.BodyType = Enum.TryParse<BodyTypeKind>(ContextCharacterBodyType, true, out var bodyType)
            ? bodyType : BodyTypeKind.Human;
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

    public async Task SpawnSelectedContextCharacter()
    {
        if (SelectedContextCharacter == null)
        {
            _simulation.StatusText = "Select a context character first.";
            return;
        }

        var spawnPosition = FindSpawnPosition();
        var character = _composition.CreateCharacterFromProfile(_simulation.World, _simulation.World.WorldContexts.FirstOrDefault(), SelectedContextCharacter, spawnPosition);
        _simulation.World.Characters[character.Id] = character;
        if (_simulation.World.Tiles.TryGetValue(character.Position, out var tile) && !tile.OccupantIds.Contains(character.Id))
        {
            tile.OccupantIds.Add(character.Id);
        }

        RpSimulationService.UpdatePerception(_simulation.World);
        _simulation.Characters.Add(character);
        _simulation.SelectedCharacter = character;
        _simulation.RaiseWorldChanged();
        _simulation.StatusText = $"Spawned {character.Name} at {character.Position}.";
        RpSimulationService.AppendDebugLog($"Context character spawned | profile={SelectedContextCharacter.Id} | character={character.Id} | name={character.Name} | position={character.Position}");
        await _worldSaveService.SaveAsync(_simulation.World);
    }

    private Vec3Int FindSpawnPosition()
    {
        var origin = _simulation.PlayerCharacter?.Position ?? _simulation.SelectedCharacter?.Position ?? new Vec3Int(0, 0, 0);
        var candidates = new[] { origin }
            .Concat(origin.Neighbors())
            .Concat(_simulation.World.Tiles.Keys.OrderBy(pos => Math.Abs(pos.X - origin.X) + Math.Abs(pos.Y - origin.Y) + Math.Abs(pos.Z - origin.Z)));

        foreach (var position in candidates)
        {
            if (!_simulation.World.Tiles.TryGetValue(position, out var tile) ||
                tile.Solidity == TileSolidity.Solid ||
                tile.OccupantIds.Any(id => _simulation.World.Characters.ContainsKey(id)))
            {
                continue;
            }
            return position;
        }
        return origin;
    }

    [RelayCommand]
    public async Task DraftWithAi()
    {
        if (SelectedContextCharacter == null)
        {
            AiDraftStatus = "Select or create a context character first.";
            return;
        }

        IsDrafting = true;
        AiDraftStatus = "Drafting...";
        try
        {
            var result = await _authoringAssistant.DraftAsync(
                RpAuthoringTargetKind.ContextCharacter,
                AiIdeaPrompt,
                _simulation.AiProvider,
                _simulation.GetCurrentProviderApiKey(),
                _simulation.SelectedModel?.Id ?? _simulation.SelectedModel?.Name ?? string.Empty);

            if (!result.Success)
            {
                AiDraftStatus = result.ErrorMessage ?? "Draft failed.";
                return;
            }

            ApplyDraftFields(result.Fields);

            AiDraftStatus = result.SafetyState == RpImportSafetyState.NeedsReview
                ? "Drafted — flagged for review. Enable manually after checking the content."
                : "Drafted. Review the fields, then click Apply to commit.";

            if (result.SafetyState == RpImportSafetyState.NeedsReview)
            {
                ContextCharacterIsNamed = false;
            }
        }
        finally
        {
            IsDrafting = false;
        }
    }

    private void ApplyDraftFields(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("ContextCharacterName", out var v)) ContextCharacterName = v;
        if (fields.TryGetValue("ContextCharacterArchetype", out v)) ContextCharacterArchetype = v;
        if (fields.TryGetValue("ContextCharacterRace", out v)) ContextCharacterRace = v;
        if (fields.TryGetValue("ContextCharacterRole", out v)) ContextCharacterRole = v;
        if (fields.TryGetValue("ContextCharacterPersonality", out v)) ContextCharacterPersonality = v;
        if (fields.TryGetValue("ContextCharacterStory", out v)) ContextCharacterStory = v;
        if (fields.TryGetValue("ContextCharacterGoal", out v)) ContextCharacterGoal = v;
        if (fields.TryGetValue("ContextCharacterLifeGoal", out v)) ContextCharacterLifeGoal = v;
        if (fields.TryGetValue("ContextCharacterTags", out v)) ContextCharacterTags = v;
        if (fields.TryGetValue("ContextCharacterSpeechStyle", out v)) ContextCharacterSpeechStyle = v;
        if (fields.TryGetValue("ContextCharacterFirstEncounter", out v)) ContextCharacterFirstEncounter = v;
        if (fields.TryGetValue("ContextCharacterNegotiation", out v)) ContextCharacterNegotiation = v;
        if (fields.TryGetValue("ContextCharacterEscalation", out v)) ContextCharacterEscalation = v;
        if (fields.TryGetValue("ContextCharacterDeEscalation", out v)) ContextCharacterDeEscalation = v;
        if (fields.TryGetValue("ContextCharacterRelationshipHandling", out v)) ContextCharacterRelationshipHandling = v;
        if (fields.TryGetValue("ContextCharacterDeception", out v)) ContextCharacterDeception = v;
        if (fields.TryGetValue("ContextCharacterCombatPreferences", out v)) ContextCharacterCombatPreferences = v;
        if (fields.TryGetValue("ContextCharacterCapturePreferences", out v)) ContextCharacterCapturePreferences = v;
    }
}
