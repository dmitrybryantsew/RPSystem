using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpCaveMapGenerator
{
    public RpCaveMapGenerationResult Generate(RpCaveMapGenerationOptions? options = null)
    {
        options ??= new RpCaveMapGenerationOptions();
        options = Normalize(options);

        var random = new Random(options.Seed);
        var open = CreateInitialMap(options, random);
        for (var i = 0; i < options.CellularAutomataIterations; i++)
        {
            open = StepCellularAutomata(open, options);
        }

        var start = new Vec3Int(0, 0, 0);
        var exit = new Vec3Int(0, options.Levels - 1, 0);
        CarveGuaranteedBackbone(open, options);

        var world = BuildWorld(open, options);
        var connectors = AddVerticalConnectors(world, options);
        AddTestActors(world, start);

        return new RpCaveMapGenerationResult
        {
            World = world,
            StartPosition = start,
            ExitPosition = exit,
            VerticalConnectors = connectors,
            OpenTileCount = world.Tiles.Values.Count(tile => tile.Solidity != TileSolidity.Solid),
            SolidTileCount = world.Tiles.Values.Count(tile => tile.Solidity == TileSolidity.Solid)
        };
    }

    private static RpCaveMapGenerationOptions Normalize(RpCaveMapGenerationOptions options)
        => new()
        {
            Width = Math.Max(5, MakeOdd(options.Width)),
            Depth = Math.Max(5, MakeOdd(options.Depth)),
            Levels = Math.Max(1, options.Levels),
            Seed = options.Seed,
            WallFillPercent = Math.Clamp(options.WallFillPercent, 25, 75),
            CellularAutomataIterations = Math.Clamp(options.CellularAutomataIterations, 0, 12),
            ConnectorKind = options.ConnectorKind,
            Name = string.IsNullOrWhiteSpace(options.Name) ? "Generated Cave System" : options.Name
        };

    private static int MakeOdd(int value) => value % 2 == 0 ? value + 1 : value;

    private static bool[,,] CreateInitialMap(RpCaveMapGenerationOptions options, Random random)
    {
        var open = new bool[options.Levels, options.Width, options.Depth];
        for (var y = 0; y < options.Levels; y++)
        {
            for (var x = 0; x < options.Width; x++)
            {
                for (var z = 0; z < options.Depth; z++)
                {
                    var border = x == 0 || z == 0 || x == options.Width - 1 || z == options.Depth - 1;
                    open[y, x, z] = !border && random.Next(100) >= options.WallFillPercent;
                }
            }
        }

        return open;
    }

    private static bool[,,] StepCellularAutomata(bool[,,] current, RpCaveMapGenerationOptions options)
    {
        var next = new bool[options.Levels, options.Width, options.Depth];
        for (var y = 0; y < options.Levels; y++)
        {
            for (var x = 0; x < options.Width; x++)
            {
                for (var z = 0; z < options.Depth; z++)
                {
                    if (x == 0 || z == 0 || x == options.Width - 1 || z == options.Depth - 1)
                    {
                        next[y, x, z] = false;
                        continue;
                    }

                    var wallNeighbors = CountWallNeighbors(current, options, y, x, z);
                    next[y, x, z] = wallNeighbors < 5;
                }
            }
        }

        return next;
    }

    private static int CountWallNeighbors(bool[,,] open, RpCaveMapGenerationOptions options, int y, int x, int z)
    {
        var walls = 0;
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var nx = x + dx;
                var nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= options.Width || nz >= options.Depth || !open[y, nx, nz])
                {
                    walls++;
                }
            }
        }

        return walls;
    }

    private static void CarveGuaranteedBackbone(bool[,,] open, RpCaveMapGenerationOptions options)
    {
        var centerX = options.Width / 2;
        var centerZ = options.Depth / 2;
        for (var y = 0; y < options.Levels; y++)
        {
            for (var x = 1; x < options.Width - 1; x++)
            {
                open[y, x, centerZ] = true;
            }

            for (var z = 1; z < options.Depth - 1; z++)
            {
                open[y, centerX, z] = true;
            }
        }
    }

    private static World BuildWorld(bool[,,] open, RpCaveMapGenerationOptions options)
    {
        var world = new World
        {
            Name = options.Name,
            Lore = "A generated multi-level cave system intended for RP pathfinding and unit behavior tests.",
            Clock = new WorldClock { Year = 1, Month = 1, Day = 1, Hour = 8, Minute = 0, SecondsPerTick = 6 }
        };

        var halfWidth = options.Width / 2;
        var halfDepth = options.Depth / 2;
        for (var y = 0; y < options.Levels; y++)
        {
            for (var x = 0; x < options.Width; x++)
            {
                for (var z = 0; z < options.Depth; z++)
                {
                    var position = new Vec3Int(x - halfWidth, y, z - halfDepth);
                    var isOpen = open[y, x, z];
                    world.Tiles[position] = new Tile
                    {
                        Position = position,
                        Solidity = isOpen ? TileSolidity.Empty : TileSolidity.Solid,
                        BulkMaterial = isOpen ? MaterialType.Air : MaterialType.Rock,
                        BulkState = isOpen ? MaterialState.Gas : MaterialState.Solid,
                        BulkHealth = isOpen ? 0 : 100,
                        Temperature = 285.15f
                    };
                }
            }
        }

        return world;
    }

    private static List<RpVerticalConnector> AddVerticalConnectors(World world, RpCaveMapGenerationOptions options)
    {
        var connectors = new List<RpVerticalConnector>();
        var connectorPosition = new Vec3Int(0, 0, 0);
        for (var y = 0; y < options.Levels - 1; y++)
        {
            var lower = connectorPosition with { Y = y };
            var upper = connectorPosition with { Y = y + 1 };
            if (!world.Tiles.TryGetValue(lower, out var lowerTile) ||
                !world.Tiles.TryGetValue(upper, out var upperTile))
            {
                continue;
            }

            lowerTile.Solidity = TileSolidity.Empty;
            lowerTile.BulkMaterial = MaterialType.Air;
            lowerTile.BulkState = MaterialState.Gas;
            upperTile.Solidity = TileSolidity.Empty;
            upperTile.BulkMaterial = MaterialType.Air;
            upperTile.BulkState = MaterialState.Gas;

            if (options.ConnectorKind == RpVerticalConnectorKind.Ladder)
            {
                AddFeature(lowerTile, RpTileMovementFeature.LadderUp);
                AddFeature(upperTile, RpTileMovementFeature.LadderDown);
            }
            else
            {
                AddFeature(lowerTile, RpTileMovementFeature.RampUp);
                AddFeature(upperTile, RpTileMovementFeature.RampDown);
            }

            connectors.Add(new RpVerticalConnector(lower, upper, options.ConnectorKind));
        }

        return connectors;
    }

    private static void AddFeature(Tile tile, RpTileMovementFeature feature)
    {
        if (!tile.MovementFeatures.Contains(feature))
        {
            tile.MovementFeatures.Add(feature);
        }
    }

    private static void AddTestActors(World world, Vec3Int start)
    {
        var player = new Character
        {
            Name = "Cave Test Player",
            Race = "Human",
            Position = start,
            BodyType = BodyTypeKind.Human,
            Body = RpBodyFactory.CreateBody(BodyTypeKind.Human),
            RpTags = ["creature", "sapient", "player"]
        };

        world.Characters[player.Id] = player;
        if (world.Tiles.TryGetValue(start, out var tile))
        {
            tile.OccupantIds.Add(player.Id);
        }
    }
}

public sealed class RpCaveMapGenerationOptions
{
    public string Name { get; set; } = "Generated Cave System";
    public int Width { get; set; } = 31;
    public int Depth { get; set; } = 31;
    public int Levels { get; set; } = 3;
    public int Seed { get; set; } = 1337;
    public int WallFillPercent { get; set; } = 45;
    public int CellularAutomataIterations { get; set; } = 4;
    public RpVerticalConnectorKind ConnectorKind { get; set; } = RpVerticalConnectorKind.Ramp;
}

public sealed class RpCaveMapGenerationResult
{
    public World World { get; set; } = new();
    public Vec3Int StartPosition { get; set; }
    public Vec3Int ExitPosition { get; set; }
    public List<RpVerticalConnector> VerticalConnectors { get; set; } = [];
    public int OpenTileCount { get; set; }
    public int SolidTileCount { get; set; }
}

public enum RpVerticalConnectorKind { Ramp, Ladder }

public sealed record RpVerticalConnector(Vec3Int LowerPosition, Vec3Int UpperPosition, RpVerticalConnectorKind Kind);
