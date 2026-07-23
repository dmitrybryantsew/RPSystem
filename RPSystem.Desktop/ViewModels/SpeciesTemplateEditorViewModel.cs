using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single species template within a world context.
/// </summary>
public sealed partial class SpeciesTemplateEditorViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpAuthoringAssistantService _authoringAssistant;
    private bool _isLoading;

    [ObservableProperty] private string _aiIdeaPrompt = string.Empty;
    [ObservableProperty] private bool _isDrafting;
    [ObservableProperty] private string _aiDraftStatus = string.Empty;

    [ObservableProperty] private RpSpeciesTemplate? _selectedSpeciesTemplate;

    [ObservableProperty] private string _speciesTemplateName = string.Empty;
    [ObservableProperty] private string _speciesTemplateRace = string.Empty;
    [ObservableProperty] private string _speciesTemplateBodyType = BodyTypeKind.Human.ToString();
    [ObservableProperty] private string _speciesBodyLanguageText = string.Empty;
    [ObservableProperty] private string _speciesVocalizationsText = string.Empty;
    [ObservableProperty] private string _speciesDietRules = string.Empty;
    [ObservableProperty] private string _speciesEnergyRules = string.Empty;
    [ObservableProperty] private string _speciesMagicRules = string.Empty;
    [ObservableProperty] private string _speciesAnatomyModifiersText = string.Empty;
    [ObservableProperty] private string _speciesTagsText = string.Empty;

    public ObservableCollection<RpSpeciesTemplate> SpeciesTemplates { get; } = [];

    public IReadOnlyList<string> BodyTypeOptions { get; } = Enum.GetNames<BodyTypeKind>();

    public string ActiveSpeciesTemplateSummary => SelectedSpeciesTemplate == null
        ? "No species template selected"
        : $"{SelectedSpeciesTemplate.Name} ({SelectedSpeciesTemplate.BodyType})";

    public SpeciesTemplateEditorViewModel(WorldSimulationViewModel simulation, RpAuthoringAssistantService authoringAssistant)
    {
        _simulation = simulation;
        _authoringAssistant = authoringAssistant;
    }

    public void RefreshSpeciesTemplates(List<RpSpeciesTemplate>? templates)
    {
        SpeciesTemplates.Clear();
        if (templates != null)
        {
            foreach (var template in templates)
            {
                SpeciesTemplates.Add(template);
            }
        }
        SelectedSpeciesTemplate = SpeciesTemplates.FirstOrDefault();
    }

    public void LoadSpeciesTemplateEditor(RpSpeciesTemplate? template)
    {
        _isLoading = true;
        try
        {
            SelectedSpeciesTemplate = template;
            SpeciesTemplateName = template?.Name ?? string.Empty;
            SpeciesTemplateRace = template?.AppliesToRace ?? string.Empty;
            SpeciesTemplateBodyType = template?.BodyType.ToString() ?? BodyTypeKind.Human.ToString();
            SpeciesBodyLanguageText = EditorTextFormat.FormatDictionary(template?.BodyLanguage);
            SpeciesVocalizationsText = EditorTextFormat.FormatDictionary(template?.Vocalizations);
            SpeciesDietRules = template?.DietRules ?? string.Empty;
            SpeciesEnergyRules = template?.EnergyRules ?? string.Empty;
            SpeciesMagicRules = template?.MagicRules ?? string.Empty;
            SpeciesAnatomyModifiersText = EditorTextFormat.FormatDictionary(template?.AnatomyModifiers);
            SpeciesTagsText = EditorTextFormat.FormatLines(template?.Tags);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ApplySpeciesTemplateEditor()
    {
        if (_isLoading || SelectedSpeciesTemplate == null) return;

        SelectedSpeciesTemplate.Name = string.IsNullOrWhiteSpace(SpeciesTemplateName) ? "Species" : SpeciesTemplateName.Trim();
        SelectedSpeciesTemplate.AppliesToRace = SpeciesTemplateRace.Trim();
        SelectedSpeciesTemplate.BodyType = Enum.TryParse<BodyTypeKind>(SpeciesTemplateBodyType, true, out var bodyType)
            ? bodyType : BodyTypeKind.Human;
        SelectedSpeciesTemplate.BodyLanguage = EditorTextFormat.ParseDictionary(SpeciesBodyLanguageText);
        SelectedSpeciesTemplate.Vocalizations = EditorTextFormat.ParseDictionary(SpeciesVocalizationsText);
        SelectedSpeciesTemplate.DietRules = SpeciesDietRules;
        SelectedSpeciesTemplate.EnergyRules = SpeciesEnergyRules;
        SelectedSpeciesTemplate.MagicRules = SpeciesMagicRules;
        SelectedSpeciesTemplate.AnatomyModifiers = EditorTextFormat.ParseDictionary(SpeciesAnatomyModifiersText);
        SelectedSpeciesTemplate.Tags = EditorTextFormat.ParseLines(SpeciesTagsText);
    }

    [RelayCommand]
    public async Task DraftWithAi()
    {
        if (SelectedSpeciesTemplate == null)
        {
            AiDraftStatus = "Select or create a species template first.";
            return;
        }

        IsDrafting = true;
        AiDraftStatus = "Drafting...";
        try
        {
            var result = await _authoringAssistant.DraftAsync(
                RpAuthoringTargetKind.SpeciesTemplate,
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
        }
        finally
        {
            IsDrafting = false;
        }
    }

    private void ApplyDraftFields(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("SpeciesTemplateName", out var v)) SpeciesTemplateName = v;
        if (fields.TryGetValue("SpeciesTemplateRace", out v)) SpeciesTemplateRace = v;
        if (fields.TryGetValue("SpeciesTemplateBodyType", out v) && BodyTypeOptions.Contains(v, StringComparer.OrdinalIgnoreCase))
            SpeciesTemplateBodyType = v;
        if (fields.TryGetValue("SpeciesBodyLanguageText", out v)) SpeciesBodyLanguageText = v;
        if (fields.TryGetValue("SpeciesVocalizationsText", out v)) SpeciesVocalizationsText = v;
        if (fields.TryGetValue("SpeciesDietRules", out v)) SpeciesDietRules = v;
        if (fields.TryGetValue("SpeciesEnergyRules", out v)) SpeciesEnergyRules = v;
        if (fields.TryGetValue("SpeciesMagicRules", out v)) SpeciesMagicRules = v;
        if (fields.TryGetValue("SpeciesAnatomyModifiersText", out v)) SpeciesAnatomyModifiersText = v;
        if (fields.TryGetValue("SpeciesTagsText", out v)) SpeciesTagsText = v;
    }
}
