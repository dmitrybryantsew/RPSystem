using Xunit;
using FluentAssertions;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Tests.RpSystem;

public class RpBodyAndSpeciesTests
{
    [Fact]
    public void EnsureBody_CreatesExpectedHumanParts()
    {
        var c = new Character { Race = "Human", BodyType = BodyTypeKind.Human };
        RpBodyFactory.EnsureBody(c);
        c.Body.Should().Contain(p => p.Id == "head");
        c.Body.Should().Contain(p => p.Id == "torso");
        c.Body.Should().Contain(p => p.Role == BodyPartRole.Core);
    }

    [Fact]
    public void EnsureBody_CreatesExpectedChangelingParts()
    {
        var c = new Character { Race = "Changeling", BodyType = BodyTypeKind.Changeling };
        RpBodyFactory.EnsureBody(c);
        c.Body.Should().Contain(p => p.Id == "horn");
        c.Body.Should().Contain(p => p.Id == "left_wing");
        c.Body.Should().Contain(p => p.Id == "right_wing");
    }

    [Fact]
    public void EnsureBody_CreatesExpectedEquineParts()
    {
        var c = new Character { Race = "Pony Unicorn", BodyType = BodyTypeKind.Equine };

        RpBodyFactory.EnsureBody(c);

        c.Body.Should().Contain(p => p.Id == "horn");
        c.Body.Should().Contain(p => p.Role == BodyPartRole.Foot && p.Name.Contains("hoof"));
        c.Body.SelectMany(p => p.WearSlots).Should().Contain("saddle");
    }

    [Fact]
    public void EnsureBody_DoesNotDuplicatePartsWhenCalledTwice()
    {
        var c = new Character { Race = "Human", BodyType = BodyTypeKind.Human };
        RpBodyFactory.EnsureBody(c);
        int count = c.Body.Count;
        RpBodyFactory.EnsureBody(c);
        c.Body.Should().HaveCount(count);
    }

    [Fact]
    public void InferBodyType_DetectsEquine()
    {
        RpBodyFactory.InferBodyType("Pony Unicorn").Should().Be(BodyTypeKind.Equine);
    }

    [Fact]
    public void InferBodyType_DefaultsToHumanForNull()
    {
        RpBodyFactory.InferBodyType(null).Should().Be(BodyTypeKind.Human);
    }

    [Fact]
    public void CriticalBodyParts_IncludeMainControlOrgans()
    {
        var human = RpBodyFactory.CreateBody(BodyTypeKind.Human);
        var construct = RpBodyFactory.CreateBody(BodyTypeKind.Construct);

        human.Where(p => p.IsCritical).Select(p => p.Role)
            .Should().Contain([BodyPartRole.Head, BodyPartRole.Neck, BodyPartRole.Core]);
        construct.Where(p => p.IsCritical).Select(p => p.Role)
            .Should().Contain([BodyPartRole.Sensor, BodyPartRole.Core]);
    }
}
