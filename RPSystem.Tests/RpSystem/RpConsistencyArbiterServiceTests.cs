using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpConsistencyArbiterServiceTests
{
    [Fact]
    public void ApplyStateUpdates_RelationshipDelta_UpdatesKnownCharactersEntry()
    {
        var world = CreateTwoCharacterWorld(out var focal, out var target);
        var arbiter = new RpConsistencyArbiterService();

        var events = arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            RelationshipDeltas = [new RpRelationshipDelta { TargetNameOrId = target.Name, TrustChange = 10 }]
        });

        focal.KnownCharacters.Should().ContainSingle();
        focal.KnownCharacters[0].CharacterId.Should().Be(target.Id);
        focal.KnownCharacters[0].Trust.Should().Be(10);
        events.Should().Contain(e => e.Description.Contains(target.Name));
    }

    [Fact]
    public void ApplyStateUpdates_RelationshipDelta_ClampsExcessiveChange()
    {
        var world = CreateTwoCharacterWorld(out var focal, out var target);
        var arbiter = new RpConsistencyArbiterService();

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            RelationshipDeltas = [new RpRelationshipDelta { TargetNameOrId = target.Name, TrustChange = 500 }]
        });

        focal.KnownCharacters.Should().ContainSingle();
        focal.KnownCharacters[0].Trust.Should().Be(RpConsistencyArbiterService.MaxRelationshipDeltaPerTick);
    }

    [Fact]
    public void ApplyStateUpdates_RelationshipDelta_ClampsAtUpperBound()
    {
        var world = CreateTwoCharacterWorld(out var focal, out var target);
        focal.KnownCharacters.Add(new Relationship { CharacterId = target.Id, Name = target.Name, Trust = 95 });
        var arbiter = new RpConsistencyArbiterService();

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            RelationshipDeltas = [new RpRelationshipDelta { TargetNameOrId = target.Name, TrustChange = 15 }]
        });

        focal.KnownCharacters[0].Trust.Should().Be(RpConsistencyArbiterService.MaxRelationshipValue);
    }

    [Fact]
    public void ApplyStateUpdates_RelationshipDelta_IgnoresUnknownTarget()
    {
        var world = CreateTwoCharacterWorld(out var focal, out _);
        var arbiter = new RpConsistencyArbiterService();

        var events = arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            RelationshipDeltas = [new RpRelationshipDelta { TargetNameOrId = "Nobody", TrustChange = 10 }]
        });

        focal.KnownCharacters.Should().BeEmpty();
        events.Should().BeEmpty();
    }

    [Fact]
    public void ApplyStateUpdates_SecretDisclosure_AddsSecretOnce()
    {
        var world = CreateTwoCharacterWorld(out var focal, out var target);
        var arbiter = new RpConsistencyArbiterService();
        var response = new LlmActionResponse
        {
            SecretsLearned = [new RpSecretDisclosure { TargetNameOrId = target.Name, Secret = "is afraid of fire" }]
        };

        arbiter.ApplyStateUpdates(world, focal, response);
        arbiter.ApplyStateUpdates(world, focal, response);

        focal.KnownCharacters.Should().ContainSingle();
        focal.KnownCharacters[0].KnownSecrets.Should().ContainSingle("is afraid of fire");
    }

    [Fact]
    public void ApplyStateUpdates_ContinuityUpdate_AddsAndResolvesPendingConsequence()
    {
        var world = CreateTwoCharacterWorld(out var focal, out _);
        var context = world.WorldContexts[0];
        var arbiter = new RpConsistencyArbiterService();

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            ContinuityUpdate = new RpContinuityUpdate { AddPendingConsequence = "the bridge will collapse" }
        });
        context.Continuity.PendingConsequences.Should().Contain("the bridge will collapse");

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            ContinuityUpdate = new RpContinuityUpdate { ResolvePendingConsequence = "the bridge will collapse" }
        });
        context.Continuity.PendingConsequences.Should().BeEmpty();
    }

    [Fact]
    public void ApplyStateUpdates_SceneUpdate_CapsEscalationSpendToRemainingBudget()
    {
        var world = CreateTwoCharacterWorld(out var focal, out _);
        var context = world.WorldContexts[0];
        context.SceneState.EscalationBudget = 0.05f;
        var arbiter = new RpConsistencyArbiterService();

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            SceneUpdate = new RpSceneUpdate { EscalationDelta = 1.0f }
        });

        context.SceneState.EscalationBudget.Should().Be(0f);
    }

    [Fact]
    public void ApplyStateUpdates_SceneUpdate_RefusesSkippingMultiplePhasesForward()
    {
        var world = CreateTwoCharacterWorld(out var focal, out _);
        var context = world.WorldContexts[0];
        context.SceneState.Phase = RpScenePhase.Setup;
        var arbiter = new RpConsistencyArbiterService();

        arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            SceneUpdate = new RpSceneUpdate { AdvanceToPhase = RpScenePhase.Conflict }
        });

        context.SceneState.Phase.Should().Be(RpScenePhase.Setup);
    }

    [Fact]
    public void ApplyStateUpdates_SceneUpdate_AllowsMovingBackwardAnyDistance()
    {
        var world = CreateTwoCharacterWorld(out var focal, out _);
        var context = world.WorldContexts[0];
        context.SceneState.Phase = RpScenePhase.Conflict;
        var arbiter = new RpConsistencyArbiterService();

        var events = arbiter.ApplyStateUpdates(world, focal, new LlmActionResponse
        {
            SceneUpdate = new RpSceneUpdate { AdvanceToPhase = RpScenePhase.Setup }
        });

        context.SceneState.Phase.Should().Be(RpScenePhase.Setup);
        events.Should().Contain(e => e.ActorName == "Scene" && e.Description.Contains("Setup"));
    }

    [Fact]
    public void GetActiveSceneContext_PrefersNonImportContext()
    {
        var world = new World { Name = "Test" };
        world.WorldContexts.Add(new RpWorldContextEntry { Name = "Lore Import", IsEnabled = true });
        world.WorldContexts.Add(new RpWorldContextEntry { Name = "Main Scene", IsEnabled = true });

        var resolved = RpSimulationService.GetActiveSceneContext(world);

        resolved.Should().NotBeNull();
        resolved!.Name.Should().Be("Main Scene");
    }

    private static World CreateTwoCharacterWorld(out Character focal, out Character target)
    {
        var world = new World { Name = "Two Char World" };
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Main Scene",
            IsEnabled = true,
            SceneState = new RpSceneState(),
            Continuity = new RpContinuityState()
        });

        focal = new Character
        {
            Name = "Focal",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        target = new Character
        {
            Name = "Target",
            Race = "Human",
            Position = new Vec3Int(1, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };

        world.Characters[focal.Id] = focal;
        world.Characters[target.Id] = target;

        RpSimulationService.UpdatePerception(world);
        return world;
    }
}
