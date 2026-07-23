using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using RPSystem.Core.Models;
using RPSystem.Desktop.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Manages world simulation state: ticking, save/load, model selection.
/// </summary>
public sealed partial class WorldSimulationViewModel : ObservableObject
{
    private const string PreviousDefaultRpModelId = "google/gemma-4-31B-turbo-TEE";
    private const string DefaultRpModelId = "Nemotron-3-Nano-Omni-30B-TEE";
    private const string DefaultRpModelName = "Nemotron 3 Nano Omni 30B TEE";
    private static readonly List<AiModel> CachedRpModels = [];
    private static bool hasLoadedRpModelList;

    private readonly RpSimulationService _simulationService;
    private readonly AiModelService _modelService;
    private readonly ISettingsService _settingsService;
    private readonly RpWorldSaveService _worldSaveService;
    private readonly RpMarkdownImportService _markdownImportService;
    private readonly IFilePickerService _filePickerService;
    private CancellationTokenSource? _tickCts;

    [ObservableProperty] private World _world = RpWorldFactory.CreateStarterWorld();
    [ObservableProperty] private Character? _selectedCharacter;
    [ObservableProperty] private AiModel? _selectedModel;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _useLlm;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<AiModel> AvailableModels { get; } = [];
    public ObservableCollection<Character> Characters { get; } = [];
    public ObservableCollection<string> EventLog { get; } = [];

    public string WorldTitle => $"{World.Name} - {World.Clock.Display}";
    public string SliceModeText => SliceMode == RpSliceMode.Horizontal ? "Horizontal X/Z" : "Vertical X/Y";
    public string SliceCoordinateText => SliceMode == RpSliceMode.Horizontal ? $"Y {SliceCoordinate}" : $"Z {SliceCoordinate}";
    public string LlmStateText => UseLlm ? "LLM on" : "Local wait";

    // Slice state — owned here for map rendering, but WorldMapViewModel reads it
    [ObservableProperty] private RpSliceMode _sliceMode;
    [ObservableProperty] private int _sliceCoordinate;

    public WorldSimulationViewModel(
        RpSimulationService simulationService,
        AiModelService modelService,
        ISettingsService settingsService,
        RpWorldSaveService worldSaveService,
        RpMarkdownImportService markdownImportService,
        IFilePickerService filePickerService)
    {
        _simulationService = simulationService;
        _modelService = modelService;
        _settingsService = settingsService;
        _worldSaveService = worldSaveService;
        _markdownImportService = markdownImportService;
        _filePickerService = filePickerService;

        _useLlm = settingsService.GetBool("RpUseLlm", false);
        RpSimulationService.AppendDebugLog($"RP settings loaded | useLlm={UseLlm} | storedRpUseLlm={settingsService.GetBool("RpUseLlm", false)}");
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

    partial void OnUseLlmChanged(bool value)
    {
        _settingsService.SetBool("RpUseLlm", value);
        RpSimulationService.AppendDebugLog($"RP settings toggle changed | useLlm={value}");
        OnPropertyChanged(nameof(LlmStateText));
        StatusText = value
            ? "RP LLM turns enabled."
            : "RP LLM turns disabled. NPCs will wait locally.";
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

    [RelayCommand]
    public async Task LoadModels()
    {
        if (IsBusy) return;
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
        if (IsBusy) return;
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
            World, UseLlm, _settingsService.AiProvider,
            GetCurrentProviderApiKey(),
            SelectedModel?.Id ?? SelectedModel?.Name ?? string.Empty,
            PlayerCharacter?.Id, cancellationToken);

        foreach (var evt in events)
        {
            EventLog.Insert(0, FormatEvent(evt));
        }

        TrimEventLog();
        RaiseWorldChanged();
        return events;
    }

    public async Task AdvanceWorldAfterPlayerActionAsync(string actionStatus)
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
            if (IsBusy && i > 0) return;
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
            if (loadedWorld == null) return;
            World = loadedWorld;
            StatusText = $"World loaded: {_worldSaveService.DefaultSavePath}";
            RpSimulationService.AppendDebugLog("RP world auto-loaded on startup.");
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
            var result = await _filePickerService.PickFileAsync(
                "Import RP markdown",
                new[] { ".md", ".txt" });

            if (result == null) return;

            var content = await File.ReadAllTextAsync(result);
            var import = _markdownImportService.ImportIntoWorld(
                World,
                Path.GetFileNameWithoutExtension(result),
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
                World.WorldContexts.Add(import.ImportedContext);
            }

            StatusText = import.Message;
            RaiseWorldChanged();
        }
        catch (Exception ex)
        {
            StatusText = $"Import failed: {ex.Message}";
        }
    }

    public Character? PlayerCharacter { get; set; }

    public string AiProvider => _settingsService.AiProvider;

    public string GetCurrentProviderApiKey()
        => (AiProvider switch
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

    private void ApplyModelList(IEnumerable<AiModel> models)
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

    private static List<AiModel> EnsureDefaultModel(IEnumerable<AiModel> models)
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

    private static AiModel CreateDefaultRpModel()
        => new()
        {
            Id = DefaultRpModelId,
            Name = DefaultRpModelName,
            Provider = "Chutes"
        };

    private static bool IsDefaultRpModel(AiModel model)
        => ContainsDefaultRpModel(model.Id) || ContainsDefaultRpModel(model.Name);

    private static bool IsPreviousDefaultRpModel(AiModel model)
        => string.Equals(model.Id, PreviousDefaultRpModelId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(model.Name, PreviousDefaultRpModelId, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsDefaultRpModel(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
            value.Contains(DefaultRpModelId, StringComparison.OrdinalIgnoreCase);

    private static string FormatEvent(NarrativeEvent evt)
        => $"T{evt.Tick} {evt.ActorName}: {evt.Description}";

    private void ResetCollections()
    {
        Characters.Clear();
        EnsureWorldContexts();

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

    public void RaiseWorldChanged()
    {
        OnPropertyChanged(nameof(WorldTitle));
        OnPropertyChanged(nameof(SliceModeText));
        OnPropertyChanged(nameof(SliceCoordinateText));
        OnPropertyChanged(nameof(LlmStateText));
    }

    private void UpdateSelectedCharacterSummary()
    {
        // This will be called by PlayerControlViewModel which has the full character summary logic
        OnPropertyChanged(nameof(SelectedCharacter));
    }

    private void TrimEventLog()
    {
        while (EventLog.Count > 80)
        {
            EventLog.RemoveAt(EventLog.Count - 1);
        }
    }
}
