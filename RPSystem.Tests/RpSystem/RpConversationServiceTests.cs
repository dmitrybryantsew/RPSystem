using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpConversationServiceTests
{
    [Fact]
    public void StartConversation_Succeeds_WithConsciousPartner()
    {
        var (world, player, partner) = CreateConversationWorld();
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());

        var (success, status, opening) = service.StartConversation(world, player.Id, partner.Id);

        success.Should().BeTrue();
        status.Should().Contain(partner.Name);
        world.ActiveConversation.Should().NotBeNull();
        world.ActiveConversation!.Transcript.Should().ContainSingle();
        opening.Should().NotBeNull();
    }

    [Fact]
    public void StartConversation_Fails_WhenAlreadyInConversation()
    {
        var (world, player, partner) = CreateConversationWorld();
        var otherPartner = new Character
        {
            Name = "Other",
            Race = "Human",
            Position = new Vec3Int(2, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        };
        world.Characters[otherPartner.Id] = otherPartner;
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());
        service.StartConversation(world, player.Id, partner.Id);

        var (success, _, _) = service.StartConversation(world, player.Id, otherPartner.Id);

        success.Should().BeFalse();
        world.ActiveConversation!.PartnerCharacterId.Should().Be(partner.Id);
    }

    [Fact]
    public void StartConversation_Fails_WhenPartnerUnconscious()
    {
        var (world, player, partner) = CreateConversationWorld();
        partner.Vitals.LifeState = RpLifeState.Unconscious;
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());

        var (success, _, _) = service.StartConversation(world, player.Id, partner.Id);

        success.Should().BeFalse();
        world.ActiveConversation.Should().BeNull();
    }

    [Fact]
    public void StartConversation_AdvancesSetupPhaseToFirstContact()
    {
        var (world, player, partner) = CreateConversationWorld();
        world.WorldContexts[0].SceneState.Phase = RpScenePhase.Setup;
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());

        service.StartConversation(world, player.Id, partner.Id);

        world.WorldContexts[0].SceneState.Phase.Should().Be(RpScenePhase.FirstContact);
    }

    [Fact]
    public void AddPlayerLine_Throws_WhenNoActiveConversation()
    {
        var world = new World { Name = "Test" };
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());

        Action act = () => service.AddPlayerLine(world, "hello");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddPlayerLine_AppendsToTranscriptAndPartnerPerceivedLog()
    {
        var (world, player, partner) = CreateConversationWorld();
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());
        service.StartConversation(world, player.Id, partner.Id);

        var evt = service.AddPlayerLine(world, "hello there");

        world.ActiveConversation!.Transcript.Should().Contain(e => e.Description == "hello there");
        partner.PerceivedLog.Should().Contain(e => e.Description == "hello there");
        evt.Should().NotBeNull();
    }

    [Fact]
    public void AddPlayerLine_TrimsTranscriptAtMaxLength()
    {
        var (world, player, partner) = CreateConversationWorld();
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());
        service.StartConversation(world, player.Id, partner.Id);
        for (var i = 0; i < RpConversationService.MaxTranscriptLength; i++)
        {
            world.ActiveConversation!.Transcript.Add(new NarrativeEvent
            {
                Tick = i,
                ActorName = "Filler",
                Description = $"filler-{i}"
            });
        }

        service.AddPlayerLine(world, "overflow");

        world.ActiveConversation!.Transcript.Count.Should().Be(RpConversationService.MaxTranscriptLength);
        world.ActiveConversation.Transcript[^1].Description.Should().Be("overflow");
        world.ActiveConversation.Transcript[0].Description.Should().StartWith("filler-");
    }

    [Fact]
    public void EndConversation_ClearsActiveConversationAndSummarizesIntoPartnerMemory()
    {
        var (world, player, partner) = CreateConversationWorld();
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());
        service.StartConversation(world, player.Id, partner.Id);
        service.AddPlayerLine(world, "hello");
        partner.MemorySummaries.Clear();

        var closing = service.EndConversation(world);

        world.ActiveConversation.Should().BeNull();
        partner.MemorySummaries.Should().ContainSingle(s => s.StartsWith("[Conversation"));
        closing.Should().NotBeNull();
    }

    [Fact]
    public void EndConversation_NoOp_WhenNoneActive()
    {
        var world = new World { Name = "Test" };
        var service = new RpConversationService(new RpRuleBasedTextSummarizer());

        var closing = service.EndConversation(world);

        closing.Should().BeNull();
        world.ActiveConversation.Should().BeNull();
    }

    private static (World world, Character player, Character partner) CreateConversationWorld()
    {
        var world = new World { Name = "Conv Test" };
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
        return (world, player, partner);
    }
}
