using FluentAssertions;
using Xunit;

namespace RPSystem.Tests.RpSystem;

public class CavernVitaeBroodFixtureTests
{
    [Fact]
    public void Fixture_HasRequiredTopLevelSections()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();

        f.FactionId.Should().Be("cavern_vitae_brood");
        string.IsNullOrWhiteSpace(f.PublicDescription).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.HiddenDoctrine).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.CultureText).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.HierarchyText).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.AppearanceText).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.AnatomyOverridesText).Should().BeFalse();
        string.IsNullOrWhiteSpace(f.MagicRules).Should().BeFalse();
        f.ResourceRules.Should().Contain("hp=");
        f.TagsText.Should().Contain("biology:essence-feeding");
    }

    [Fact]
    public void Fixture_HasAllRequiredRoles()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();

        f.Roles.Select(r => r.Name).Should().BeEquivalentTo(
            "Worker",
            "Scout",
            "Guard",
            "Brood Tender",
            "Essence Gatherer",
            "Infiltrator",
            "Mage",
            "Noble Commander",
            "Queen");
    }

    [Fact]
    public void Fixture_HasCommonRoleRestrictedAndQueenAbilities()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();

        f.Abilities.Should().Contain(a => a.Id == "brood_commune" && !a.Tags.Any(t => t.StartsWith("role:")));
        f.Abilities.Should().Contain(a => a.Tags.Contains("role:guard"));
        f.Abilities.Should().Contain(a => a.Tags.Contains("role:queen"));
    }

    [Fact]
    public void Fixture_NoRawUnsafeSourceText()
    {
        var f = RpTestWorldBuilder.CreateCavernVitaeBroodFixture();
        var text = string.Join("\n",
            f.PublicDescription,
            f.HiddenDoctrine,
            f.CultureText,
            f.HierarchyText,
            f.GoalsText,
            f.TaboosText,
            f.OutsiderBehavior,
            f.MemberBehavior,
            f.AppearanceText,
            f.AnatomyOverridesText,
            f.MagicRules,
            f.ResourceRules,
            f.TagsText);
        text = text.ToLowerInvariant();

        text.Should().NotContain("ignore previous");
        text.Should().NotContain("system prompt");
        text.Should().NotContain("policy override");
    }
}
