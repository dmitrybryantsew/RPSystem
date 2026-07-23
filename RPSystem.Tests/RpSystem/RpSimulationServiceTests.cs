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

    [Fact]
    public async Task TickAsync_ConversationFocusMode_OnlyCallsLlmForPartner()
    {
        var world = new World { Name = "Conv Focus Test" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        var player = new Character
        {
            Name = "Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var partner = new Character
        {
            Name = "Partner",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var bystander = new Character
        {
            Name = "Bystander",
            Race = "Human",
            Position = new Vec3Int(2, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[player.Id] = player;
        world.Characters[partner.Id] = partner;
        world.Characters[bystander.Id] = bystander;

        RpSimulationService.UpdatePerception(world);

        var conversationService = new RpConversationService(new RpRuleBasedTextSummarizer());
        conversationService.StartConversation(world, player.Id, partner.Id);

        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        fake.CallCount.Should().Be(1);
        fake.Snapshots[0].FocalCharacter.Id.Should().Be(partner.Id);
    }

    [Fact]
    public async Task TickAsync_ConversationFocusMode_PartnerSnapshotUsesTranscriptNotPerceivedLog()
    {
        var world = new World { Name = "Conv Transcript Test" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        var player = new Character
        {
            Name = "Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var partner = new Character
        {
            Name = "Partner",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[player.Id] = player;
        world.Characters[partner.Id] = partner;
        RpSimulationService.UpdatePerception(world);

        var conversationService = new RpConversationService(new RpRuleBasedTextSummarizer());
        conversationService.StartConversation(world, player.Id, partner.Id);
        conversationService.AddPlayerLine(world, "hello partner");

        // Seed partner's PerceivedLog with unrelated filler events
        for (var i = 0; i < 20; i++)
        {
            partner.PerceivedLog.Add(new NarrativeEvent
            {
                Tick = 100 + i,
                ActorName = "Filler",
                Description = $"filler event {i}"
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

        var snapshot = fake.Snapshots[0];
        snapshot.RecentEvents.Should().Contain(e => e.Description == "hello partner");
        snapshot.RecentEvents.Should().NotContain(e => e.Description.StartsWith("filler event"));
    }

    [Fact]
    public async Task TickAsync_ConversationFocusMode_DoesNotStartNewDecisionsForBystanders()
    {
        var world = new World { Name = "Conv Bystander Test" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        var player = new Character
        {
            Name = "Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var partner = new Character
        {
            Name = "Partner",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var bystander = new Character
        {
            Name = "Bystander",
            Race = "Human",
            Position = new Vec3Int(2, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[player.Id] = player;
        world.Characters[partner.Id] = partner;
        world.Characters[bystander.Id] = bystander;

        RpSimulationService.UpdatePerception(world);

        var conversationService = new RpConversationService(new RpRuleBasedTextSummarizer());
        conversationService.StartConversation(world, player.Id, partner.Id);

        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        bystander.ActionQueue.Should().BeEmpty();
    }

    [Fact]
    public async Task TickAsync_AutoEndsConversation_WhenPartnerBecomesUnconscious()
    {
        var world = new World { Name = "Conv AutoEnd Test" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        var player = new Character
        {
            Name = "Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var partner = new Character
        {
            Name = "Partner",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[player.Id] = player;
        world.Characters[partner.Id] = partner;
        RpSimulationService.UpdatePerception(world);

        var conversationService = new RpConversationService(new RpRuleBasedTextSummarizer());
        conversationService.StartConversation(world, player.Id, partner.Id);
        world.ActiveConversation.Should().NotBeNull();

        partner.Vitals.LifeState = RpLifeState.Unconscious;

        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        world.ActiveConversation.Should().BeNull();
    }

    [Fact]
    public async Task TickAsync_ConversationFocusMode_StillRunsPhysicsAndPerceptionForEveryone()
    {
        var world = new World { Name = "Conv Physics Test" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        var player = new Character
        {
            Name = "Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var partner = new Character
        {
            Name = "Partner",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        var bystander = new Character
        {
            Name = "Bystander",
            Race = "Human",
            Position = new Vec3Int(2, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[player.Id] = player;
        world.Characters[partner.Id] = partner;
        world.Characters[bystander.Id] = bystander;

        RpSimulationService.UpdatePerception(world);

        var conversationService = new RpConversationService(new RpRuleBasedTextSummarizer());
        conversationService.StartConversation(world, player.Id, partner.Id);

        var fake = new RpFakeLlmClient();
        fake.Enqueue(new LlmActionResponse
        {
            Note = "wait",
            Actions = [new CharacterAction { Type = ActionType.Wait, TickCost = 1 }]
        });
        var service = new RpSimulationService(fake);

        await service.TickAsync(world, useLlm: true, provider: "fake", apiKey: "key", model: "model", player.Id, CancellationToken.None);

        bystander.PerceivedState.VisibleCharacterIds.Should().Contain(player.Id);
        bystander.PerceivedState.VisibleCharacterIds.Should().Contain(partner.Id);
    }
}
