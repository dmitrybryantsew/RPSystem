using CommunityToolkit.Mvvm.ComponentModel;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for scene, environment, and continuity state within a world context.
/// </summary>
public sealed partial class SceneEnvironmentContinuityEditorViewModel : ObservableObject
{
    private bool _isLoading;

    [ObservableProperty] private string _scenePhase = RpScenePhase.Setup.ToString();
    [ObservableProperty] private float _sceneEscalationBudget = 1;
    [ObservableProperty] private float _sceneEscalationRate = 0.1f;
    [ObservableProperty] private string _sceneActiveThreadsText = string.Empty;
    [ObservableProperty] private string _sceneForeshadowedText = string.Empty;
    [ObservableProperty] private string _sceneUnresolvedPromisesText = string.Empty;
    [ObservableProperty] private string _sceneMajorPrerequisitesText = string.Empty;
    [ObservableProperty] private string _environmentInteractiveObjectsText = string.Empty;
    [ObservableProperty] private string _environmentTellsText = string.Empty;
    [ObservableProperty] private string _environmentHazardsText = string.Empty;
    [ObservableProperty] private string _environmentTerrainAffordancesText = string.Empty;
    [ObservableProperty] private string _environmentCluesText = string.Empty;
    [ObservableProperty] private string _environmentDomainOwnerRulesText = string.Empty;
    [ObservableProperty] private string _continuityPhysicalChangesText = string.Empty;
    [ObservableProperty] private string _continuityEmotionalChangesText = string.Empty;
    [ObservableProperty] private string _continuityRelationshipChangesText = string.Empty;
    [ObservableProperty] private string _continuityFlagsText = string.Empty;
    [ObservableProperty] private string _continuityTriggersText = string.Empty;
    [ObservableProperty] private string _continuityIrreversibleEventsText = string.Empty;
    [ObservableProperty] private string _continuityPendingConsequencesText = string.Empty;

    public IReadOnlyList<string> ScenePhaseOptions { get; } = Enum.GetNames<RpScenePhase>();

