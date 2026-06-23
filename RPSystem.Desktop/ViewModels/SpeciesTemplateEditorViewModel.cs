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
    private bool _isLoading;

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
}
