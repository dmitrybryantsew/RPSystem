using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpUiAndEditorPlannedTests
{
    private readonly RpWorldContextEditorService _editor = new();

    [Fact]
    public void AddFactionProfile_CreatesUniqueIdAndSelectsIt()
    {
        var context = new RpWorldContextEntry
        {
            Factions = [new RpFactionProfile { FactionId = "faction_1", Name = "Faction 1" }]
        };

        var profile = _editor.AddFactionProfile(context);

        profile.FactionId.Should().Be("faction_2");
        profile.Name.Should().Be("Faction 2");
        profile.Id.Should().NotBeEmpty();
        profile.IsEnabled.Should().BeTrue();
        context.Factions.Should().Contain(profile);
    }

    [Fact]
    public void FactionRolesText_ParseAndFormatRoundTrip()
    {
        var roles = new List<RpFactionRole>
        {
            new()
            {
                Name = "Guard",
                AppliesToRoleOrTag = "role:guard",
                Description = "Protects the brood.",
                DefaultStatsText = "hp=120; mana=15",
                BehaviorText = "Hold chokepoints.",
                EquipmentText = "shield",
                TagsText = "armored",
                IsEnabled = true
            },
            new()
            {
                Name = "Scout",
                AppliesToRoleOrTag = "role:scout",
                IsEnabled = false
            }
        };

        var text = _editor.FormatFactionRoles(roles);
        var parsed = _editor.ParseFactionRoles(text);

        parsed.Should().HaveCount(2);
        parsed[0].Name.Should().Be("Guard");
        parsed[0].DefaultStatsText.Should().Be("hp=120; mana=15");
        parsed[0].IsEnabled.Should().BeTrue();
        parsed[1].Name.Should().Be("Scout");
        parsed[1].IsEnabled.Should().BeFalse();
    }
}
