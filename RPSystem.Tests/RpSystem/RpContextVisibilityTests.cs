using FluentAssertions;
using Xunit;
using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public class RpContextVisibilityTests
{
    [Fact]
    public void BuildSnapshot_IncludesPublicAndWorldOnlyModulesForNpcButOnlyPublicForPlayer()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var service = new RpSimulationService(new RpFakeLlmClient());
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Visibility Context",
            Modules =
            [
                Module("Public Module", RpContextVisibility.Public, "public text"),
                Module("World Module", RpContextVisibility.WorldOnly, "world text"),
                Module("Hidden Module", RpContextVisibility.HiddenFromPlayer, "hidden text")
            ]
        });

        var npcSnapshot = service.BuildSnapshot(world, npc);
        var playerSnapshot = service.BuildSnapshot(world, npc, npc.Id);

        npcSnapshot.ActiveWorldContexts.Single().Modules.Select(m => m.Name)
            .Should().Contain(["Public Module", "World Module", "Hidden Module"]);
        playerSnapshot.ActiveWorldContexts.Single().Modules.Select(m => m.Name)
            .Should().ContainSingle().Which.Should().Be("Public Module");
    }

    [Fact]
    public void BuildSnapshot_CharacterKnownModuleRequiresMatchingTarget()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var service = new RpSimulationService(new RpFakeLlmClient());
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Known Context",
            Modules =
            [
                Module("Matching Memory", RpContextVisibility.CharacterKnown, "known", appliesTo: "faction:cavern_vitae_brood"),
                Module("Other Memory", RpContextVisibility.CharacterKnown, "unknown", appliesTo: "faction:other")
            ]
        });

        var snapshot = service.BuildSnapshot(world, npc);

        snapshot.ActiveWorldContexts.Single().Modules.Should().ContainSingle(m => m.Name == "Matching Memory");
    }

    [Fact]
    public void BuildSnapshot_ExcludesDisabledContextAndDisabledModule()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var service = new RpSimulationService(new RpFakeLlmClient());
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Enabled Context",
            Modules =
            [
                Module("Enabled Module", RpContextVisibility.Public, "enabled"),
                Module("Disabled Module", RpContextVisibility.Public, "disabled", isEnabled: false)
            ]
        });
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Disabled Context",
            IsEnabled = false,
            Modules = [Module("Invisible Module", RpContextVisibility.Public, "invisible")]
        });

        var snapshot = service.BuildSnapshot(world, npc);

        snapshot.ActiveWorldContexts.Should().ContainSingle(c => c.Name == "Enabled Context");
        snapshot.ActiveWorldContexts.Single().Modules.Should().ContainSingle(m => m.Name == "Enabled Module");
    }

    private static RpContextModule Module(
        string name,
        RpContextVisibility visibility,
        string text,
        string appliesTo = "",
        bool isEnabled = true)
        => new()
        {
            Name = name,
            Visibility = visibility,
            Text = text,
            AppliesTo = appliesTo,
            IsEnabled = isEnabled
        };
}
