using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Editor for a single ability within a context character.
/// </summary>
public sealed partial class ContextAbilityEditorViewModel : ObservableObject
{
    private bool _isLoading;

    [ObservableProperty] private RpAbility? _selectedContextAbility;

    [ObservableProperty] private string _contextAbilityId = string.Empty;
    [ObservableProperty] private string _contextAbilityName = string.Empty;
    [ObservableProperty] private string _contextAbilityDescription = string.Empty;
    [ObservableProperty] private string _contextAbilityTargetKind = RpAbilityTargetKind.Tile.ToString();
    [ObservableProperty] private string _contextAbilityDamageType = RpDamageType.Physical.ToString();
    [ObservableProperty] private string _contextAbilityResource = RpAbilityResource.None.ToString();
    [ObservableProperty] private float _contextAbilityManaCost;
    [ObservableProperty] private float _contextAbilityFocusCost;
    [ObservableProperty] private float _contextAbilityStaminaCost;
    [ObservableProperty] private float _contextAbilityDamage;
    [ObservableProperty] private int _contextAbilityRange = 1;
    [ObservableProperty] private int _contextAbilityTickCost = 1;
    [ObservableProperty] private int _contextAbilityCooldownTicks;
    [ObservableProperty] private string _contextAbilityRangeText = string.Empty;
    [ObservableProperty] private string _contextAbilityTargetRules = string.Empty;
    [ObservableProperty] private string _contextAbilityConstraints = string.Empty;
    [ObservableProperty] private string _contextAbilityWorldEffect = string.Empty;
    [ObservableProperty] private string _contextAbilityNarrativeEffect = string.Empty;
    [ObservableProperty] private string _contextAbilityAllowedUsage = string.Empty;
    [ObservableProperty] private string _contextAbilityForbiddenUsage = string.Empty;
    [ObservableProperty] private string _contextAbilityTagsText = string.Empty;

    public ObservableCollection<RpAbility> ContextAbilities { get; } = [];

    public IReadOnlyList<string> AbilityTargetKindOptions { get; } = Enum.GetNames<RpAbilityTargetKind>();
    public IReadOnlyList<string> AbilityDamageTypeOptions { get; } = Enum.GetNames<RpDamageType>();
    public IReadOnlyList<string> AbilityResourceOptions { get; } = Enum.GetNames<RpAbilityResource>();

    public string ActiveAbilitySummary => SelectedContextAbility == null
        ? "No ability selected"
        : $"{SelectedContextAbility.Name} ({SelectedContextAbility.TargetKind})";

    public void RefreshContextAbilities(List<RpAbility>? abilities)
    {
        ContextAbilities.Clear();
        if (abilities != null)
        {
            foreach (var ability in abilities)
            {
                ContextAbilities.Add(ability);
            }
        }
        SelectedContextAbility = ContextAbilities.FirstOrDefault();
    }

    public void LoadContextAbilityEditor(RpAbility? ability)
    {
        _isLoading = true;
        try
        {
            SelectedContextAbility = ability;
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
            ContextAbilityTagsText = EditorTextFormat.FormatLines(ability?.Tags);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void ApplyContextAbilityEditor()
    {
        if (_isLoading || SelectedContextAbility == null) return;

        SelectedContextAbility.Id = string.IsNullOrWhiteSpace(ContextAbilityId) ? EditorTextFormat.Slugify(ContextAbilityName, "ability") : ContextAbilityId.Trim();
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
        SelectedContextAbility.Tags = EditorTextFormat.ParseLines(ContextAbilityTagsText);
    }
}
