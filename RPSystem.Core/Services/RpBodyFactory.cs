using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public static class RpBodyFactory
{
    public static BodyTypeKind InferBodyType(string? raceOrSpecies)
    {
        if (string.IsNullOrWhiteSpace(raceOrSpecies))
        {
            return BodyTypeKind.Human;
        }

        var text = raceOrSpecies.Trim().ToLowerInvariant();
        if (text.Contains("equine") || text.Contains("horse") || text.Contains("pony"))
        {
            return BodyTypeKind.Equine;
        }

        if (text.Contains("changeling"))
        {
            return BodyTypeKind.Changeling;
        }

        if (text.Contains("construct") || text.Contains("robot") || text.Contains("android") || text.Contains("golem"))
        {
            return BodyTypeKind.Construct;
        }

        if (text.Contains("bird") || text.Contains("avian") || text.Contains("gryphon") || text.Contains("griffin"))
        {
            return BodyTypeKind.Avian;
        }

        if (text.Contains("snake") || text.Contains("serpent") || text.Contains("wyrm"))
        {
            return BodyTypeKind.Serpentine;
        }

        if (text.Contains("wolf") || text.Contains("cat") || text.Contains("dog") || text.Contains("deer"))
        {
            return BodyTypeKind.Quadruped;
        }

        if (text.Contains("human"))
        {
            return BodyTypeKind.Human;
        }

        return BodyTypeKind.Humanoid;
    }

    public static List<BodyPart> CreateBody(BodyTypeKind bodyType)
        => bodyType switch
        {
            BodyTypeKind.Equine => EquineBody(),
            BodyTypeKind.Changeling => ChangelingBody(),
            BodyTypeKind.Quadruped => QuadrupedBody(bodyType),
            BodyTypeKind.Avian => AvianBody(),
            BodyTypeKind.Serpentine => SerpentineBody(),
            BodyTypeKind.Construct => ConstructBody(),
            BodyTypeKind.Humanoid => HumanoidBody(bodyType),
            _ => HumanoidBody(BodyTypeKind.Human)
        };

    public static void EnsureBody(Character character)
    {
        if (character.Body.Count == 0)
        {
            character.BodyType = InferBodyType(character.Race);
            character.Body = CreateBody(character.BodyType);
            return;
        }

        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in character.Body)
        {
            part.BodyType = character.BodyType;

            if (string.IsNullOrWhiteSpace(part.Id))
            {
                part.Id = MakeUniqueId(part.Name, usedIds);
            }
            else if (!usedIds.Add(part.Id))
            {
                part.Id = MakeUniqueId(part.Id, usedIds);
            }

            if (part.HpMax <= 0)
            {
                part.HpMax = 10;
            }

            if (part.HpCurrent <= 0)
            {
                part.HpCurrent = part.HpMax;
            }
        }
    }

    public static string DescribeBody(Character character)
    {
        if (character.Body.Count == 0)
        {
            return "Body: unspecified.";
        }

        var healthy = character.Body.Count(part => part.HpCurrent >= part.HpMax * 0.75f);
        var damaged = character.Body.Count(part => part.HpCurrent < part.HpMax * 0.75f);
        var holding = character.Body
            .Where(part => part.HeldItemId.HasValue)
            .Select(part => part.Name)
            .ToList();
        var slots = character.Body
            .SelectMany(part => part.WearSlots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8);

        var summary = $"{character.BodyType}: {character.Body.Count} parts, {healthy} healthy";
        if (damaged > 0)
        {
            summary += $", {damaged} damaged";
        }

        var slotText = string.Join(", ", slots);
        if (!string.IsNullOrWhiteSpace(slotText))
        {
            summary += $"\nWear slots: {slotText}";
        }

        if (holding.Count > 0)
        {
            summary += $"\nHolding with: {string.Join(", ", holding)}";
        }

        return summary;
    }

    private static List<BodyPart> HumanoidBody(BodyTypeKind bodyType)
        =>
        [
            Part("head", "head", bodyType, BodyPartRole.Head, BodyPartSide.None, null, 10, true, ["sight", "hearing", "smell", "speech"], ["headwear", "eyewear", "mask"]),
            Part("neck", "neck", bodyType, BodyPartRole.Neck, BodyPartSide.None, "torso", 8, true, ["breathing"], ["neckwear"]),
            Part("torso", "torso", bodyType, BodyPartRole.Core, BodyPartSide.None, null, 24, true, ["breathing", "balance"], ["torso", "back", "belt"]),
            Part("left_arm", "left arm", bodyType, BodyPartRole.Arm, BodyPartSide.Left, "torso", 12, false, ["reach", "push"], ["armwear"]),
            Part("right_arm", "right arm", bodyType, BodyPartRole.Arm, BodyPartSide.Right, "torso", 12, false, ["reach", "push"], ["armwear"]),
            Part("left_hand", "left hand", bodyType, BodyPartRole.Hand, BodyPartSide.Left, "left_arm", 8, false, ["grip", "fine manipulation"], ["glove", "ring"], canHold: true),
            Part("right_hand", "right hand", bodyType, BodyPartRole.Hand, BodyPartSide.Right, "right_arm", 8, false, ["grip", "fine manipulation"], ["glove", "ring"], canHold: true),
            Part("left_leg", "left leg", bodyType, BodyPartRole.Leg, BodyPartSide.Left, "torso", 14, false, ["locomotion", "balance"], ["legwear"]),
            Part("right_leg", "right leg", bodyType, BodyPartRole.Leg, BodyPartSide.Right, "torso", 14, false, ["locomotion", "balance"], ["legwear"]),
            Part("left_foot", "left foot", bodyType, BodyPartRole.Foot, BodyPartSide.Left, "left_leg", 8, false, ["stance"], ["footwear"]),
            Part("right_foot", "right foot", bodyType, BodyPartRole.Foot, BodyPartSide.Right, "right_leg", 8, false, ["stance"], ["footwear"])
        ];

    private static List<BodyPart> QuadrupedBody(BodyTypeKind bodyType)
        =>
        [
            Part("head", "head", bodyType, BodyPartRole.Head, BodyPartSide.None, "neck", 12, true, ["sight", "hearing", "smell", "bite"], ["headwear", "mask"]),
            Part("neck", "neck", bodyType, BodyPartRole.Neck, BodyPartSide.None, "torso", 12, true, ["breathing", "posture"], ["collar", "harness"]),
            Part("torso", "torso", bodyType, BodyPartRole.Core, BodyPartSide.None, null, 30, true, ["breathing", "balance"], ["torso", "back", "harness"]),
            Part("front_left_leg", "front left leg", bodyType, BodyPartRole.Leg, BodyPartSide.FrontLeft, "torso", 14, false, ["locomotion", "stance"], ["legwear"]),
            Part("front_right_leg", "front right leg", bodyType, BodyPartRole.Leg, BodyPartSide.FrontRight, "torso", 14, false, ["locomotion", "stance"], ["legwear"]),
            Part("rear_left_leg", "rear left leg", bodyType, BodyPartRole.Leg, BodyPartSide.RearLeft, "torso", 16, false, ["locomotion", "kick"], ["legwear"]),
            Part("rear_right_leg", "rear right leg", bodyType, BodyPartRole.Leg, BodyPartSide.RearRight, "torso", 16, false, ["locomotion", "kick"], ["legwear"]),
            Part("front_left_foot", "front left foot", bodyType, BodyPartRole.Foot, BodyPartSide.FrontLeft, "front_left_leg", 8, false, ["traction"], ["footwear"]),
            Part("front_right_foot", "front right foot", bodyType, BodyPartRole.Foot, BodyPartSide.FrontRight, "front_right_leg", 8, false, ["traction"], ["footwear"]),
            Part("rear_left_foot", "rear left foot", bodyType, BodyPartRole.Foot, BodyPartSide.RearLeft, "rear_left_leg", 8, false, ["traction"], ["footwear"]),
            Part("rear_right_foot", "rear right foot", bodyType, BodyPartRole.Foot, BodyPartSide.RearRight, "rear_right_leg", 8, false, ["traction"], ["footwear"]),
            Part("tail", "tail", bodyType, BodyPartRole.Tail, BodyPartSide.None, "torso", 8, false, ["balance", "expression"], [])
        ];

    private static List<BodyPart> EquineBody()
    {
        var body = QuadrupedBody(BodyTypeKind.Equine);
        foreach (var foot in body.Where(part => part.Role == BodyPartRole.Foot))
        {
            foot.Name = foot.Name.Replace("foot", "hoof", StringComparison.Ordinal);
            foot.Functions = ["traction", "impact"];
            foot.WearSlots = ["hoofwear"];
        }

        body.First(part => part.Id == "torso").WearSlots.Add("saddle");
        body.First(part => part.Id == "neck").WearSlots.Add("mane");
        body.Add(Part("horn", "horn", BodyTypeKind.Equine, BodyPartRole.Horn, BodyPartSide.None, "head", 7, false, ["spell focus"], ["hornwear"]));
        return body;
    }

    private static List<BodyPart> ChangelingBody()
    {
        var body = QuadrupedBody(BodyTypeKind.Changeling);
        body.Add(Part("horn", "jagged horn", BodyTypeKind.Changeling, BodyPartRole.Horn, BodyPartSide.None, "head", 8, false, ["spell focus"], ["hornwear"]));
        body.Add(Part("left_wing", "left translucent wing", BodyTypeKind.Changeling, BodyPartRole.Wing, BodyPartSide.Left, "torso", 9, false, ["flight", "balance"], ["wingwear"]));
        body.Add(Part("right_wing", "right translucent wing", BodyTypeKind.Changeling, BodyPartRole.Wing, BodyPartSide.Right, "torso", 9, false, ["flight", "balance"], ["wingwear"]));
        body.First(part => part.Id == "torso").WearSlots.Add("carapace");
        return body;
    }

    private static List<BodyPart> AvianBody()
        =>
        [
            Part("head", "head", BodyTypeKind.Avian, BodyPartRole.Head, BodyPartSide.None, "neck", 8, true, ["sight", "hearing", "beak"], ["headwear"]),
            Part("neck", "neck", BodyTypeKind.Avian, BodyPartRole.Neck, BodyPartSide.None, "torso", 7, true, ["breathing"], ["neckwear"]),
            Part("torso", "torso", BodyTypeKind.Avian, BodyPartRole.Core, BodyPartSide.None, null, 18, true, ["breathing", "balance"], ["torso", "harness"]),
            Part("left_wing", "left wing", BodyTypeKind.Avian, BodyPartRole.Wing, BodyPartSide.Left, "torso", 12, false, ["flight", "balance"], ["wingwear"]),
            Part("right_wing", "right wing", BodyTypeKind.Avian, BodyPartRole.Wing, BodyPartSide.Right, "torso", 12, false, ["flight", "balance"], ["wingwear"]),
            Part("left_leg", "left leg", BodyTypeKind.Avian, BodyPartRole.Leg, BodyPartSide.Left, "torso", 9, false, ["locomotion", "perch"], ["legwear"]),
            Part("right_leg", "right leg", BodyTypeKind.Avian, BodyPartRole.Leg, BodyPartSide.Right, "torso", 9, false, ["locomotion", "perch"], ["legwear"]),
            Part("left_foot", "left talon", BodyTypeKind.Avian, BodyPartRole.Foot, BodyPartSide.Left, "left_leg", 7, false, ["grip", "traction"], ["footwear"], canHold: true),
            Part("right_foot", "right talon", BodyTypeKind.Avian, BodyPartRole.Foot, BodyPartSide.Right, "right_leg", 7, false, ["grip", "traction"], ["footwear"], canHold: true),
            Part("tail", "tail feathers", BodyTypeKind.Avian, BodyPartRole.Tail, BodyPartSide.None, "torso", 6, false, ["balance", "steering"], [])
        ];

    private static List<BodyPart> SerpentineBody()
        =>
        [
            Part("head", "head", BodyTypeKind.Serpentine, BodyPartRole.Head, BodyPartSide.None, "neck", 10, true, ["sight", "smell", "bite"], ["headwear"]),
            Part("neck", "neck", BodyTypeKind.Serpentine, BodyPartRole.Neck, BodyPartSide.None, "upper_body", 10, true, ["breathing"], ["neckwear"]),
            Part("upper_body", "upper body", BodyTypeKind.Serpentine, BodyPartRole.Core, BodyPartSide.None, null, 18, true, ["breathing", "posture"], ["torso", "harness"]),
            Part("mid_body", "mid body", BodyTypeKind.Serpentine, BodyPartRole.Core, BodyPartSide.None, "upper_body", 20, false, ["locomotion", "coil"], ["bodywear"]),
            Part("lower_body", "lower body", BodyTypeKind.Serpentine, BodyPartRole.Core, BodyPartSide.None, "mid_body", 18, false, ["locomotion", "coil"], ["bodywear"]),
            Part("tail", "tail", BodyTypeKind.Serpentine, BodyPartRole.Tail, BodyPartSide.None, "lower_body", 12, false, ["balance", "grip"], ["tailwear"])
        ];

    private static List<BodyPart> ConstructBody()
        =>
        [
            Part("sensor_head", "sensor head", BodyTypeKind.Construct, BodyPartRole.Sensor, BodyPartSide.None, "core_chassis", 12, true, ["vision", "audio", "signal"], ["sensor_mount"]),
            Part("core_chassis", "core chassis", BodyTypeKind.Construct, BodyPartRole.Core, BodyPartSide.None, null, 32, true, ["power", "processing"], ["chassis", "back_mount"]),
            Part("left_manipulator", "left manipulator", BodyTypeKind.Construct, BodyPartRole.Hand, BodyPartSide.Left, "core_chassis", 14, false, ["grip", "tool use"], ["tool_mount"], canHold: true),
            Part("right_manipulator", "right manipulator", BodyTypeKind.Construct, BodyPartRole.Hand, BodyPartSide.Right, "core_chassis", 14, false, ["grip", "tool use"], ["tool_mount"], canHold: true),
            Part("left_support", "left support strut", BodyTypeKind.Construct, BodyPartRole.Leg, BodyPartSide.Left, "core_chassis", 16, false, ["locomotion", "stability"], ["leg_mount"]),
            Part("right_support", "right support strut", BodyTypeKind.Construct, BodyPartRole.Leg, BodyPartSide.Right, "core_chassis", 16, false, ["locomotion", "stability"], ["leg_mount"]),
            Part("utility_mount", "utility mount", BodyTypeKind.Construct, BodyPartRole.ToolMount, BodyPartSide.None, "core_chassis", 10, false, ["mounted tool"], ["tool_mount"], canHold: true)
        ];

    private static BodyPart Part(
        string id,
        string name,
        BodyTypeKind bodyType,
        BodyPartRole role,
        BodyPartSide side,
        string? parentId,
        float hp,
        bool critical,
        List<string> functions,
        List<string> wearSlots,
        bool canHold = false)
        => new()
        {
            Id = id,
            Name = name,
            BodyType = bodyType,
            Role = role,
            Side = side,
            ParentPartId = parentId,
            HpMax = hp,
            HpCurrent = hp,
            IsCritical = critical,
            Functions = functions,
            WearSlots = wearSlots,
            CanHoldItem = canHold
        };

    private static string MakeUniqueId(string name, HashSet<string> usedIds)
    {
        var id = string.Join("_", (string.IsNullOrWhiteSpace(name) ? "part" : name)
            .ToLowerInvariant()
            .Split([' ', '-', '.', '/', '\\'], StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(id))
        {
            id = "part";
        }

        var candidate = id;
        var suffix = 2;
        while (!usedIds.Add(candidate))
        {
            candidate = $"{id}_{suffix++}";
        }

        return candidate;
    }
}