    public void LoadSceneEnvironmentContinuityEditor(RpWorldContextEntry? context)
    {
        _isLoading = true;
        try
        {
            var scene = context?.SceneState ?? new RpSceneState();
            ScenePhase = scene.Phase.ToString();
            SceneEscalationBudget = scene.EscalationBudget;
            SceneEscalationRate = scene.EscalationRatePerTick;
            SceneActiveThreadsText = EditorTextFormat.FormatLines(scene.ActiveThreads);
            SceneForeshadowedText = EditorTextFormat.FormatLines(scene.ForeshadowedElements);
            SceneUnresolvedPromisesText = EditorTextFormat.FormatLines(scene.UnresolvedPromises);
            SceneMajorPrerequisitesText = EditorTextFormat.FormatLines(scene.MajorActionPrerequisites);

            var environment = context?.EnvironmentRules ?? new RpEnvironmentRuleSet();
            EnvironmentInteractiveObjectsText = FormatInteractiveObjects(environment.InteractiveObjects);
            EnvironmentTellsText = EditorTextFormat.FormatLines(environment.EnvironmentalTells);
            EnvironmentHazardsText = EditorTextFormat.FormatLines(environment.Hazards);
            EnvironmentTerrainAffordancesText = EditorTextFormat.FormatLines(environment.TerrainAffordances);
            EnvironmentCluesText = EditorTextFormat.FormatLines(environment.Clues);
            EnvironmentDomainOwnerRulesText = EditorTextFormat.FormatLines(environment.DomainOwnerAwarenessRules);

            var continuity = context?.Continuity ?? new RpContinuityState();
            ContinuityPhysicalChangesText = EditorTextFormat.FormatLines(continuity.PersistentPhysicalChanges);
            ContinuityEmotionalChangesText = EditorTextFormat.FormatLines(continuity.EmotionalStateChanges);
            ContinuityRelationshipChangesText = EditorTextFormat.FormatLines(continuity.RelationshipChanges);
            ContinuityFlagsText = EditorTextFormat.FormatLines(continuity.Flags);
            ContinuityTriggersText = EditorTextFormat.FormatLines(continuity.Triggers);
            ContinuityIrreversibleEventsText = EditorTextFormat.FormatLines(continuity.IrreversibleEvents);
            ContinuityPendingConsequencesText = EditorTextFormat.FormatLines(continuity.PendingConsequences);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ApplySceneEnvironmentContinuityEditor(RpWorldContextEntry? context)
    {
        if (_isLoading || context == null) return;

        context.SceneState ??= new RpSceneState();
        context.SceneState.Phase = Enum.TryParse<RpScenePhase>(ScenePhase, true, out var phase) ? phase : RpScenePhase.Setup;
        context.SceneState.EscalationBudget = Math.Max(0, SceneEscalationBudget);
        context.SceneState.EscalationRatePerTick = Math.Max(0, SceneEscalationRate);
        context.SceneState.ActiveThreads = EditorTextFormat.ParseLines(SceneActiveThreadsText);
        context.SceneState.ForeshadowedElements = EditorTextFormat.ParseLines(SceneForeshadowedText);
        context.SceneState.UnresolvedPromises = EditorTextFormat.ParseLines(SceneUnresolvedPromisesText);
        context.SceneState.MajorActionPrerequisites = EditorTextFormat.ParseLines(SceneMajorPrerequisitesText);

        context.EnvironmentRules ??= new RpEnvironmentRuleSet();
        context.EnvironmentRules.InteractiveObjects = ParseInteractiveObjects(EnvironmentInteractiveObjectsText);
        context.EnvironmentRules.EnvironmentalTells = EditorTextFormat.ParseLines(EnvironmentTellsText);
        context.EnvironmentRules.Hazards = EditorTextFormat.ParseLines(EnvironmentHazardsText);
        context.EnvironmentRules.TerrainAffordances = EditorTextFormat.ParseLines(EnvironmentTerrainAffordancesText);
        context.EnvironmentRules.Clues = EditorTextFormat.ParseLines(EnvironmentCluesText);
        context.EnvironmentRules.DomainOwnerAwarenessRules = EditorTextFormat.ParseLines(EnvironmentDomainOwnerRulesText);

        context.Continuity ??= new RpContinuityState();
        context.Continuity.PersistentPhysicalChanges = EditorTextFormat.ParseLines(ContinuityPhysicalChangesText);
        context.Continuity.EmotionalStateChanges = EditorTextFormat.ParseLines(ContinuityEmotionalChangesText);
        context.Continuity.RelationshipChanges = EditorTextFormat.ParseLines(ContinuityRelationshipChangesText);
        context.Continuity.Flags = EditorTextFormat.ParseLines(ContinuityFlagsText);
        context.Continuity.Triggers = EditorTextFormat.ParseLines(ContinuityTriggersText);
        context.Continuity.IrreversibleEvents = EditorTextFormat.ParseLines(ContinuityIrreversibleEventsText);
        context.Continuity.PendingConsequences = EditorTextFormat.ParseLines(ContinuityPendingConsequencesText);
    }

    private static string FormatInteractiveObjects(IEnumerable<RpInteractiveObjectRule>? rules)
        => rules == null
            ? string.Empty
            : string.Join(Environment.NewLine, rules.Select(rule =>
                $"{rule.Name} | {rule.AppliesToTileOrTag} | {rule.Interaction} | {rule.WorldEffect} | {rule.NarrativeEffect} | {rule.Constraints}"));

    private static List<RpInteractiveObjectRule> ParseInteractiveObjects(string? value)
    {
        var result = new List<RpInteractiveObjectRule>();
        foreach (var line in EditorTextFormat.ParseLines(value))
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
}
