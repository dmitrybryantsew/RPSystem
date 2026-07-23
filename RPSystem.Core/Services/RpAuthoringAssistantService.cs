using System.Text;
using System.Text.RegularExpressions;
using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

public sealed class RpAuthoringAssistantService(IRpTextCompletionClient completionClient)
{
    public async Task<RpAuthoringDraftResult> DraftAsync(
        RpAuthoringTargetKind targetKind,
        string userIdea,
        string provider,
        string apiKey,
        string model)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            return new RpAuthoringDraftResult { Success = false, ErrorMessage = "No model or API key configured." };
        }

        if (string.IsNullOrWhiteSpace(userIdea))
        {
            return new RpAuthoringDraftResult { Success = false, ErrorMessage = "Describe what you want drafted first." };
        }

        var prompt = BuildPrompt(targetKind, userIdea);
        string raw;
        try
        {
            raw = await completionClient.GenerateTextAsync(provider, apiKey, model, prompt);
        }
        catch (Exception ex)
        {
            return new RpAuthoringDraftResult { Success = false, ErrorMessage = $"Draft request failed: {ex.Message}" };
        }

        var safety = RpContentSafetyClassifier.ClassifySafety(raw);
        if (safety is RpImportSafetyState.DisabledPromptInjection or RpImportSafetyState.DisabledUnsafe)
        {
            return new RpAuthoringDraftResult
            {
                Success = false,
                SafetyState = safety,
                ErrorMessage = "Drafted content failed the safety check and was discarded."
            };
        }

        var parsed = ParseKeyedBlocks(raw);
        var fields = MapFields(targetKind, parsed);

        return new RpAuthoringDraftResult { Success = true, SafetyState = safety, Fields = fields };
    }

    private static string BuildPrompt(RpAuthoringTargetKind targetKind, string userIdea)
    {
        var header =
            "You are drafting structured content for a tile-based roleplay simulation's authoring tools. " +
            "A human will review and edit everything you write before it is used — do not worry about being " +
            "perfectly final, just produce a strong first draft. " +
            "Respond ONLY with plain text in the exact KEY: value format shown below. Do not use JSON, " +
            "markdown formatting, or any preamble/explanation outside the specified keys. " +
            "If a key's description says it holds multiple lines, put each line on its own line after the key, " +
            "indented or not, until the next KEY: is reached. Do not invent extra keys.\n\n" +
            $"The human's idea: {userIdea}\n\n" +
            "Respond using exactly these keys:\n";

        var schema = targetKind switch
        {
            RpAuthoringTargetKind.FactionProfile => FactionSchema,
            RpAuthoringTargetKind.ContextCharacter => ContextCharacterSchema,
            RpAuthoringTargetKind.RelationshipRule => RelationshipRuleSchema,
            RpAuthoringTargetKind.SpeciesTemplate => SpeciesTemplateSchema,
            RpAuthoringTargetKind.ContextModule => ContextModuleSchema,
            _ => string.Empty
        };

        return header + schema;
    }

    private const string FactionSchema =
        "NAME: short faction name\n" +
        "FACTION_ID: lowercase-hyphenated-slug\n" +
        "PUBLIC_DESCRIPTION: one paragraph, what outsiders would say about this faction\n" +
        "HIDDEN_DOCTRINE: one paragraph, what only members know\n" +
        "CULTURE: customs, values, day-to-day life\n" +
        "HIERARCHY: how leadership/rank works\n" +
        "GOALS: what the faction is trying to achieve\n" +
        "TABOOS: what members must never do\n" +
        "OUTSIDER_BEHAVIOR: how members treat non-members\n" +
        "MEMBER_BEHAVIOR: how members treat each other\n" +
        "APPEARANCE: visual/style markers of membership\n" +
        "TAGS: comma-separated short tags\n" +
        "RELATIONSHIP_RULES: zero or more lines, each formatted exactly as:\n" +
        "TargetNameOrTag | RelationshipType | Trust | Fear | Dependency | Loyalty | Manipulation | Suspicion | secret1;secret2 | handling rules text\n" +
        "(RelationshipType must be one of: Unknown, Ally, Subordinate, Equal, Superior, Rival, Enemy, Dependent, Captor, Captive. " +
        "Trust/Fear/Dependency/Loyalty/Manipulation/Suspicion must be integers from -100 to 100.)\n";

    private const string ContextCharacterSchema =
        "NAME: character name\n" +
        "ARCHETYPE: short archetype label (e.g. regular, elite, leader)\n" +
        "RACE: race/species name\n" +
        "ROLE: role in the world\n" +
        "PERSONALITY: personality description\n" +
        "STORY: brief backstory\n" +
        "GOAL: current goal, one sentence, imperative mood\n" +
        "LIFE_GOAL: long-term life goal, one sentence\n" +
        "TAGS: comma-separated short tags\n" +
        "SPEECH_STYLE: how this character talks\n" +
        "FIRST_ENCOUNTER: how they behave meeting someone new\n" +
        "NEGOTIATION: how they negotiate\n" +
        "ESCALATION: how conflict escalates with them\n" +
        "DE_ESCALATION: how they calm things down\n" +
        "RELATIONSHIP_HANDLING: how they treat people close to them\n" +
        "DECEPTION: how/whether they lie or mislead\n" +
        "COMBAT_PREFERENCES: how they fight if forced to\n" +
        "CAPTURE_PREFERENCES: what they do with captives, if relevant\n";

    private const string RelationshipRuleSchema =
        "TARGET: TargetNameOrTag this rule applies to\n" +
        "TYPE: one of Unknown, Ally, Subordinate, Equal, Superior, Rival, Enemy, Dependent, Captor, Captive\n" +
        "TRUST: integer -100 to 100\n" +
        "FEAR: integer -100 to 100\n" +
        "DEPENDENCY: integer -100 to 100\n" +
        "LOYALTY: integer -100 to 100\n" +
        "MANIPULATION: integer -100 to 100\n" +
        "SUSPICION: integer -100 to 100\n" +
        "KNOWN_SECRETS: semicolon-separated list, may be empty\n" +
        "HANDLING_RULES: free text describing how this relationship should be roleplayed\n";

    private const string SpeciesTemplateSchema =
        "NAME: species template name\n" +
        "APPLIES_TO_RACE: race name this applies to\n" +
        "BODY_TYPE: one of Human, Humanoid, Quadruped, Equine, Avian, Serpentine, Construct, Changeling\n" +
        "BODY_LANGUAGE: zero or more lines formatted as key: value (e.g. \"tail wag: happiness\")\n" +
        "VOCALIZATIONS: zero or more lines formatted as key: value\n" +
        "DIET_RULES: diet description\n" +
        "ENERGY_RULES: how this species' energy/stamina/resource system should be treated\n" +
        "MAGIC_RULES: magic ability rules if any, otherwise \"none\"\n" +
        "ANATOMY_MODIFIERS: zero or more lines formatted as key: value\n" +
        "TAGS: comma-separated short tags\n";

    private const string ContextModuleSchema =
        "NAME: short module name\n" +
        "TYPE: one of the existing RpContextModuleType values (ask for GeneralRules if unsure)\n" +
        "VISIBILITY: one of Public, WorldOnly, HiddenFromPlayer, CharacterKnown\n" +
        "PRIORITY: integer, lower runs first, default 100\n" +
        "APPLIES_TO: which characters/tags this applies to, or blank for everyone\n" +
        "TEXT: the actual rules/lore text content of this module\n";

    private static Dictionary<string, string> ParseKeyedBlocks(string? raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(raw)) return result;

        string? currentKey = null;
        var buffer = new StringBuilder();

        void Flush()
        {
            if (currentKey != null)
            {
                result[currentKey] = buffer.ToString().Trim();
            }
            buffer.Clear();
        }

        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var match = Regex.Match(line, @"^([A-Z_]+):\s?(.*)$");
            if (match.Success)
            {
                Flush();
                currentKey = match.Groups[1].Value;
                buffer.Append(match.Groups[2].Value);
            }
            else if (currentKey != null)
            {
                buffer.Append('\n').Append(line);
            }
        }
        Flush();

        return result;
    }

    private static Dictionary<string, string> MapFields(RpAuthoringTargetKind targetKind, Dictionary<string, string> parsed)
    {
        var map = new Dictionary<string, string>();

        void Copy(string sourceKey, string targetProperty)
        {
            if (parsed.TryGetValue(sourceKey, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                map[targetProperty] = value;
            }
        }

        switch (targetKind)
        {
            case RpAuthoringTargetKind.FactionProfile:
                Copy("NAME", "FactionName");
                Copy("FACTION_ID", "FactionId");
                Copy("PUBLIC_DESCRIPTION", "FactionPublicDescription");
                Copy("HIDDEN_DOCTRINE", "FactionHiddenDoctrine");
                Copy("CULTURE", "FactionCulture");
                Copy("HIERARCHY", "FactionHierarchy");
                Copy("GOALS", "FactionGoals");
                Copy("TABOOS", "FactionTaboos");
                Copy("OUTSIDER_BEHAVIOR", "FactionOutsiderBehavior");
                Copy("MEMBER_BEHAVIOR", "FactionMemberBehavior");
                Copy("APPEARANCE", "FactionAppearance");
                Copy("TAGS", "FactionTags");
                Copy("RELATIONSHIP_RULES", "FactionRelationshipRulesText");
                break;

            case RpAuthoringTargetKind.ContextCharacter:
                Copy("NAME", "ContextCharacterName");
                Copy("ARCHETYPE", "ContextCharacterArchetype");
                Copy("RACE", "ContextCharacterRace");
                Copy("ROLE", "ContextCharacterRole");
                Copy("PERSONALITY", "ContextCharacterPersonality");
                Copy("STORY", "ContextCharacterStory");
                Copy("GOAL", "ContextCharacterGoal");
                Copy("LIFE_GOAL", "ContextCharacterLifeGoal");
                Copy("TAGS", "ContextCharacterTags");
                Copy("SPEECH_STYLE", "ContextCharacterSpeechStyle");
                Copy("FIRST_ENCOUNTER", "ContextCharacterFirstEncounter");
                Copy("NEGOTIATION", "ContextCharacterNegotiation");
                Copy("ESCALATION", "ContextCharacterEscalation");
                Copy("DE_ESCALATION", "ContextCharacterDeEscalation");
                Copy("RELATIONSHIP_HANDLING", "ContextCharacterRelationshipHandling");
                Copy("DECEPTION", "ContextCharacterDeception");
                Copy("COMBAT_PREFERENCES", "ContextCharacterCombatPreferences");
                Copy("CAPTURE_PREFERENCES", "ContextCharacterCapturePreferences");
                break;

            case RpAuthoringTargetKind.RelationshipRule:
                Copy("TARGET", "RelationshipTargetNameOrTag");
                Copy("TYPE", "RelationshipType");
                Copy("TRUST", "RelationshipTrustText");
                Copy("FEAR", "RelationshipFearText");
                Copy("DEPENDENCY", "RelationshipDependencyText");
                Copy("LOYALTY", "RelationshipLoyaltyText");
                Copy("MANIPULATION", "RelationshipManipulationText");
                Copy("SUSPICION", "RelationshipSuspicionText");
                Copy("KNOWN_SECRETS", "RelationshipKnownSecretsText");
                Copy("HANDLING_RULES", "RelationshipHandlingRules");
                break;

            case RpAuthoringTargetKind.SpeciesTemplate:
                Copy("NAME", "SpeciesTemplateName");
                Copy("APPLIES_TO_RACE", "SpeciesTemplateRace");
                Copy("BODY_TYPE", "SpeciesTemplateBodyType");
                Copy("BODY_LANGUAGE", "SpeciesBodyLanguageText");
                Copy("VOCALIZATIONS", "SpeciesVocalizationsText");
                Copy("DIET_RULES", "SpeciesDietRules");
                Copy("ENERGY_RULES", "SpeciesEnergyRules");
                Copy("MAGIC_RULES", "SpeciesMagicRules");
                Copy("ANATOMY_MODIFIERS", "SpeciesAnatomyModifiersText");
                Copy("TAGS", "SpeciesTagsText");
                break;

            case RpAuthoringTargetKind.ContextModule:
                Copy("NAME", "ContextModuleName");
                Copy("TYPE", "ContextModuleType");
                Copy("VISIBILITY", "ContextModuleVisibility");
                Copy("PRIORITY", "ContextModulePriorityText");
                Copy("APPLIES_TO", "ContextModuleAppliesTo");
                Copy("TEXT", "ContextModuleText");
                break;
        }

        return map;
    }
}
