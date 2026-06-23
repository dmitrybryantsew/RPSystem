using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

/// <summary>
/// Reusable fixture builder for RP tests. Creates deterministic worlds
/// without MAUI dependencies.
/// </summary>
public static class RpTestWorldBuilder
{
    public static World CreateMinimalWorld(int size = 5)
    {
        var world = new World
        {
            Name = "Test World",
            Lore = "A minimal test world.",
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 12, Minute = 0, SecondsPerTick = 6 }
        };

        int half = size / 2;
        for (int x = -half; x <= half; x++)
        {
            for (int z = -half; z <= half; z++)
            {
                var tile = new Tile
                {
                    Position = new Vec3Int(x, 0, z),
                    Solidity = TileSolidity.Empty,
                    BulkMaterial = MaterialType.Air,
                    BulkState = MaterialState.Gas,
                    Temperature = 293.15f
                };
                tile.Sides[(int)Direction.Floor] = new Side
                {
                    Direction = Direction.Floor,
                    Material = MaterialType.Rock,
                    Health = 40,
                    IsPassable = true
                };
                world.Tiles[tile.Position] = tile;
            }
        }

        // Add one obstacle tile
        if (world.Tiles.TryGetValue(new Vec3Int(1, 0, 1), out var obstacle))
        {
            obstacle.Solidity = TileSolidity.Solid;
            obstacle.BulkMaterial = MaterialType.Rock;
            obstacle.BulkState = MaterialState.Solid;
            obstacle.BulkHealth = 100;
        }

