using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single context module within a world context.
/// </summary>
public sealed partial class ContextModuleEditorViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpAuthoringAssistantService _authoringAssistant;
    private bool _isLoading;

    [ObservableProperty] private string _aiIdeaPrompt = string.Empty;
    [ObservableProperty] private bool _isDrafting;
    [ObservableProperty] private string _aiDraftStatus = string.Empty;

    [ObservableProperty] private RpContextModule? _selectedContextModule;

    [ObservableProperty] private string _contextModuleName = string.Empty;
    [ObservableProperty] private bool _contextModuleIsEnabled;
    [ObservableProperty] private string _contextModuleType = RpContextModuleType.GeneralRules.ToString();
    [ObservableProperty] private string _contextModuleVisibility = RpContextVisibility.WorldOnly.ToString();
    [ObservableProperty] private int _contextModulePriority = 100;
    [ObservableProperty] private string _contextModuleSourceLabel = string.Empty;
    [ObservableProperty] private string _contextModuleAppliesTo = string.Empty;
    [ObservableProperty] private string _contextModuleText = string.Empty;

    public ObservableCollection<RpContextModule> ContextModules { get; } = [];

    public IReadOnlyList<string> ContextModuleTypeOptions { get; } = Enum.GetNames<RpContextModuleType>();
    public IReadOnlyList<string> ContextModuleVisibilityOptions { get; } = Enum.GetNames<RpContextVisibility>();

    public string ActiveContextModuleSummary => SelectedContextModule == null
        ? "No module selected"
        : $"{SelectedContextModule.Name} ({SelectedContextModule.Type})";

    public ContextModuleEditorViewModel(WorldSimulationViewModel simulation, RpAuthoringAssistantService authoringAssistant)
    {
        _simulation = simulation;
        _authoringAssistant = authoringAssistant;
    }

    public void RefreshContextModules(List<RpContextModule>? modules)
    {
        ContextModules.Clear();
        if (modules != null)
        {
            foreach (var module in modules.OrderBy(m => m.Priority).ThenBy(m => m.Name))
            {
                ContextModules.Add(module);
            }
        }
        SelectedContextModule = ContextModules.FirstOrDefault();
    }

    public void LoadContextModuleEditor(RpContextModule? module)
    {
        _isLoading = true;
        try
        {
            SelectedContextModule = module;
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
            _isLoading = false;
        }
    }

    public void ApplyContextModuleEditor()
    {
        if (_isLoading || SelectedContextModule == null) return;

        SelectedContextModule.Name = string.IsNullOrWhiteSpace(ContextModuleName) ? "Context Module" : ContextModuleName.Trim();
        SelectedContextModule.IsEnabled = ContextModuleIsEnabled;
        SelectedContextModule.Type = Enum.TryParse<RpContextModuleType>(ContextModuleType, true, out var type)
            ? type : RpContextModuleType.GeneralRules;
        SelectedContextModule.Visibility = Enum.TryParse<RpContextVisibility>(ContextModuleVisibility, true, out var visibility)
            ? visibility : RpContextVisibility.WorldOnly;
        SelectedContextModule.Priority = ContextModulePriority;
        SelectedContextModule.SourceLabel = ContextModuleSourceLabel.Trim();
        SelectedContextModule.AppliesTo = ContextModuleAppliesTo.Trim();
        SelectedContextModule.Text = ContextModuleText;
    }

    [RelayCommand]
    public async Task DraftWithAi()
    {
        if (SelectedContextModule == null)
        {
            AiDraftStatus = "Select or create a context module first.";
            return;
        }

        IsDrafting = true;
        AiDraftStatus = "Drafting...";
        try
        {
            var result = await _authoringAssistant.DraftAsync(
                RpAuthoringTargetKind.ContextModule,
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
                ContextModuleIsEnabled = false;
            }
        }
        finally
        {
            IsDrafting = false;
        }
    }

    private void ApplyDraftFields(Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("ContextModuleName", out var v)) ContextModuleName = v;
        if (fields.TryGetValue("ContextModuleType", out v) && ContextModuleTypeOptions.Contains(v, StringComparer.OrdinalIgnoreCase))
            ContextModuleType = v;
        if (fields.TryGetValue("ContextModuleVisibility", out v) && ContextModuleVisibilityOptions.Contains(v, StringComparer.OrdinalIgnoreCase))
            ContextModuleVisibility = v;
        if (fields.TryGetValue("ContextModulePriorityText", out v) && int.TryParse(v, out var pri)) ContextModulePriority = pri;
        if (fields.TryGetValue("ContextModuleAppliesTo", out v)) ContextModuleAppliesTo = v;
        if (fields.TryGetValue("ContextModuleText", out v)) ContextModuleText = v;
    }
}
