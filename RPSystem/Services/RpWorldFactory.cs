using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public static class RpWorldFactory
{
    public static World CreateStarterWorld()
        => CreateCavernStarterWorld();

    public static World CreateCavernStarterWorld()
    {
        var world = new World
        {
            Name = "Basement Cavern",
            Lore = "A tight stone cavern beneath an old basement. The setting is intentionally specific: rough rock, dim crystal light, limited exits, and magic constrained by health, mana, visibility, and available space.",
            WorldContexts =
            [
                new RpWorldContextEntry
                {
                    Name = "Cavern Rules",
                    IsEnabled = true,
                    RulesText = "The current setting is a confined basement cavern. Space, line of sight, tile passability, health, mana, focus, stamina, and known character goals constrain every action. Characters should choose actions that are physically possible in the visible world.",
                    Modules =
                    [
                        new RpContextModule
                        {
                            Name = "Cavern Physical Constraints",
                            IsEnabled = true,
                            Type = RpContextModuleType.EnvironmentRules,
                            Visibility = RpContextVisibility.WorldOnly,
                            Priority = 100,
                            Text = "The current setting is a confined basement cavern. Space, line of sight, tile passability, health, mana, focus, stamina, and known character goals constrain every action. Characters should choose actions that are physically possible in the visible world."
                        }
                    ]
                },
                new RpWorldContextEntry
                {
                    Name = "Turn Rules",
                    IsEnabled = true,
                    RulesText = "One simulation tick is 6 seconds. A character should normally choose one concrete action from availableActions. Waiting is valid only when other actions do not help the character's current goal.",
                    Modules =
                    [
                        new RpContextModule
                        {
                            Name = "Turn Selection Rules",
                            IsEnabled = true,
                            Type = RpContextModuleType.SceneRules,
                            Visibility = RpContextVisibility.WorldOnly,
                            Priority = 100,
                            Text = "One simulation tick is 6 seconds. A character should normally choose one concrete action from availableActions. Waiting is valid only when other actions do not help the character's current goal."
                        }
                    ]
                }
            ],
            NotablePlaces = ["central cavern floor", "crystal outcrop", "collapsed east passage", "shadowed alcove"],
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 22, Minute = 10, SecondsPerTick = 6 }
        };

        world.Factions["player"] = new Faction
        {
            Id = "player",
            Name = "Player",
            Description = "The active player side for the current turn."
        };
        world.Factions["cavern_changelings"] = new Faction
        {
            Id = "cavern_changelings",
            Name = "Cavern Changelings",
            Description = "A small hostile presence adapted to the basement cavern."
        };

        for (int x = -6; x <= 6; x++)
        {
            for (int z = -5; z <= 5; z++)
            {
                var edge = Math.Abs(x) == 6 || Math.Abs(z) == 5;
                var tile = CreatePassableTile(new Vec3Int(x, 0, z), edge ? MaterialType.Rock : CavernFloorMaterial(x, z));
                if (edge || (x == 5 && z >= 2) || (x == -5 && z <= -2))
                {
                    tile.Solidity = TileSolidity.Solid;
                    tile.BulkMaterial = MaterialType.Rock;
                    tile.BulkState = MaterialState.Solid;
                    tile.BulkHealth = 100;
                }

                world.Tiles[tile.Position] = tile;
            }
        }

        AddInteriorRock(world, new Vec3Int(2, 0, -2));
        AddInteriorRock(world, new Vec3Int(3, 0, -2));
        AddInteriorRock(world, new Vec3Int(-3, 0, 2));

        var player = new Character
        {
            Name = "Player Unicorn",
            Race = "Pony Unicorn",
            Position = new Vec3Int(0, 0, 0),
            PersonalityTraits = ["alert", "resourceful"],
            Ideals = ["survive the cavern"],
            Desires = ["find a way out"],
            Abilities = ["spellcasting"],
            RpTags = ["creature", "sapient", "magical", "mana-user", "horn-channel", "equine", "player"],
            RpAbilities = [RpAbilityService.CreateFireballAbility()],
            Mood = "ready",
            FactionId = "player",
            CurrentGoal = new Goal { Description = "Take the next turn carefully." },
            LifeGoal = new Goal { Description = "Escape the basement cavern." },
            BodyType = BodyTypeKind.Equine,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Equine),
            Vitals = new RpVitals
            {
                HealthMax = 100,
                HealthCurrent = 100,
                ManaMax = 60,
                ManaCurrent = 60,
                FocusMax = 40,
                FocusCurrent = 40,
                StaminaMax = 100,
                StaminaCurrent = 100,
                ManaRegenPerTick = 3,
                FocusRegenPerTick = 2,
                StaminaRegenPerTick = 4
            },
            ActionSpeeds = new RpActionSpeeds { MoveSpeed = 1, AttackSpeed = 1, CastSpeed = 1 },
            StaminaMax = 100,
            StaminaCurrent = 100
        };
        AddCharacter(world, player);

        var changeling = new Character
        {
            Name = "Cavern Changeling",
            Race = "Changeling",
            Position = new Vec3Int(3, 0, 1),
            PersonalityTraits = ["watchful", "hungry"],
            Ideals = ["control the cavern"],
            Desires = ["drive intruders away"],
            Abilities = ["ambush", "flight"],
            RpTags = ["creature", "sapient", "magical", "mana-user", "horn-channel", "winged", "changeling"],
            Mood = "hostile",
            FactionId = "cavern_changelings",
            CurrentGoal = new Goal { Description = "Watch the intruder from the dark." },
            LifeGoal = new Goal { Description = "Protect the cavern nest." },
            BodyType = BodyTypeKind.Changeling,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Changeling),
            Vitals = new RpVitals
            {
                HealthMax = 80,
                HealthCurrent = 80,
                ManaMax = 20,
                ManaCurrent = 10,
                FocusMax = 45,
                FocusCurrent = 45,
                StaminaMax = 90,
                StaminaCurrent = 90,
                ManaRegenPerTick = 1,
                FocusRegenPerTick = 3,
                StaminaRegenPerTick = 5
            },
            ActionSpeeds = new RpActionSpeeds { MoveSpeed = 1.15f, AttackSpeed = 1.2f, CastSpeed = 0.8f },
            StaminaMax = 90,
            StaminaCurrent = 90
        };
        AddCharacter(world, changeling);

        AddItem(world, new Item
        {
            Name = "crystal shard",
            Description = "A faintly glowing shard from the cavern wall.",
            Material = MaterialType.Crystal,
            Weight = 0.3f,
            Position = new Vec3Int(-2, 0, 1),
            Tags = ["component", "light", "crystal"],
            GoalAffordances =
            [
                new RpGoalObjectAffordance
                {
                    Kind = RpGoalObjectKind.Resource,
                    Name = "mana focus component",
                    GoalKeywords = ["magic", "spell", "mana", "focus", "component"],
                    DesireKeywords = ["understand", "gather", "collect", "crystal"],
                    NeedTypes = [RpNeedType.Mana, RpNeedType.Focus],
                    Priority = 70,
                    ResultText = "the crystal can be studied or collected as a magical component"
                }
            ]
        });
        AddItem(world, new Item
        {
            Name = "rusted lantern",
            Description = "A dented lantern with a dry wick.",
            Material = MaterialType.Metal,
            Weight = 1.2f,
            Position = new Vec3Int(1, 0, -1),
            Tags = ["tool", "light"],
            GoalAffordances =
            [
                new RpGoalObjectAffordance
                {
                    Kind = RpGoalObjectKind.Tool,
                    Name = "light source",
                    GoalKeywords = ["escape", "explore", "search", "dark", "light"],
                    DesireKeywords = ["find a way out", "map", "inspect"],
                    Priority = 60,
                    ResultText = "the lantern helps with exploration planning"
                }
            ]
        });

        RpSimulationService.UpdatePerception(world);
        AddEvent(world, "System", "The basement cavern is ready. It is the player's turn.");
        return world;
    }

    public static World CreateGlasshouseOutpostWorld()
    {
        var world = new World
        {
            Name = "Glasshouse Outpost",
            Lore = "A small neutral outpost built around a damaged glasshouse. The world state is intentionally generic so it can be replaced by imported settings and character modules later.",
            WorldContexts =
            [
                new RpWorldContextEntry
                {
                    Name = "Outpost Rules",
                    IsEnabled = true,
                    RulesText = "The outpost is a practical survival setting. Characters should respect visible structures, blocked glass walls, inventory limits, and faction goals when deciding actions.",
                    Modules =
                    [
                        new RpContextModule
                        {
                            Name = "Outpost Physical Rules",
                            IsEnabled = true,
                            Type = RpContextModuleType.EnvironmentRules,
                            Visibility = RpContextVisibility.WorldOnly,
                            Priority = 100,
                            Text = "The outpost is a practical survival setting. Characters should respect visible structures, blocked glass walls, inventory limits, and faction goals when deciding actions."
                        }
                    ]
                }
            ],
            NotablePlaces = ["central glasshouse", "west storage alcove", "north moss court"],
            Clock = new WorldClock { Year = 42, Month = 3, Day = 14, Hour = 9, Minute = 30, SecondsPerTick = 6 }
        };

        world.Factions["outpost"] = new Faction
        {
            Id = "outpost",
            Name = "Outpost Residents",
            Description = "Practical people trying to keep the glasshouse running."
        };

        for (int x = -6; x <= 6; x++)
        {
            for (int z = -6; z <= 6; z++)
            {
                var material = Math.Abs(x) == 6 || Math.Abs(z) == 6
                    ? MaterialType.Rock
                    : ((x + z) % 5 == 0 ? MaterialType.Moss : MaterialType.Grass);

                var tile = CreatePassableTile(new Vec3Int(x, 0, z), material);
                if (Math.Abs(x) == 6 || Math.Abs(z) == 6)
                {
                    tile.Solidity = TileSolidity.Solid;
                    tile.BulkMaterial = MaterialType.Rock;
                    tile.BulkState = MaterialState.Solid;
                    tile.BulkHealth = 80;
                }

                world.Tiles[tile.Position] = tile;
            }
        }

        for (int x = -2; x <= 2; x++)
        {
            world.Tiles[new Vec3Int(x, 0, -2)].Sides[(int)Direction.North] = WallSide(Direction.North, MaterialType.Glass);
            world.Tiles[new Vec3Int(x, 0, 2)].Sides[(int)Direction.South] = WallSide(Direction.South, MaterialType.Glass);
        }

        for (int z = -2; z <= 2; z++)
        {
            world.Tiles[new Vec3Int(-2, 0, z)].Sides[(int)Direction.East] = WallSide(Direction.East, MaterialType.Glass);
            world.Tiles[new Vec3Int(2, 0, z)].Sides[(int)Direction.West] = WallSide(Direction.West, MaterialType.Glass);
        }

        AddCharacter(world, new Character
        {
            Name = "Mira",
            Race = "Human",
            Position = new Vec3Int(0, 0, 0),
            PersonalityTraits = ["observant", "careful"],
            Ideals = ["keep the outpost stable"],
            Desires = ["understand the strange heat in the glasshouse"],
            Abilities = ["repair", "botany"],
            Mood = "focused",
            FactionId = "outpost",
            CurrentGoal = new Goal { Description = "Inspect the glasshouse and report anything unusual." },
            LifeGoal = new Goal { Description = "Build a safe settlement." },
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human)
        });

        AddCharacter(world, new Character
        {
            Name = "Sable",
            Race = "Construct",
            Position = new Vec3Int(3, 0, 1),
            PersonalityTraits = ["literal", "patient"],
            Ideals = ["protect useful systems"],
            Desires = ["map the outpost accurately"],
            Abilities = ["navigation", "heavy lifting"],
            Mood = "neutral",
            FactionId = "outpost",
            CurrentGoal = new Goal { Description = "Create a reliable map of nearby rooms." },
            LifeGoal = new Goal { Description = "Maintain the outpost infrastructure." },
            BodyType = BodyTypeKind.Construct,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Construct)
        });

        var canteen = new Item
        {
            Name = "water canteen",
            Description = "A half-full metal canteen.",
            Material = MaterialType.Metal,
            Weight = 1,
            Position = new Vec3Int(-1, 0, 1),
            Tags = ["tool", "water"]
        };
        world.Items[canteen.Id] = canteen;
        world.Tiles[canteen.Position.Value].OccupantIds.Add(canteen.Id);

        RpSimulationService.UpdatePerception(world);
        AddEvent(world, "System", "The starter world was created.");
        return world;
    }

    public static World CreatePathfindingStressWorld(int unitCount)
    {
        unitCount = Math.Clamp(unitCount, 10, 100);
        var world = new World
        {
            Name = $"Pathfinding Stress {unitCount}",
            Lore = "A deterministic pathfinding stress map. Test runners move from west to east when LLM is disabled.",
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 10, SecondsPerTick = 6 },
            WorldContexts =
            [
                new RpWorldContextEntry
                {
                    Name = "Pathfinding Stress Rules",
                    IsEnabled = true,
                    RulesText = "Developer test map. Characters tagged path-test-runner should move toward their path-test-target tag each local tick."
                }
            ]
        };

        world.Factions["observer"] = new Faction { Id = "observer", Name = "Observer", Description = "Non-moving test observer." };
        world.Factions["path_test"] = new Faction { Id = "path_test", Name = "Path Test Units", Description = "Units used for local pathfinding stress tests." };

        const int minX = -30;
        const int maxX = 30;
        const int minZ = -10;
        const int maxZ = 10;
        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                var tile = CreatePassableTile(new Vec3Int(x, 0, z), (x + z) % 6 == 0 ? MaterialType.Moss : MaterialType.Rock);
                if (x is minX or maxX || z is minZ or maxZ)
                {
                    MakeSolid(tile);
                }

                world.Tiles[tile.Position] = tile;
            }
        }

        for (var z = minZ + 1; z <= maxZ - 1; z++)
        {
            if (z is -8 or -2 or 3 or 8)
            {
                continue;
            }

            MakeSolid(world.Tiles[new Vec3Int(-7, 0, z)]);
            MakeSolid(world.Tiles[new Vec3Int(8, 0, z)]);
        }

        for (var x = -20; x <= 20; x++)
        {
            if (x is -18 or -9 or 0 or 9 or 18)
            {
                continue;
            }

            MakeSolid(world.Tiles[new Vec3Int(x, 0, -4)]);
            MakeSolid(world.Tiles[new Vec3Int(x, 0, 5)]);
        }

        AddCharacter(world, CreateObserver(new Vec3Int(0, 0, 0)));

        var lanes = Enumerable.Range(minZ + 2, maxZ - minZ - 3).Where(z => z != -4 && z != 5).ToArray();
        for (var i = 0; i < unitCount; i++)
        {
            var startZ = lanes[i % lanes.Length];
            var offset = i / lanes.Length;
            var start = new Vec3Int(minX + 2 + Math.Min(offset, 3), 0, startZ);
            var target = new Vec3Int(maxX - 2, 0, -startZ);
            if (!world.Tiles.TryGetValue(start, out var startTile) || startTile.Solidity == TileSolidity.Solid)
            {
                start = new Vec3Int(minX + 2, 0, Math.Clamp(startZ + 1, minZ + 1, maxZ - 1));
            }

            var unit = CreatePathTestCharacter($"Runner {i + 1:000}", start, target, BodyTypeKind.Humanoid, [RpMovementMode.Walk]);
            AddCharacter(world, unit);
        }

        RpSimulationService.UpdatePerception(world);
        AddEvent(world, "System", $"Pathfinding stress map loaded with {unitCount} runner(s). Press Tick or Run 5 with LLM off.");
        return world;
    }

    public static World CreateVerticalPathfindingTestWorld()
    {
        var world = new World
        {
            Name = "Pathfinding Vertical Slices",
            Lore = "A three-level pathfinding map with ramp routes for walkers and open vertical space for flyers.",
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 11, SecondsPerTick = 6 },
            WorldContexts =
            [
                new RpWorldContextEntry
                {
                    Name = "Vertical Pathfinding Rules",
                    IsEnabled = true,
                    RulesText = "Developer test map. Walkers use ramps between levels. Winged units may fly through open vertical cells."
                }
            ]
        };

        world.Factions["observer"] = new Faction { Id = "observer", Name = "Observer", Description = "Non-moving test observer." };
        world.Factions["path_test"] = new Faction { Id = "path_test", Name = "Path Test Units", Description = "Units used for vertical movement tests." };

        for (var y = 0; y <= 2; y++)
        {
            for (var x = -12; x <= 12; x++)
            {
                for (var z = -6; z <= 6; z++)
                {
                    var tile = CreatePassableTile(new Vec3Int(x, y, z), (x + z + y) % 5 == 0 ? MaterialType.Moss : MaterialType.Rock);
                    if (Math.Abs(x) == 12 || Math.Abs(z) == 6)
                    {
                        MakeSolid(tile);
                    }

                    world.Tiles[tile.Position] = tile;
                }
            }
        }

        for (var y = 0; y <= 2; y++)
        {
            for (var z = -5; z <= 5; z++)
            {
                if (z is -4 or 0 or 4)
                {
                    continue;
                }

                MakeSolid(world.Tiles[new Vec3Int(-2, y, z)]);
                MakeSolid(world.Tiles[new Vec3Int(5, y, z)]);
            }
        }

        AddRampPair(world, new Vec3Int(-8, 0, -4), new Vec3Int(-8, 1, -4));
        AddRampPair(world, new Vec3Int(8, 1, 4), new Vec3Int(8, 2, 4));

        AddCharacter(world, CreateObserver(new Vec3Int(0, 0, 0)));
        AddCharacter(world, CreatePathTestCharacter("Ramp Walker A", new Vec3Int(-10, 0, -4), new Vec3Int(10, 2, 4), BodyTypeKind.Humanoid, [RpMovementMode.Walk]));
        AddCharacter(world, CreatePathTestCharacter("Ramp Walker B", new Vec3Int(-10, 0, 0), new Vec3Int(10, 2, 0), BodyTypeKind.Equine, [RpMovementMode.Walk]));

        var flyerA = CreatePathTestCharacter("Winged Flyer A", new Vec3Int(-10, 0, 5), new Vec3Int(10, 2, -5), BodyTypeKind.Avian, [RpMovementMode.Fly]);
        flyerA.RpTags.Add("winged");
        AddCharacter(world, flyerA);

        var flyerB = CreatePathTestCharacter("Winged Flyer B", new Vec3Int(10, 2, -5), new Vec3Int(-10, 0, 5), BodyTypeKind.Changeling, [RpMovementMode.Fly]);
        flyerB.RpTags.Add("winged");
        AddCharacter(world, flyerB);

        RpSimulationService.UpdatePerception(world);
        AddEvent(world, "System", "Vertical pathfinding map loaded. Switch slices between Y 0, Y 1, and Y 2 while ticking.");
        return world;
    }

    public static World CreateGlassAtriumFlightTestWorld()
    {
        var world = new World
        {
            Name = "Glass Atrium Flight Test",
            Lore = "A glass-walled ten-level atrium with real open air in the center. Flyers must climb through empty space to reach the upper target.",
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 12, SecondsPerTick = 6 },
            WorldContexts =
            [
                new RpWorldContextEntry
                {
                    Name = "Glass Atrium Rules",
                    IsEnabled = true,
                    RulesText = "Developer test map. Center atrium cells are open space, not floors. Winged path-test units should fly upward through the shaft to their upper target."
                }
            ]
        };

        world.Factions["observer"] = new Faction { Id = "observer", Name = "Observer", Description = "Non-moving test observer." };
        world.Factions["path_test"] = new Faction { Id = "path_test", Name = "Path Test Units", Description = "Units used for flight path tests." };

        const int topLevel = 9;
        for (var y = 0; y <= topLevel; y++)
        {
            for (var x = -8; x <= 8; x++)
            {
                for (var z = -5; z <= 5; z++)
                {
                    var isEdge = Math.Abs(x) == 8 || Math.Abs(z) == 5;
                    var isAtriumVoid = Math.Abs(x) <= 3 && Math.Abs(z) <= 2 && y > 0 && y < topLevel;
                    var tile = isAtriumVoid
                        ? CreateAirTile(new Vec3Int(x, y, z))
                        : CreatePassableTile(new Vec3Int(x, y, z), MaterialType.Glass);

                    if (isEdge)
                    {
                        MakeSolid(tile);
                        tile.BulkMaterial = MaterialType.Glass;
                    }

                    world.Tiles[tile.Position] = tile;
                }
            }
        }

        AddCharacter(world, CreateObserver(new Vec3Int(-6, 0, -4)));

        var flyer = CreatePathTestCharacter("Atrium Flyer", new Vec3Int(0, 0, 0), new Vec3Int(0, topLevel, 0), BodyTypeKind.Avian, [RpMovementMode.Fly]);
        flyer.RpTags.Add("winged");
        AddCharacter(world, flyer);

        var walker = CreatePathTestCharacter("Ground Walker", new Vec3Int(-6, 0, 2), new Vec3Int(6, topLevel, 2), BodyTypeKind.Humanoid, [RpMovementMode.Walk]);
        AddCharacter(world, walker);

        AddItem(world, new Item
        {
            Name = "upper target beacon",
            Description = "A marker on the upper glass balcony.",
            Material = MaterialType.Crystal,
            Weight = 0.1f,
            Position = new Vec3Int(0, topLevel, 0),
            Tags = ["pathfinding-target", "beacon"]
        });

        RpSimulationService.UpdatePerception(world);
        AddEvent(world, "System", "Glass atrium flight test loaded. Watch Y 1 through Y 9: the flyer should climb through open space toward the top beacon.");
        return world;
    }

    private static MaterialType CavernFloorMaterial(int x, int z)
        => (x + (z * 2)) % 7 == 0 ? MaterialType.Crystal :
            (x - z) % 4 == 0 ? MaterialType.Moss :
            MaterialType.Rock;

    private static void AddInteriorRock(World world, Vec3Int position)
    {
        if (!world.Tiles.TryGetValue(position, out var tile))
        {
            return;
        }

        tile.Solidity = TileSolidity.Solid;
        tile.BulkMaterial = MaterialType.Rock;
        tile.BulkState = MaterialState.Solid;
        tile.BulkHealth = 90;
    }

    private static void MakeSolid(Tile tile)
    {
        tile.Solidity = TileSolidity.Solid;
        tile.BulkMaterial = MaterialType.Rock;
        tile.BulkState = MaterialState.Solid;
        tile.BulkHealth = 100;
    }

    private static void AddRampPair(World world, Vec3Int lower, Vec3Int upper)
    {
        world.Tiles[lower].MovementFeatures.Add(RpTileMovementFeature.RampUp);
        world.Tiles[upper].MovementFeatures.Add(RpTileMovementFeature.RampDown);
    }

    private static Character CreateObserver(Vec3Int position)
        => new()
        {
            Name = "Path Observer",
            Race = "Human",
            Position = position,
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human),
            FactionId = "observer",
            RpTags = ["creature", "sapient", "player", "observer"],
            CurrentGoal = new Goal { Description = "Watch the pathfinding test." },
            LifeGoal = new Goal { Description = "Observe simulation behavior." },
            Movement = new RpMovementProfile { Modes = [RpMovementMode.Walk] }
        };

    private static Character CreatePathTestCharacter(string name, Vec3Int position, Vec3Int target, BodyTypeKind bodyType, List<RpMovementMode> modes)
        => new()
        {
            Name = name,
            Race = bodyType.ToString(),
            Position = position,
            BodyType = bodyType,
            Body = RpBodyFactory.CreateBody(bodyType),
            FactionId = "path_test",
            RpTags = ["creature", "sapient", "path-test-runner", FormatPathTargetTag(target)],
            CurrentGoal = new Goal { Description = $"Move to {target}." },
            LifeGoal = new Goal { Description = "Reach the opposite side of the pathfinding test map." },
            Movement = new RpMovementProfile { Modes = modes, MaxPathSearchNodes = 12000 },
            ActionSpeeds = new RpActionSpeeds { MoveSpeed = 1, AttackSpeed = 1, CastSpeed = 1 }
        };

    private static string FormatPathTargetTag(Vec3Int target)
        => $"path-test-target:{target.X},{target.Y},{target.Z}";

    private static Tile CreatePassableTile(Vec3Int position, MaterialType floorMaterial)
    {
        var tile = new Tile
        {
            Position = position,
            Solidity = TileSolidity.Empty,
            BulkMaterial = MaterialType.Air,
            BulkState = MaterialState.Gas,
            Temperature = 293.15f
        };
        tile.Sides[(int)Direction.Floor] = new Side
        {
            Direction = Direction.Floor,
            Material = floorMaterial,
            Health = 40,
            IsPassable = true
        };
        return tile;
    }

    private static Tile CreateAirTile(Vec3Int position)
        => new()
        {
            Position = position,
            Solidity = TileSolidity.Empty,
            BulkMaterial = MaterialType.Air,
            BulkState = MaterialState.Gas,
            Temperature = 293.15f
        };

    private static Side WallSide(Direction direction, MaterialType material)
        => new()
        {
            Direction = direction,
            Material = material,
            Health = 40,
            IsPassable = false,
            Feature = SideFeature.Window
        };

    private static void AddCharacter(World world, Character character)
    {
        world.Characters[character.Id] = character;
        if (world.Tiles.TryGetValue(character.Position, out var tile))
        {
            tile.OccupantIds.Add(character.Id);
        }
    }

    private static void AddItem(World world, Item item)
    {
        world.Items[item.Id] = item;
        if (item.Position.HasValue && world.Tiles.TryGetValue(item.Position.Value, out var tile))
        {
            tile.OccupantIds.Add(item.Id);
        }
    }

    private static void AddEvent(World world, string actor, string description)
    {
        var evt = new NarrativeEvent
        {
            Tick = world.Clock.TickCount,
            ActorName = actor,
            Description = description
        };

        foreach (var character in world.Characters.Values)
        {
            character.PerceivedLog.Add(evt);
        }
    }
}
