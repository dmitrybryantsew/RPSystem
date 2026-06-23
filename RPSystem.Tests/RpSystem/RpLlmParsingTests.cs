using FluentAssertions;
using Xunit;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpLlmParsingTests
{
    [Fact]
    public void ParseLlmResponse_ParsesValidMoveJsonWithNoteAndSpeech()
    {
        const string raw = """
            {"note":"go east","speech":"Moving.","actions":[{"type":"Move","targetPos":{"x":1,"y":0,"z":0},"tickCost":2,"note":"clear path"}]}
            """;

        var response = RpSimulationService.ParseLlmResponse(raw);

        response.Note.Should().Be("go east");
        response.Speech.Should().Be("Moving.");
        response.Actions.Should().ContainSingle();
        response.Actions[0].Type.Should().Be(ActionType.Move);
        response.Actions[0].TargetPos.Should().Be(new Vec3Int(1, 0, 0));
        response.Actions[0].TickCost.Should().Be(2);
    }

    [Fact]
    public void ParseLlmResponse_IgnoresTextAroundJsonObject()
    {
        const string raw = """
            ```json
            {"note":"speak","actions":[{"type":"Speak","payload":"Hello","tickCost":1}]}
            ```
            """;

        var response = RpSimulationService.ParseLlmResponse(raw);

        response.Actions.Should().ContainSingle(a => a.Type == ActionType.Speak && a.Payload == "Hello");
    }

    [Fact]
    public void ParseLlmResponse_ParsesValidUseAbilityJson()
    {
        const string raw = """
            {"note":"cast","actions":[{"type":"Use","payload":"rp_fireball","targetPos":{"x":-1,"y":0,"z":-1},"tickCost":1}]}
            """;

        var response = RpSimulationService.ParseLlmResponse(raw);

        response.Actions.Should().ContainSingle();
        response.Actions[0].Type.Should().Be(ActionType.Use);
        response.Actions[0].Payload.Should().Be(RpAbilityService.FireballId);
        response.Actions[0].TargetPos.Should().Be(new Vec3Int(-1, 0, -1));
    }

    [Fact]
    public void ParseLlmResponse_ReturnsWaitOnEmptyActions()
    {
        var response = RpSimulationService.ParseLlmResponse("""{"note":"nothing","actions":[]}""");

        response.Actions.Should().ContainSingle(a => a.Type == ActionType.Wait);
        response.Note.Should().Contain("no actions");
    }

    [Fact]
    public void ParseLlmResponse_ReturnsWaitOnInvalidJson()
    {
        var response = RpSimulationService.ParseLlmResponse("not json");

        response.Actions.Should().ContainSingle(a => a.Type == ActionType.Wait);
        response.Note.Should().Contain("JSON");
    }
}
