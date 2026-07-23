using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpMarkdownImportService
{
    public RpImportResult ImportIntoWorld(World world, string fallbackName, string content, Vec3Int origin)
    {
        var title = ExtractMarkdownTitle(content, fallbackName);
        var importedContext = CreateSanitizedContext(title, content);
        world.WorldContexts ??= [];
        world.WorldContexts.Add(importedContext);

        if (LooksLikeCharacter(content))
        {
            var position = FindNearestFreeTile(world, origin);
            var race = ExtractMarkdownValue(content, "race") ?? ExtractMarkdownValue(content, "species") ?? "Unknown";
            var bodyType = RpBodyFactory.InferBodyType(race);
            var character = new Character
            {
                Name = title,
                Race = race,
                Position = position,
                Mood = ExtractMarkdownValue(content, "mood") ?? "neutral",
                PersonalityTraits = ExtractMarkdownList(content, "personality", "traits"),
                Desires = ExtractMarkdownList(content, "desires", "wants"),
                Abilities = ExtractMarkdownList(content, "abilities", "skills"),
                RpTags = ["creature", "sapient"],
                CurrentGoal = new Goal { Description = ExtractMarkdownValue(content, "goal") ?? "Observe and respond to the situation." },
                LifeGoal = new Goal { Description = ExtractMarkdownValue(content, "life goal") ?? "Find a place in the world." },
                BodyType = bodyType,
                Body = RpBodyFactory.CreateBody(bodyType),
                Vitals = new RpVitals { HealthMax = 100, HealthCurrent = 100, FocusMax = 30, FocusCurrent = 30, StaminaMax = 100, StaminaCurrent = 100 }
            };
            if (character.Body.Any(part => part.Role == BodyPartRole.Horn))
            {
                character.RpTags.Add("horn-channel");
            }

            RpCreatureService.EnsureCreatureStats(character);

            world.Characters[character.Id] = character;
            if (world.Tiles.TryGetValue(position, out var tile))
            {
                tile.OccupantIds.Add(character.Id);
            }

            RpSimulationService.UpdatePerception(world);
            importedContext.Characters.Add(CreateProfileFromCharacter(character, content));
            return new RpImportResult(character, importedContext, $"Imported character and sanitized context: {character.Name}.");
        }

        world.Name = title;
        world.Lore = content.Length > 4000 ? content[..4000] : content;
        RpSimulationService.UpdatePerception(world);
        return new RpImportResult(null, importedContext, $"Imported sanitized world lore context: {title}.");
    }

    private static RpWorldContextEntry CreateSanitizedContext(string title, string content)
    {
        var context = new RpWorldContextEntry
        {
            Name = $"{title} Import",
            IsEnabled = true,
            RulesText = string.Empty,
            Modules = [],
            Characters = []
        };

        var sections = SplitMarkdownSections(content);
        if (sections.Count == 0)
        {
            sections.Add(new RpMarkdownSection(title, content));
        }

        var priority = 100;
        foreach (var section in sections)
        {
            var safety = RpContentSafetyClassifier.ClassifySafety(section.Body);
            var module = new RpContextModule
            {
                Name = string.IsNullOrWhiteSpace(section.Title) ? "Imported Text" : section.Title,
                IsEnabled = safety == RpImportSafetyState.Allowed || safety == RpImportSafetyState.Unreviewed,
                Type = safety is RpImportSafetyState.DisabledPromptInjection or RpImportSafetyState.DisabledUnsafe
                    ? RpContextModuleType.ImportNotes
                    : ClassifyModuleType(section.Title, section.Body),
                Visibility = GuessVisibility(section.Title, section.Body),
                Priority = priority,
                SourceLabel = title,
                Text = safety is RpImportSafetyState.DisabledPromptInjection or RpImportSafetyState.DisabledUnsafe
                    ? string.Empty
                    : section.Body.Trim(),
                ImportSafety = safety,
                SanitizationNote = safety switch
                {
                    RpImportSafetyState.DisabledPromptInjection => "Disabled during import because this section looked like prompt injection or app/model instruction override text.",
                    RpImportSafetyState.DisabledUnsafe => "Disabled during import because this section appears to require safety review before use.",
                    RpImportSafetyState.NeedsReview => "Imported enabled, but should be reviewed before use in simulation.",
                    _ => string.Empty
                }
            };

            context.Modules.Add(module);
            priority += 10;
        }

        AddSpeciesTemplates(context, content);
        return context;
    }

    private static List<RpMarkdownSection> SplitMarkdownSections(string content)
    {
        var result = new List<RpMarkdownSection>();
        string currentTitle = string.Empty;
        var currentLines = new List<string>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith("#"))
            {
                AddCurrentSection();
                currentTitle = line.TrimStart('#').Trim();
                continue;
            }

            currentLines.Add(line);
        }

        AddCurrentSection();
        return result
            .Where(section => !string.IsNullOrWhiteSpace(section.Body))
            .ToList();

        void AddCurrentSection()
        {
            var body = string.Join(Environment.NewLine, currentLines).Trim();
            if (!string.IsNullOrWhiteSpace(body))
            {
                result.Add(new RpMarkdownSection(currentTitle, body));
            }

            currentLines.Clear();
        }
    }

    private static RpContextModuleType ClassifyModuleType(string title, string body)
    {
        var text = $"{title}\n{body}";
        if (ContainsAny(text, "core identity", "identity", "name", "role", "archetype"))
        {
            return RpContextModuleType.CoreIdentity;
        }

        if (ContainsAny(text, "ability", "power", "spell", "magic", "cooldown", "mana", "cost", "range"))
        {
            return RpContextModuleType.AbilityMechanics;
        }

        if (ContainsAny(text, "interaction protocol", "protocol", "speech", "negotiation", "deception", "combat", "capture"))
        {
            return RpContextModuleType.InteractionProtocols;
        }

        if (ContainsAny(text, "psychology", "personality", "insecurity", "motive", "temperament"))
        {
            return RpContextModuleType.Psychology;
        }

        if (ContainsAny(text, "behavior"))
        {
            return RpContextModuleType.CharacterBehavior;
        }

        if (ContainsAny(text, "species", "biology", "vocalization", "diet"))
        {
            return RpContextModuleType.SpeciesBiology;
        }

        if (ContainsAny(text, "body", "physical", "anatomy", "species", "biology", "vocalization", "diet"))
        {
            return RpContextModuleType.PhysicalTraits;
        }

        if (ContainsAny(text, "relationship", "trust", "loyalty", "enemy", "subordinate", "secret"))
        {
            return RpContextModuleType.RelationshipRules;
        }

        if (ContainsAny(text, "scene", "pacing", "escalation", "continuity", "no reset", "foreshadow"))
        {
            return RpContextModuleType.SceneRules;
        }

        if (ContainsAny(text, "environment", "terrain", "hazard", "clue", "object", "domain"))
        {
            return RpContextModuleType.EnvironmentRules;
        }

        if (ContainsAny(text, "history", "lore", "world", "setting", "faction"))
        {
            return RpContextModuleType.WorldLore;
        }

        return RpContextModuleType.ImportNotes;
    }

    private static RpContextVisibility GuessVisibility(string title, string body)
    {
        var text = $"{title}\n{body}";
        if (ContainsAny(text, "hidden", "secret", "private", "motive", "deception", "strategy"))
        {
            return RpContextVisibility.HiddenFromPlayer;
        }

        if (ContainsAny(text, "known to", "character knows", "memory"))
        {
            return RpContextVisibility.CharacterKnown;
        }

        if (ContainsAny(text, "public", "visible", "common knowledge"))
        {
            return RpContextVisibility.Public;
        }

        return RpContextVisibility.WorldOnly;
    }

    private static void AddSpeciesTemplates(RpWorldContextEntry context, string content)
    {
        if (ContainsAny(content, "changeling"))
        {
            context.SpeciesTemplates.Add(new RpSpeciesTemplate
            {
                Name = "Changeling",
                AppliesToRace = "Changeling",
                BodyType = BodyTypeKind.Changeling,
                EnergyRules = "Use setting-specific magical or emotional-energy rules only if an enabled world module defines them.",
                MagicRules = "Magical abilities must obey visible resources, range, tick cost, and the active ability mechanics modules.",
                Tags = ["changeling", "magical", "sapient"]
            });
        }

        if (ContainsAny(content, "pony", "equine", "unicorn", "pegasus"))
        {
            context.SpeciesTemplates.Add(new RpSpeciesTemplate
            {
                Name = "Equine",
                AppliesToRace = "Pony",
                BodyType = BodyTypeKind.Equine,
                MagicRules = "Unicorn-style magic should use mana or focus and must be represented by structured abilities when it affects the world.",
                Tags = ["equine", "sapient"]
            });
        }
    }

    private static RpWorldContextCharacter CreateProfileFromCharacter(Character character, string content)
        => new()
        {
            Name = character.Name,
            IsNamedCharacter = true,
            Archetype = character.Race,
            Race = character.Race,
            BodyType = character.BodyType,
            FactionId = character.FactionId ?? string.Empty,
            RoleInWorld = character.CurrentGoal.Description,
            PersonalityText = string.Join(", ", character.PersonalityTraits),
            StoryText = string.Join(Environment.NewLine, character.Ideals.Concat(character.Desires)),
            AbilityText = string.Join(", ", character.Abilities),
            GoalText = character.CurrentGoal.Description,
            LifeGoalText = character.LifeGoal.Description,
            TagsText = string.Join(", ", character.RpTags),
            BehaviorProtocol = new RpCharacterBehaviorProtocol
            {
                SpeechStyle = ExtractSectionBody(content, "speech") ?? string.Empty,
                FirstEncounterBehavior = ExtractSectionBody(content, "first encounter") ?? string.Empty,
                NegotiationStyle = ExtractSectionBody(content, "negotiation") ?? string.Empty,
                EscalationPattern = ExtractSectionBody(content, "escalation") ?? string.Empty,
                DeEscalationPattern = ExtractSectionBody(content, "de-escalation") ?? ExtractSectionBody(content, "deescalation") ?? string.Empty,
                RelationshipHandling = ExtractSectionBody(content, "relationship") ?? string.Empty,
                DeceptionMode = ExtractSectionBody(content, "deception") ?? string.Empty,
                CombatPreferences = ExtractSectionBody(content, "combat") ?? string.Empty,
                CapturePreferences = ExtractSectionBody(content, "capture") ?? string.Empty
            }
        };

    private static string? ExtractSectionBody(string content, string name)
        => SplitMarkdownSections(content)
            .FirstOrDefault(section => section.Title.Contains(name, StringComparison.OrdinalIgnoreCase))
            ?.Body;

    private static bool LooksLikeCharacter(string content)
        => content.Contains("race", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("species", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("personality", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("traits", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("goal", StringComparison.OrdinalIgnoreCase);

    private static Vec3Int FindNearestFreeTile(World world, Vec3Int origin)
    {
        foreach (var radius in Enumerable.Range(0, 8))
        {
            foreach (var tile in world.Tiles.Values)
            {
                if (Math.Abs(tile.Position.X - origin.X) + Math.Abs(tile.Position.Y - origin.Y) + Math.Abs(tile.Position.Z - origin.Z) != radius)
                {
                    continue;
                }

                if (tile.Solidity != TileSolidity.Solid && tile.OccupantIds.All(id => !world.Characters.ContainsKey(id)))
                {
                    return tile.Position;
                }
            }
        }

        return origin;
    }

    private static string ExtractMarkdownTitle(string content, string fallback)
    {
        var line = content.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("# "));
        return string.IsNullOrWhiteSpace(line) ? fallback : line.TrimStart('#').Trim();
    }

    private static string? ExtractMarkdownValue(string content, string key)
    {
        var line = content.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith($"{key}:", StringComparison.OrdinalIgnoreCase));
        return line == null ? null : line[(line.IndexOf(':') + 1)..].Trim();
    }

    private static List<string> ExtractMarkdownList(string content, params string[] sectionNames)
    {
        var lines = content.Split('\n');
        var result = new List<string>();
        bool active = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("#"))
            {
                active = sectionNames.Any(section => line.Contains(section, StringComparison.OrdinalIgnoreCase));
                continue;
            }

            if (active && (line.StartsWith("- ") || line.StartsWith("* ")))
            {
                result.Add(line[2..].Trim());
            }
        }

        return result.Take(12).ToList();
    }

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
}

public sealed record RpImportResult(Character? ImportedCharacter, RpWorldContextEntry? ImportedContext, string Message);

internal sealed record RpMarkdownSection(string Title, string Body);
