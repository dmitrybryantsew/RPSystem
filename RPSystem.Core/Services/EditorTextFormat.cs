using System.Globalization;
using System.Text;
using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

/// <summary>
/// Shared text-formatting and parsing helpers for editor viewmodels.
/// Extracted from the monolithic RpSystemViewModel to avoid duplication.
/// Pure string parsing — no UI types — belongs in Core.
/// </summary>
public static class EditorTextFormat
{
    public static List<string> SplitList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public static int ParseInt(string? value, int fallback)
        => int.TryParse(value, out var result) ? result : fallback;

    public static float ParseFloat(string? value)
        => float.TryParse(value, out var result) ? result : 0;

    public static string Slugify(string? value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var chars = source
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        while (slug.Contains("__", StringComparison.Ordinal))
            slug = slug.Replace("__", "_", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
    }

    public static List<string> ParseLines(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

    public static Dictionary<string, string> ParseDictionary(string? value)
    {
        var result = new Dictionary<string, string>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
                result[parts[0]] = parts[1];
        }
        return result;
    }

    public static string FormatDictionary(IReadOnlyDictionary<string, string>? dict)
        => dict == null ? string.Empty : string.Join(Environment.NewLine, dict.Select(kv => $"{kv.Key} = {kv.Value}"));

    public static string FormatLines(IEnumerable<string>? lines)
        => lines == null ? string.Empty : string.Join(Environment.NewLine, lines);

    public static string FormatAbilities(IEnumerable<RpAbility>? abilities)
        => abilities == null
            ? string.Empty
            : string.Join(Environment.NewLine, abilities.Select(ability =>
                $"{ability.Id} | {ability.Name} | {ability.TargetKind} | {ability.PrimaryResource} | {ability.ManaCost:0.###} | {ability.FocusCost:0.###} | {ability.StaminaCost:0.###} | {ability.Damage:0.###} | {ability.Range} | {ability.TickCost} | {ability.CooldownTicks} | {ability.Description} | {string.Join(',', ability.Tags ?? [])}"));

    public static List<RpAbility> ParseAbilities(string? value)
    {
        var result = new List<RpAbility>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var name = parts.ElementAtOrDefault(1);
            result.Add(new RpAbility
            {
                Id = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? Slugify(name, "ability") : parts.ElementAtOrDefault(0) ?? string.Empty,
                Name = string.IsNullOrWhiteSpace(name) ? "Ability" : name,
                TargetKind = Enum.TryParse<RpAbilityTargetKind>(parts.ElementAtOrDefault(2), true, out var targetKind) ? targetKind : RpAbilityTargetKind.Character,
                PrimaryResource = Enum.TryParse<RpAbilityResource>(parts.ElementAtOrDefault(3), true, out var resource) ? resource : RpAbilityResource.None,
                ManaCost = ParseFloat(parts.ElementAtOrDefault(4)),
                FocusCost = ParseFloat(parts.ElementAtOrDefault(5)),
                StaminaCost = ParseFloat(parts.ElementAtOrDefault(6)),
                Damage = ParseFloat(parts.ElementAtOrDefault(7)),
                Range = Math.Max(0, ParseInt(parts.ElementAtOrDefault(8), 1)),
                TickCost = Math.Max(1, ParseInt(parts.ElementAtOrDefault(9), 1)),
                CooldownTicks = Math.Max(0, ParseInt(parts.ElementAtOrDefault(10), 0)),
                Description = parts.ElementAtOrDefault(11) ?? string.Empty,
                Tags = SplitList(parts.ElementAtOrDefault(12))
            });
        }
        return result;
    }

    public static string FormatRelationshipRules(IEnumerable<RpRelationshipRule>? rules)
        => rules == null
            ? string.Empty
            : string.Join(Environment.NewLine, rules.Select(rule =>
                $"{rule.TargetNameOrTag} | {rule.Type} | {rule.Trust} | {rule.Fear} | {rule.Dependency} | {rule.Loyalty} | {rule.Manipulation} | {rule.Suspicion} | {string.Join(';', rule.KnownSecrets ?? [])} | {rule.HandlingRules}"));

    public static List<RpRelationshipRule> ParseRelationshipRules(string? value)
    {
        var result = new List<RpRelationshipRule>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            result.Add(new RpRelationshipRule
            {
                TargetNameOrTag = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? "target" : parts.ElementAtOrDefault(0) ?? "target",
                Type = Enum.TryParse<RpRelationshipType>(parts.ElementAtOrDefault(1), true, out var type) ? type : RpRelationshipType.Unknown,
                Trust = ParseInt(parts.ElementAtOrDefault(2), 0),
                Fear = ParseInt(parts.ElementAtOrDefault(3), 0),
                Dependency = ParseInt(parts.ElementAtOrDefault(4), 0),
                Loyalty = ParseInt(parts.ElementAtOrDefault(5), 0),
                Manipulation = ParseInt(parts.ElementAtOrDefault(6), 0),
                Suspicion = ParseInt(parts.ElementAtOrDefault(7), 0),
                KnownSecrets = SplitList(parts.ElementAtOrDefault(8)),
                HandlingRules = parts.ElementAtOrDefault(9) ?? string.Empty
            });
        }
        return result;
    }

    public static string FormatInteractiveObjects(IEnumerable<RpInteractiveObjectRule>? rules)
        => rules == null
            ? string.Empty
            : string.Join(Environment.NewLine, rules.Select(rule =>
                $"{rule.Name} | {rule.AppliesToTileOrTag} | {rule.Interaction} | {rule.WorldEffect} | {rule.NarrativeEffect} | {rule.Constraints}"));

    public static List<RpInteractiveObjectRule> ParseInteractiveObjects(string? value)
    {
        var result = new List<RpInteractiveObjectRule>();
        foreach (var line in ParseLines(value))
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