        // Add player
        var player = new Character
        {
            Name = "Test Player",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human),
            FactionId = "player",
            RpTags = ["creature", "sapient", "player"],
            Vitals = new RpVitals
            {
                HealthMax = 100, HealthCurrent = 100,
                ManaMax = 50, ManaCurrent = 50,
                FocusMax = 30, FocusCurrent = 30,
                StaminaMax = 100, StaminaCurrent = 100,
                ManaRegenPerTick = 2, FocusRegenPerTick = 2, StaminaRegenPerTick = 3
            }
        };
        AddCharacter(world, player);

        // Add changeling NPC
        var npc = new Character
        {
            Name = "Test Changeling",
            Race = "Changeling",
            Position = new Vec3Int(-1, 0, -1),
            BodyType = BodyTypeKind.Changeling,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Changeling),
            FactionId = "cavern_vitae_brood",
            RpTags = ["creature", "sapient", "changeling"],
            Vitals = new RpVitals
            {
                HealthMax = 80, HealthCurrent = 80,
                ManaMax = 20, ManaCurrent = 20,
                FocusMax = 40, FocusCurrent = 40,
                StaminaMax = 90, StaminaCurrent = 90
            }
        };
        AddCharacter(world, npc);

        // Add interactive object
        var item = new Item
        {
            Name = "test lever",
            Description = "A rusty lever on the wall.",
            Material = MaterialType.Metal,
            Weight = 2f,
            Position = new Vec3Int(2, 0, 0),
            Tags = ["interactive", "mechanism"]
        };
        world.Items[item.Id] = item;
        if (world.Tiles.TryGetValue(item.Position.Value, out var itemTile))
        {
            itemTile.OccupantIds.Add(item.Id);
        }

        // Add one world context entry
        world.WorldContexts.Add(new RpWorldContextEntry
        {
            Name = "Test Context",
            IsEnabled = true,
            RulesText = "Basic test rules."
        });

        RpSimulationService.UpdatePerception(world);
        return world;
    }

    public static RpFactionProfile CreateCavernVitaeBroodFixture()
    {
        return new RpFactionProfile
        {
            FactionId = "cavern_vitae_brood",
            Name = "Cavern Vitae Brood",
            IsEnabled = true,
            Visibility = RpContextVisibility.WorldOnly,
            ParentSpeciesOrRace = "changeling",
            PublicDescription = "A secretive changeling collective adapted to subterranean life.",
            HiddenDoctrine = "Essence is the currency of survival and ascension.",
            CultureText = "Communal hierarchy centered on the Queen. Roles are assigned by aptitude.",
            HierarchyText = "Queen > Noble Commander > Mage/Guard > Scout/Infiltrator > Worker/Brood Tender/Essence Gatherer",
            GoalsText = "Expand territory, gather essence, protect the brood.",
            TaboosText = "Wasting essence, disobeying the Queen, revealing the brood to outsiders.",
            OutsiderBehavior = "Hostile or evasive unless the outsider demonstrates value.",
            MemberBehavior = "Cooperative within role boundaries; deferential to superiors.",
            AppearanceText = "Translucent carapace, jagged horn, bioluminescent markings when fed.",
            AnatomyOverridesText = "Carapace plating adds natural armor. Wings enable short flight bursts.",
            MagicRules = "Essence-fueled magic. Mana regenerates slowly without feeding.",
            ResourceRules = "hp=80; mana=20; focus=40; stamina=90",
            TagsText = "faction:cavern_vitae_brood, species:changeling, biology:essence-feeding",
            Roles = CreateBroodRoles(),
            Abilities = CreateBroodAbilities(),
            RelationshipRules = CreateBroodRelationshipRules()
        };
    }

    private static List<RpFactionRole> CreateBroodRoles()
    {
        return
        [
            new() { Name = "Worker", IsEnabled = true, AppliesToRoleOrTag = "role:worker", DefaultStatsText = "hp=80; mana=10; focus=20; stamina=100" },
            new() { Name = "Scout", IsEnabled = true, AppliesToRoleOrTag = "role:scout", DefaultStatsText = "hp=70; mana=15; focus=35; stamina=110" },
            new() { Name = "Guard", IsEnabled = true, AppliesToRoleOrTag = "role:guard", DefaultStatsText = "hp=120; mana=15; focus=30; stamina=90" },
            new() { Name = "Brood Tender", IsEnabled = true, AppliesToRoleOrTag = "role:brood_tender", DefaultStatsText = "hp=70; mana=25; focus=40; stamina=80" },
            new() { Name = "Essence Gatherer", IsEnabled = true, AppliesToRoleOrTag = "role:essence_gatherer", DefaultStatsText = "hp=75; mana=30; focus=35; stamina=95" },
            new() { Name = "Infiltrator", IsEnabled = true, AppliesToRoleOrTag = "role:infiltrator", DefaultStatsText = "hp=65; mana=20; focus=45; stamina=100" },
            new() { Name = "Mage", IsEnabled = true, AppliesToRoleOrTag = "role:mage", DefaultStatsText = "hp=60; mana=50; focus=50; stamina=70" },
            new() { Name = "Noble Commander", IsEnabled = true, AppliesToRoleOrTag = "role:noble_commander", DefaultStatsText = "hp=100; mana=30; focus=40; stamina=90" },
            new() { Name = "Queen", IsEnabled = true, AppliesToRoleOrTag = "role:queen", DefaultStatsText = "hp=150; mana=60; focus=60; stamina=80" }
        ];
    }

    private static List<RpAbility> CreateBroodAbilities()
    {
        return
        [
            new()
            {
                Id = "brood_commune",
                Name = "Brood Commune",
                Description = "Telepathic link with nearby brood members.",
                TargetKind = RpAbilityTargetKind.Self,
                PrimaryResource = RpAbilityResource.Focus,
                FocusCost = 5,
                Range = 4,
                TickCost = 1,
                Tags = ["telepathy", "communication"]
            },
            new()
            {
                Id = "resin_bind",
                Name = "Resin Bind",
                Description = "Spray sticky resin to restrain a target.",
                TargetKind = RpAbilityTargetKind.Character,
                DamageType = RpDamageType.Physical,
                PrimaryResource = RpAbilityResource.Stamina,
                StaminaCost = 15,
                Range = 3,
                TickCost = 1,
                CooldownTicks = 3,
                Tags = ["restrain", "role:guard"]
            },
            new()
            {
                Id = "royal_decree",
                Name = "Royal Decree",
                Description = "Issue an irresistible command to lesser brood members.",
                TargetKind = RpAbilityTargetKind.Character,
                PrimaryResource = RpAbilityResource.Mana,
                ManaCost = 30,
                FocusCost = 10,
                Range = 6,
                TickCost = 2,
                CooldownTicks = 5,
                Tags = ["command", "role:queen"]
            }
        ];
    }

    private static List<RpRelationshipRule> CreateBroodRelationshipRules()
    {
        return
        [
            new() { TargetNameOrTag = "player", Type = RpRelationshipType.Rival, Trust = 10, Fear = 30, Suspicion = 60, HandlingRules = "Observe from shadows. Do not engage unless threatened." },
            new() { TargetNameOrTag = "outsider", Type = RpRelationshipType.Enemy, Trust = 0, Fear = 20, Suspicion = 80, HandlingRules = "Drive away or capture for essence extraction." },
            new() { TargetNameOrTag = "faction:cavern_vitae_brood", Type = RpRelationshipType.Ally, Trust = 70, Loyalty = 80, Dependency = 40, HandlingRules = "Cooperate according to role hierarchy." }
        ];
    }

    public static RpSpeciesTemplate CreateChangelingSpeciesTemplate()
    {
        return new RpSpeciesTemplate
        {
            Name = "Changeling",
            AppliesToRace = "changeling",
            BodyType = BodyTypeKind.Changeling,
            BodyLanguage = new Dictionary<string, string>
            {
                ["agitated"] = "wings flutter rapidly, carapace darkens",
                ["calm"] = "slow wing fold, soft bioluminescence",
                ["threatening"] = "horn lowered, wings spread wide"
            },
            Vocalizations = new Dictionary<string, string>
            {
                ["hiss"] = "warning or displeasure",
                ["click"] = "acknowledgment or coordination signal",
                ["hum"] = "contentment or communal bonding"
            },
            DietRules = "Feeds on ambient magical essence. Cannot digest solid food.",
            EnergyRules = "Mana regenerates at half rate without recent feeding.",
            MagicRules = "Innate essence manipulation. Horn focuses spellcasting.",
            AnatomyModifiers = new Dictionary<string, string>
            {
                ["carapace"] = "natural armor equivalent to light leather",
                ["wings"] = "short burst flight, not sustained",
                ["horn"] = "spell focus, vulnerable if broken"
            },
            Tags = ["species:changeling", "biology:essence-feeding", "magic:innate"]
        };
    }

    private static void AddCharacter(World world, Character character)
    {
        world.Characters[character.Id] = character;
        if (world.Tiles.TryGetValue(character.Position, out var tile))
        {
            tile.OccupantIds.Add(character.Id);
        }
    }
}
