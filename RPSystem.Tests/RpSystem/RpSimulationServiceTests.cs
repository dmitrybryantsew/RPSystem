using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpSimulationServiceTests
{
    [Fact]
    public void BuildSnapshot_IncludesMatchingFactionSpeciesRelationshipsAndActions()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        npc.RpAbilities.Add(RpAbilityService.CreateFireballAbility());
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Brood Context",
            Factions = [RpTestWorldBuilder.CreateCavernVitaeBroodFixture()],
            SpeciesTemplates = [RpTestWorldBuilder.CreateChangelingSpeciesTemplate()]
        });
        RpSimulationService.UpdatePerception(world);

        var service = new RpSimulationService(new RpFakeLlmClient());
        var snapshot = service.BuildSnapshot(world, npc);

        snapshot.FocalFactionProfiles.Should().ContainSingle(f => f.FactionId == "cavern_vitae_brood");
        snapshot.FocalFactionProfiles[0].Roles.Should().HaveCount(9);
        snapshot.FocalSpeciesTemplates.Should().ContainSingle(s => s.Name == "Changeling");
        snapshot.FocalRelationshipRules.Should().Contain(r => r.TargetNameOrTag == "player" && r.Type == RpRelationshipType.Rival);
        snapshot.AvailableActions.Should().Contain(a => a.Action.Type == ActionType.Use && a.Action.Payload == RpAbilityService.FireballId);
    }

    [Fact]
    public void BuildSnapshot_ExcludesWorldOnlyFactionForPlayerFocalSnapshot()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        world.WorldContexts.Clear();
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Brood Context",
            Factions = [RpTestWorldBuilder.CreateCavernVitaeBroodFixture()]
        });

        var service = new RpSimulationService(new RpFakeLlmClient());
        var snapshot = service.BuildSnapshot(world, npc, npc.Id);

        snapshot.FocalFactionProfiles.Should().BeEmpty();
    }

    [Fact]
    public void BuildSnapshot_IncludesVisibleGoalObjectInteractionAction()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var item = world.Items.Values.First(i => i.Name == "test lever");
        item.GoalAffordances.Add(new RpGoalObjectAffordance
        {
            Kind = RpGoalObjectKind.Workstation,
            Name = "mechanism control",
            GoalKeywords = ["control", "mechanism"],
            Priority = 90
        });
        RpSimulationService.UpdatePerception(world);
        var service = new RpSimulationService(new RpFakeLlmClient());

        var snapshot = service.BuildSnapshot(world, player, player.Id);

        snapshot.AvailableActions.Should().Contain(action =>
            action.Action.Type == ActionType.Interact &&
            action.Action.TargetId == item.Id &&
            action.Label.Contains("mechanism control"));
    }

    [Fact]
    public async Task TickAsync_WithFakeLlm_BuildsSnapshotForNpcAndAdvancesClock()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        npc.Vitals.ManaCurrent = 0;
        npc.Vitals.ManaRegenPerTick = 2;
        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        var events = await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        fake.CallCount.Should().Be(1);
        fake.Snapshots.Should().ContainSingle(s => s.FocalCharacter.Id == npc.Id);
        world.Clock.TickCount.Should().Be(1);
        world.Clock.Second.Should().Be(6);
        npc.Vitals.ManaCurrent.Should().Be(2);
        events.Should().Contain(e => e.ActorName == npc.Name);
    }

    [Fact]
    public async Task TickAsync_DecrementsAbilityCooldown()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var ability = RpAbilityService.CreateFireballAbility();
        ability.RemainingCooldownTicks = 2;
        player.RpAbilities.Add(ability);
        var service = new RpSimulationService(new RpFakeLlmClient());

        await service.TickAsync(world, useLlm: false, provider: "", apiKey: "", model: "", player.Id, CancellationToken.None);

        ability.RemainingCooldownTicks.Should().Be(1);
    }

    [Fact]
    public async Task TickAsync_PopulatesSceneAndContinuityOnSnapshot()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        var context = world.WorldContexts[0];
        context.SceneState.Phase = RpScenePhase.Negotiation;
        context.Continuity.Flags.Add("test-flag");
        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        fake.Snapshots.Should().ContainSingle(s => s.FocalCharacter.Id == npc.Id);
        var snapshot = fake.Snapshots.First(s => s.FocalCharacter.Id == npc.Id);
        snapshot.ActiveSceneState.Should().NotBeNull();
        snapshot.ActiveSceneState!.Phase.Should().Be(RpScenePhase.Negotiation);
        snapshot.ActiveContinuity.Should().NotBeNull();
        snapshot.ActiveContinuity!.Flags.Should().Contain("test-flag");
    }

    [Fact]
    public async Task TickAsync_TrimsPerceivedLogOnlyThroughMemoryCompaction()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var player = world.Characters.Values.First(c => c.Name == "Test Player");
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        for (var i = 0; i < 30; i++)
        {
            npc.PerceivedLog.Add(new NarrativeEvent
            {
                Tick = i,
                ActorName = "Seeder",
                Description = $"event {i}"
            });
        }
        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        npc.PerceivedLog.Count.Should().BeLessThanOrEqualTo(RpMemoryCompactionService.RawPerceivedLogCap);
        npc.MemorySummaries.Should().NotBeEmpty();
    }

    [Fact]
    public void BuildSnapshot_IncludesRecentMemorySummariesAndHistoryDigest()
    {
        var world = RpTestWorldBuilder.CreateMinimalWorld();
        var npc = world.Characters.Values.First(c => c.Name == "Test Changeling");
        npc.MemorySummaries.AddRange(["m1", "m2", "m3", "m4", "m5", "m6", "m7"]);
        world.History.Add(new HistoryYear { Year = 1, Summary = "epoch one summary" });
        world.History.Add(new HistoryYear { Year = 2, Summary = "epoch two summary" });
        world.History.Add(new HistoryYear { Year = 3, Summary = string.Empty });
        world.History.Add(new HistoryYear { Year = 4, Summary = "epoch four summary" });
        world.History.Add(new HistoryYear { Year = 5, Summary = "epoch five summary" });
        world.History.Add(new HistoryYear { Year = 6, Summary = "epoch six summary" });

        var service = new RpSimulationService(new RpFakeLlmClient());
        var snapshot = service.BuildSnapshot(world, npc);

        snapshot.RecentMemorySummaries.Should().HaveCount(5);
        snapshot.RecentMemorySummaries.Should().Equal(["m3", "m4", "m5", "m6", "m7"]);
        snapshot.HistoryDigest.Should().HaveCount(4);
        snapshot.HistoryDigest.Should().Equal(["epoch two summary", "epoch four summary", "epoch five summary", "epoch six summary"]);
        snapshot.HistoryDigest.Should().NotContain(s => string.IsNullOrWhiteSpace(s));
    }
}
