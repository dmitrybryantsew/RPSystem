using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpCharacterCompositionService
{
    public Character CreateCharacterFromProfile(World world, RpWorldContextEntry? context, RpWorldContextCharacter profile, Vec3Int position)
    {
        var speciesTemplate = FindSpeciesTemplateForProfile(context, profile);
        var factionProfile = FindFactionProfileForProfile(context, profile);
        var tags = SplitList(profile.TagsText);
        ApplySpeciesTags(speciesTemplate, tags);
        ApplyFactionTags(factionProfile, profile, tags);

        var bodyType = speciesTemplate?.BodyType ?? profile.BodyType;
        var character = new Character
        {
            Name = profile.Name,
            Race = profile.Race,
            Position = position,
            PersonalityTraits = SplitList(profile.PersonalityText),
            Ideals = string.IsNullOrWhiteSpace(profile.RoleInWorld) ? [] : [profile.RoleInWorld],
            Desires = string.IsNullOrWhiteSpace(profile.StoryText) ? [] : [profile.StoryText],
            Abilities = SplitList(profile.AbilityText),
            RpAbilities = BuildProfileAbilities(profile, factionProfile),
            RpTags = tags,
            Mood = "calm",
            FactionId = string.IsNullOrWhiteSpace(profile.FactionId) ? factionProfile?.FactionId : profile.FactionId,
            CurrentGoal = new Goal { Description = profile.GoalText },
            LifeGoal = new Goal { Description = string.IsNullOrWhiteSpace(profile.LifeGoalText) ? profile.GoalText : profile.LifeGoalText },
            BodyType = bodyType,
            Body = RpBodyFactory.CreateBody(bodyType),
            Vitals = new RpVitals
            {
                HealthMax = 100,
                HealthCurrent = 100,
                FocusMax = tags.Contains("sapient", StringComparer.OrdinalIgnoreCase) ? 30 : 0,
                FocusCurrent = tags.Contains("sapient", StringComparer.OrdinalIgnoreCase) ? 30 : 0,
                StaminaMax = 100,
                StaminaCurrent = 100,
                FocusRegenPerTick = 2,
                StaminaRegenPerTick = 3
            },
            ActionSpeeds = new RpActionSpeeds { MoveSpeed = 1, AttackSpeed = 1, CastSpeed = 1 },
            StaminaMax = 100,
            StaminaCurrent = 100
        };

        if (!character.RpTags.Contains("creature", StringComparer.OrdinalIgnoreCase))
        {
            character.RpTags.Add("creature");
        }

        ApplySpeciesTemplateToCharacter(character, speciesTemplate);
        ApplyFactionProfileToCharacter(character, factionProfile, profile);
        ApplyRelationshipRulesToCharacter(world, character, profile.RelationshipRules);
        ApplyRelationshipRulesToCharacter(world, character, factionProfile?.RelationshipRules);
        RpCreatureService.EnsureCreatureStats(character);
        return character;
    }

    public static RpSpeciesTemplate? FindSpeciesTemplateForProfile(RpWorldContextEntry? context, RpWorldContextCharacter profile)
        => (context?.SpeciesTemplates ?? [])
            .FirstOrDefault(template => SpeciesTemplateMatchesProfile(template, profile));

    public static RpFactionProfile? FindFactionProfileForProfile(RpWorldContextEntry? context, RpWorldContextCharacter profile)
        => (context?.Factions ?? [])
            .FirstOrDefault(faction => faction.IsEnabled && FactionProfileMatchesProfile(faction, profile));

    public static bool FactionProfileMatchesProfile(RpFactionProfile faction, RpWorldContextCharacter profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.FactionId) &&
            string.Equals(profile.FactionId, faction.FactionId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var profileTags = SplitList(profile.TagsText);
        var targets = SplitList(faction.AppliesTo);
        return targets.Any(target =>
            string.Equals(target, profile.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, profile.Race, StringComparison.OrdinalIgnoreCase) ||
            profileTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool SpeciesTemplateMatchesProfile(RpSpeciesTemplate template, RpWorldContextCharacter profile)
    {
        if (template.BodyType == profile.BodyType)
        {
            return true;
        }

        var profileTags = SplitList(profile.TagsText);
        var targets = SplitList(template.AppliesToRace);
        if (targets.Any(target =>
            string.Equals(target, profile.Race, StringComparison.OrdinalIgnoreCase) ||
            profileTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        return (template.Tags ?? []).Any(templateTag =>
            string.Equals(templateTag, profile.Race, StringComparison.OrdinalIgnoreCase) ||
            profileTags.Any(tag => string.Equals(tag, templateTag, StringComparison.OrdinalIgnoreCase)));
    }

    public static List<RpAbility> BuildProfileAbilities(RpWorldContextCharacter profile, RpFactionProfile? faction)
    {
        var result = new List<RpAbility>();
        if (faction != null)
        {
            result.AddRange((faction.Abilities ?? [])
                .Where(ability => FactionAbilityMatchesProfile(ability, profile))
                .Select(CloneAbility));

            foreach (var role in (faction.Roles ?? []).Where(candidate => candidate.IsEnabled && FactionRoleMatchesProfile(candidate, profile)))
            {
                result.AddRange((role.Abilities ?? []).Select(CloneAbility));
            }
        }

        result.AddRange(profile.StructuredAbilities?.Select(CloneAbility) ?? []);
        return result
            .GroupBy(ability => string.IsNullOrWhiteSpace(ability.Id) ? ability.Name : ability.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
    }

    public static bool FactionRoleMatchesProfile(RpFactionRole role, RpWorldContextCharacter profile)
    {
        var targets = SplitList(role.AppliesToRoleOrTag);
        if (targets.Count == 0)
        {
            return false;
        }

        var profileTags = SplitList(profile.TagsText);
        return targets.Any(target =>
            string.Equals(target, profile.RoleInWorld, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, profile.Archetype, StringComparison.OrdinalIgnoreCase) ||
            profileTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)));
    }

    public static bool FactionAbilityMatchesProfile(RpAbility ability, RpWorldContextCharacter profile)
    {
        var roleTags = (ability.Tags ?? [])
            .Where(tag => tag.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag["role:".Length..])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        if (roleTags.Count == 0)
        {
            return true;
        }

        var profileTags = SplitList(profile.TagsText);
        return roleTags.Any(roleTag =>
            string.Equals(roleTag, profile.RoleInWorld, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleTag, profile.Archetype, StringComparison.OrdinalIgnoreCase) ||
            profileTags.Any(tag =>
                string.Equals(tag, roleTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tag, $"role:{roleTag}", StringComparison.OrdinalIgnoreCase)));
    }

    public static void ApplyStatModifier(Character character, string key, string value)
    {
        if (!float.TryParse(value, out var amount) || amount < 0)
        {
            return;
        }

        if (key.Equals("health", StringComparison.OrdinalIgnoreCase) || key.Equals("hp", StringComparison.OrdinalIgnoreCase))
        {
            character.Vitals.HealthMax = amount;
            character.Vitals.HealthCurrent = amount;
        }
        else if (key.Equals("mana", StringComparison.OrdinalIgnoreCase))
        {
            character.Vitals.ManaMax = amount;
            character.Vitals.ManaCurrent = amount;
        }
        else if (key.Equals("focus", StringComparison.OrdinalIgnoreCase))
        {
            character.Vitals.FocusMax = amount;
            character.Vitals.FocusCurrent = amount;
        }
        else if (key.Equals("stamina", StringComparison.OrdinalIgnoreCase))
        {
            character.Vitals.StaminaMax = amount;
            character.Vitals.StaminaCurrent = amount;
            character.StaminaMax = amount;
            character.StaminaCurrent = amount;
        }
    }

    private static void ApplySpeciesTags(RpSpeciesTemplate? speciesTemplate, List<string> tags)
    {
        if (speciesTemplate == null)
        {
            return;
        }

        foreach (var tag in speciesTemplate.Tags ?? [])
        {
            AddDistinct(tags, tag);
        }

        if (!string.IsNullOrWhiteSpace(speciesTemplate.MagicRules))
        {
            AddDistinct(tags, "magical");
        }
    }

    private static void ApplySpeciesTemplateToCharacter(Character character, RpSpeciesTemplate? template)
    {
        if (template == null)
        {
            return;
        }

        foreach (var modifier in template.AnatomyModifiers ?? [])
        {
            ApplyAnatomyModifier(character, modifier.Key, modifier.Value);
        }
    }

    private static void ApplyFactionTags(RpFactionProfile? faction, RpWorldContextCharacter profile, List<string> tags)
    {
        if (faction == null)
        {
            return;
        }

        foreach (var tag in SplitList(faction.TagsText).Concat([$"faction:{faction.FactionId}"]))
        {
            AddDistinct(tags, tag);
        }

        foreach (var role in (faction.Roles ?? []).Where(candidate => candidate.IsEnabled && FactionRoleMatchesProfile(candidate, profile)))
        {
            foreach (var tag in SplitList(role.TagsText).Concat(SplitList(role.AppliesToRoleOrTag)))
            {
                AddDistinct(tags, tag);
            }
        }
    }

    private static void ApplyFactionProfileToCharacter(Character character, RpFactionProfile? faction, RpWorldContextCharacter profile)
    {
        if (faction == null)
        {
            return;
        }

        foreach (var modifier in ParseDictionary(faction.AnatomyOverridesText))
        {
            ApplyAnatomyModifier(character, modifier.Key, modifier.Value);
        }

        if (!string.IsNullOrWhiteSpace(faction.MagicRules) && character.Vitals.ManaMax <= 0)
        {
            character.Vitals.ManaMax = 50;
            character.Vitals.ManaCurrent = 50;
            character.Vitals.ManaRegenPerTick = 3;
        }

        foreach (var role in (faction.Roles ?? []).Where(candidate => candidate.IsEnabled && FactionRoleMatchesProfile(candidate, profile)))
        {
            foreach (var modifier in ParseInlineDictionary(role.DefaultStatsText))
            {
                ApplyStatModifier(character, modifier.Key, modifier.Value);
            }
        }
    }

    private static void ApplyRelationshipRulesToCharacter(World world, Character character, IEnumerable<RpRelationshipRule>? rules)
    {
        foreach (var rule in rules ?? [])
        {
            foreach (var matchedCharacter in world.Characters.Values.Where(candidate => RelationshipRuleMatchesCharacter(rule, candidate)))
            {
                character.KnownCharacters.Add(new Relationship
                {
                    CharacterId = matchedCharacter.Id,
                    Name = matchedCharacter.Name,
                    Type = rule.Type,
                    Trust = rule.Trust,
                    Fear = rule.Fear,
                    Dependency = rule.Dependency,
                    Loyalty = rule.Loyalty,
                    Manipulation = rule.Manipulation,
                    Suspicion = rule.Suspicion,
                    KnownSecrets = (rule.KnownSecrets ?? []).ToList(),
                    SharedHistory = string.IsNullOrWhiteSpace(rule.HandlingRules) ? [] : [rule.HandlingRules]
                });
            }
        }
    }

    private static bool RelationshipRuleMatchesCharacter(RpRelationshipRule rule, Character character)
    {
        var targets = SplitList(rule.TargetNameOrTag);
        return targets.Any(target =>
            string.Equals(target, character.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target, character.Race, StringComparison.OrdinalIgnoreCase) ||
            character.RpTags.Any(tag => string.Equals(tag, target, StringComparison.OrdinalIgnoreCase)));
    }

    private static void ApplyAnatomyModifier(Character character, string key, string value)
    {
        foreach (var part in character.Body.Where(candidate => BodyPartMatchesModifier(candidate, key)))
        {
            if (!part.Functions.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                part.Functions.Add(value);
            }
        }
    }

    private static bool BodyPartMatchesModifier(BodyPart part, string key)
        => !string.IsNullOrWhiteSpace(key) &&
            (part.Name.Contains(key, StringComparison.OrdinalIgnoreCase) ||
             part.Id.Contains(key, StringComparison.OrdinalIgnoreCase) ||
             part.Role.ToString().Contains(key, StringComparison.OrdinalIgnoreCase));

    private static RpAbility CloneAbility(RpAbility ability)
        => new()
        {
            Id = ability.Id,
            Name = ability.Name,
            Description = ability.Description,
            TargetKind = ability.TargetKind,
            DamageType = ability.DamageType,
            PrimaryResource = ability.PrimaryResource,
            ManaCost = ability.ManaCost,
            FocusCost = ability.FocusCost,
            StaminaCost = ability.StaminaCost,
            Damage = ability.Damage,
            Range = ability.Range,
            TickCost = ability.TickCost,
            CooldownTicks = ability.CooldownTicks,
            RemainingCooldownTicks = ability.RemainingCooldownTicks,
            RangeText = ability.RangeText,
            TargetRules = ability.TargetRules,
            Constraints = ability.Constraints,
            WorldEffect = ability.WorldEffect,
            NarrativeEffect = ability.NarrativeEffect,
            AllowedUsage = ability.AllowedUsage,
            ForbiddenUsage = ability.ForbiddenUsage,
            Tags = (ability.Tags ?? []).ToList()
        };

    private static Dictionary<string, string> ParseInlineDictionary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var normalized = value
            .Replace(';', '\n')
            .Replace(',', '\n')
            .Replace('/', '\n');
        return ParseDictionary(normalized);
    }

    private static Dictionary<string, string> ParseDictionary(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitListPreserveValues(value))
        {
            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                separator = line.IndexOf(':');
            }

            if (separator < 0)
            {
                result[line] = string.Empty;
                continue;
            }

            var key = line[..separator].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                result[key] = line[(separator + 1)..].Trim();
            }
        }

        return result;
    }

    private static List<string> SplitList(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static List<string> SplitListPreserveValues(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

    private static void AddDistinct(List<string> tags, string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
        }
    }
}
