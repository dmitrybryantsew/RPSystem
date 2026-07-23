using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single faction profile within a world context.
/// </summary>
public sealed partial class FactionProfileEditorViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpAuthoringAssistantService _authoringAssistant;
    private bool _isLoading;

    [ObservableProperty] private string _aiIdeaPrompt = string.Empty;
    [ObservableProperty] private bool _isDrafting;
    [ObservableProperty] private string _aiDraftStatus = string.Empty;

    [ObservableProperty] private RpFactionProfile? _selectedFactionProfile;

    [ObservableProperty] private string _factionId = string.Empty;
    [ObservableProperty] private string _factionName = string.Empty;
    [ObservableProperty] private bool _factionIsEnabled;
    [ObservableProperty] private string _factionVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private string _factionAppliesTo = string.Empty;
    [ObservableProperty] private string _factionParentSpecies = string.Empty;
    [ObservableProperty] private string _factionPublicDescription = string.Empty;
    [ObservableProperty] private string _factionHiddenDoctrine = string.Empty;
    [ObservableProperty] private string _factionCulture = string.Empty;
    [ObservableProperty] private string _factionHierarchy = string.Empty;
    [ObservableProperty] private string _factionGoals = string.Empty;
    [ObservableProperty] private string _factionTaboos = string.Empty;
    [ObservableProperty] private string _factionOutsiderBehavior = string.Empty;
    [ObservableProperty] private string _factionMemberBehavior = string.Empty;
    [ObservableProperty] private string _factionAppearance = string.Empty;
    [ObservableProperty] private string _factionAnatomyOverrides = string.Empty;
    [ObservableProperty] private string _factionMagicRules = string.Empty;
    [ObservableProperty] private string _factionResourceRules = string.Empty;
    [ObservableProperty] private string _factionTags = string.Empty;
    [ObservableProperty] private string _factionRolesText = string.Empty;
    [ObservableProperty] private string _factionAbilitiesText = string.Empty;
    [ObservableProperty] private string _factionRelationshipRulesText = string.Empty;

    public ObservableCollection<RpFactionProfile> FactionProfiles { get; } = [];

    public string ActiveFactionProfileSummary
    {
        get
        {
            if (FactionProfiles.Count == 0) return "No faction profiles";
            return $"{FactionProfiles.Count(f => f.IsEnabled)} active / {FactionProfiles.Count} total";
        }
    }

    public FactionProfileEditorViewModel(WorldSimulationViewModel simulation, RpAuthoringAssistantService authoringAssistant)
    {
        _simulation = simulation;
        _authoringAssistant = authoringAssistant;
    }

    public void RefreshFactionProfiles(List<RpFactionProfile>? factions)
    {
        FactionProfiles.Clear();
        if (factions != null)
        {
            foreach (var faction in factions)
            {
                FactionProfiles.Add(faction);
            }
        }
        SelectedFactionProfile = FactionProfiles.FirstOrDefault(f => f.IsEnabled) ?? FactionProfiles.FirstOrDefault();
    }

    public void LoadFactionProfileEditor(RpFactionProfile? faction)
    {
        _isLoading = true;
        try
        {
            SelectedFactionProfile = faction;
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
            _isLoading = false;
        }
    }

    public void ApplyFactionProfileEditor()
    {
        if (_isLoading || SelectedFactionProfile == null) return;

        SelectedFactionProfile.FactionId = string.IsNullOrWhiteSpace(FactionId) ? EditorTextFormat.Slugify(FactionName, "faction") : FactionId.Trim();
        SelectedFactionProfile.Name = string.IsNullOrWhiteSpace(FactionName) ? "Faction" : FactionName.Trim();
        SelectedFactionProfile.IsEnabled = FactionIsEnabled;
        SelectedFactionProfile.Visibility = Enum.TryParse<RpContextVisibility>(FactionVisibility, true, out var visibility)
            ? visibility : RpContextVisibility.WorldOnly;
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
    }

    public void SyncRuntimeFaction(RpFactionProfile profile, World world)
    {
        if (string.IsNullOrWhiteSpace(profile.FactionId)) return;
        world.Factions ??= [];
        world.Factions[profile.FactionId] = new Faction
        {
            Id = profile.FactionId,
            Name = profile.Name,
            Description = profile.PublicDescription
        };
    }

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

    private static List<RpFactionRole> ParseFactionRoles(string? value)
        => new RpWorldContextEditorService().ParseFactionRoles(value);

    private static List<RpAbility> ParseAbilities(string? value)
    {
        var result = new List<RpAbility>();
        foreach (var line in EditorTextFormat.ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var name = parts.ElementAtOrDefault(1);
            result.Add(new RpAbility
            {
                Id = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? EditorTextFormat.Slugify(name, "ability") : parts.ElementAtOrDefault(0) ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(name) ? "Ability" : name,
                TargetKind = Enum.TryParse<RpAbilityTargetKind>(parts.ElementAtOrDefault(2), true, out var targetKind) ? targetKind : RpAbilityTargetKind.Character,
                PrimaryResource = Enum.TryParse<RpAbilityResource>(parts.ElementAtOrDefault(3), true, out var resource) ? resource : RpAbilityResource.None,
                ManaCost = EditorTextFormat.ParseFloat(parts.ElementAtOrDefault(4)),
                FocusCost = EditorTextFormat.ParseFloat(parts.ElementAtOrDefault(5)),
                StaminaCost = EditorTextFormat.ParseFloat(parts.ElementAtOrDefault(6)),
                Damage = EditorTextFormat.ParseFloat(parts.ElementAtOrDefault(7)),
                Range = Math.Max(0, EditorTextFormat.ParseInt(parts.ElementAtOrDefault(8), 1)),
                TickCost = Math.Max(1, EditorTextFormat.ParseInt(parts.ElementAtOrDefault(9), 1)),
                CooldownTicks = Math.Max(0, EditorTextFormat.ParseInt(parts.ElementAtOrDefault(10), 0)),
                Description = parts.ElementAtOrDefault(11) ?? string.Empty,
                Tags = EditorTextFormat.SplitList(parts.ElementAtOrDefault(12))
            });
        }
        return result;
    }

    private static List<RpRelationshipRule> ParseRelationshipRules(string? value)
    {
        var result = new List<RpRelationshipRule>();
        foreach (var line in EditorTextFormat.ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            result.Add(new RpRelationshipRule
            {
                TargetNameOrTag = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? "target" : parts.ElementAtOrDefault(0) ?? "target",
                Type = Enum.TryParse<RpRelationshipType>(parts.ElementAtOrDefault(1), true, out var type) ? type : RpRelationshipType.Unknown,
                Trust = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(2), 0),
                Fear = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(3), 0),
                Dependency = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(4), 0),
                Loyalty = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(5), 0),
                Manipulation = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(6), 0),
                Suspicion = EditorTextFormat.ParseInt(parts.ElementAtOrDefault(7), 0),
                KnownSecrets = EditorTextFormat.SplitList(parts.ElementAtOrDefault(8)),
                HandlingRules = parts.ElementAtOrDefault(9) ?? string.Empty
            });
        }
        return result;
    }

    [RelayCommand]
    public async Task DraftWithAi()
    {
        if (SelectedFactionProfile == null)
        {
            AiDraftStatus = "Select or create a faction profile first.";
            return;
        }

        IsDrafting = true;
        AiDraftStatus = "Drafting...";
        try
        {
            var result = await _authoringAssistant.DraftAsync(
                RpAuthoringTargetKind.FactionProfile,
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
                FactionIsEnabled = false;
            }
        }
        finally
        {
            IsDrafting = false;
        }
    }

    private void ApplyDraftFields(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("FactionName", out var v)) FactionName = v;
        if (fields.TryGetValue("FactionId", out v)) FactionId = v;
        if (fields.TryGetValue("FactionPublicDescription", out v)) FactionPublicDescription = v;
        if (fields.TryGetValue("FactionHiddenDoctrine", out v)) FactionHiddenDoctrine = v;
        if (fields.TryGetValue("FactionCulture", out v)) FactionCulture = v;
        if (fields.TryGetValue("FactionHierarchy", out v)) FactionHierarchy = v;
        if (fields.TryGetValue("FactionGoals", out v)) FactionGoals = v;
        if (fields.TryGetValue("FactionTaboos", out v)) FactionTaboos = v;
        if (fields.TryGetValue("FactionOutsiderBehavior", out v)) FactionOutsiderBehavior = v;
        if (fields.TryGetValue("FactionMemberBehavior", out v)) FactionMemberBehavior = v;
        if (fields.TryGetValue("FactionAppearance", out v)) FactionAppearance = v;
        if (fields.TryGetValue("FactionTags", out v)) FactionTags = v;
        if (fields.TryGetValue("FactionRelationshipRulesText", out v)) FactionRelationshipRulesText = v;
    }
}
