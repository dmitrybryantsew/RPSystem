using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single relationship rule within a context character.
/// </summary>
public sealed partial class RelationshipRuleEditorViewModel : ObservableObject
{
    private bool _isLoading;

    [ObservableProperty] private RpRelationshipRule? _selectedRelationshipRule;

    [ObservableProperty] private string _relationshipTargetNameOrTag = string.Empty;
    [ObservableProperty] private string _relationshipType = RpRelationshipType.Unknown.ToString();
    [ObservableProperty] private int _relationshipTrust;
    [ObservableProperty] private int _relationshipFear;
    [ObservableProperty] private int _relationshipDependency;
    [ObservableProperty] private int _relationshipLoyalty;
    [ObservableProperty] private int _relationshipManipulation;
    [ObservableProperty] private int _relationshipSuspicion;
    [ObservableProperty] private string _relationshipKnownSecretsText = string.Empty;
    [ObservableProperty] private string _relationshipHandlingRules = string.Empty;

    public ObservableCollection<RpRelationshipRule> RelationshipRules { get; } = [];

    public IReadOnlyList<string> RelationshipTypeOptions { get; } = Enum.GetNames<RpRelationshipType>();

    public string ActiveRelationshipRuleSummary => SelectedRelationshipRule == null
        ? "No relationship rule selected"
        : $"{SelectedRelationshipRule.TargetNameOrTag} ({SelectedRelationshipRule.Type})";

    public void RefreshRelationshipRules(List<RpRelationshipRule>? rules)
    {
        RelationshipRules.Clear();
        if (rules != null)
        {
            foreach (var rule in rules)
            {
                RelationshipRules.Add(rule);
            }
        }
        SelectedRelationshipRule = RelationshipRules.FirstOrDefault();
    }

    public void LoadRelationshipRuleEditor(RpRelationshipRule? rule)
    {
        _isLoading = true;
        try
        {
            SelectedRelationshipRule = rule;
            RelationshipTargetNameOrTag = rule?.TargetNameOrTag ?? string.Empty;
            RelationshipType = rule?.Type.ToString() ?? RpRelationshipType.Unknown.ToString();
            RelationshipTrust = rule?.Trust ?? 0;
            RelationshipFear = rule?.Fear ?? 0;
            RelationshipDependency = rule?.Dependency ?? 0;
            RelationshipLoyalty = rule?.Loyalty ?? 0;
            RelationshipManipulation = rule?.Manipulation ?? 0;
            RelationshipSuspicion = rule?.Suspicion ?? 0;
            RelationshipKnownSecretsText = EditorTextFormat.FormatLines(rule?.KnownSecrets);
            RelationshipHandlingRules = rule?.HandlingRules ?? string.Empty;
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ApplyRelationshipRuleEditor()
    {
        if (_isLoading || SelectedRelationshipRule == null) return;

        SelectedRelationshipRule.TargetNameOrTag = string.IsNullOrWhiteSpace(RelationshipTargetNameOrTag) ? "target" : RelationshipTargetNameOrTag.Trim();
        SelectedRelationshipRule.Type = Enum.TryParse<RpRelationshipType>(RelationshipType, true, out var type) ? type : RpRelationshipType.Unknown;
        SelectedRelationshipRule.Trust = RelationshipTrust;
        SelectedRelationshipRule.Fear = RelationshipFear;
        SelectedRelationshipRule.Dependency = RelationshipDependency;
        SelectedRelationshipRule.Loyalty = RelationshipLoyalty;
        SelectedRelationshipRule.Manipulation = RelationshipManipulation;
        SelectedRelationshipRule.Suspicion = RelationshipSuspicion;
        SelectedRelationshipRule.KnownSecrets = EditorTextFormat.ParseLines(RelationshipKnownSecretsText);
        SelectedRelationshipRule.HandlingRules = RelationshipHandlingRules;
    }
}
