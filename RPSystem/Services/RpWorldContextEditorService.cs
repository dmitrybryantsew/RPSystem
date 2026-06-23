using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpWorldContextEditorService
{
    public RpFactionProfile AddFactionProfile(RpWorldContextEntry context)
    {
        var next = (context.Factions?.Count ?? 0) + 1;
        var profile = new RpFactionProfile
        {
            FactionId = $"faction_{next}",
            Name = $"Faction {next}",
            IsEnabled = true,
            Visibility = RpContextVisibility.WorldOnly
        };

        context.Factions ??= [];
        context.Factions.Add(profile);
        return profile;
    }

    public string FormatFactionRoles(IEnumerable<RpFactionRole>? roles)
        => roles == null
            ? string.Empty
            : string.Join(Environment.NewLine, roles.Select(role =>
                $"{role.Name} | {role.AppliesToRoleOrTag} | {role.Description} | {role.DefaultStatsText} | {role.BehaviorText} | {role.EquipmentText} | {role.TagsText} | {(role.IsEnabled ? "on" : "off")}"));

    public List<RpFactionRole> ParseFactionRoles(string? value)
    {
        var result = new List<RpFactionRole>();
        foreach (var line in ParseLines(value))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            result.Add(new RpFactionRole
            {
                Name = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(0)) ? "Role" : parts.ElementAtOrDefault(0) ?? "Role",
                AppliesToRoleOrTag = parts.ElementAtOrDefault(1) ?? string.Empty,
                Description = parts.ElementAtOrDefault(2) ?? string.Empty,
                DefaultStatsText = parts.ElementAtOrDefault(3) ?? string.Empty,
                BehaviorText = parts.ElementAtOrDefault(4) ?? string.Empty,
                EquipmentText = parts.ElementAtOrDefault(5) ?? string.Empty,
                TagsText = parts.ElementAtOrDefault(6) ?? string.Empty,
                IsEnabled = !string.Equals(parts.ElementAtOrDefault(7), "off", StringComparison.OrdinalIgnoreCase)
            });
        }

        return result;
    }

    private static List<string> ParseLines(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
}
