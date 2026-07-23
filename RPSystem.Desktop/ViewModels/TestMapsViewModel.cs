using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Handles loading of test/preset maps.
/// </summary>
public sealed partial class TestMapsViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly WorldMapViewModel _map;
    private readonly RpCaveMapGenerator _caveMapGenerator;

    public TestMapsViewModel(
        WorldSimulationViewModel simulation,
        WorldMapViewModel map,
        RpCaveMapGenerator caveMapGenerator)
    {
        _simulation = simulation;
        _map = map;
        _caveMapGenerator = caveMapGenerator;
    }

    [RelayCommand]
    public void LoadCavernStarterTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateCavernStarterWorld(), "Loaded starter cavern test map.");
    }

    [RelayCommand]
    public void LoadGlasshouseTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateGlasshouseOutpostWorld(), "Loaded glasshouse movement test map.");
    }

    [RelayCommand]
    public void LoadGeneratedRampCaveTestMap()
    {
        var result = _caveMapGenerator.Generate(new RpCaveMapGenerationOptions
        {
            Name = "Generated Ramp Cave",
            Width = 31,
            Depth = 31,
            Levels = 3,
            Seed = 1337,
            ConnectorKind = RpVerticalConnectorKind.Ramp
        });
        LoadTestWorld(result.World, $"Loaded generated ramp cave: {result.OpenTileCount} open tile(s), {result.VerticalConnectors.Count} connector(s).");
    }

    [RelayCommand]
    public void LoadGeneratedLadderCaveTestMap()
    {
        var result = _caveMapGenerator.Generate(new RpCaveMapGenerationOptions
        {
            Name = "Generated Ladder Cave",
            Width = 31,
            Depth = 31,
            Levels = 3,
            Seed = 2027,
            ConnectorKind = RpVerticalConnectorKind.Ladder
        });

        var player = result.World.Characters.Values.FirstOrDefault(character => character.RpTags.Contains("player", StringComparer.OrdinalIgnoreCase));
        if (player != null)
        {
            player.Movement.Modes = [RpMovementMode.Climb];
            if (!player.RpTags.Contains("climber", StringComparer.OrdinalIgnoreCase))
            {
                player.RpTags.Add("climber");
            }
        }

        LoadTestWorld(result.World, $"Loaded generated ladder cave: {result.OpenTileCount} open tile(s), {result.VerticalConnectors.Count} connector(s).");
    }

    [RelayCommand]
    public void LoadPathfindingStress10TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(10), "Loaded pathfinding stress map with 10 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadPathfindingStress50TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(50), "Loaded pathfinding stress map with 50 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadPathfindingStress100TestMap()
    {
        LoadTestWorld(RpWorldFactory.CreatePathfindingStressWorld(100), "Loaded pathfinding stress map with 100 runners. Press Tick or Run 5 with LLM off.");
    }

    [RelayCommand]
    public void LoadVerticalPathfindingTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateVerticalPathfindingTestWorld(), "Loaded 3-slice pathfinding map. Watch Y 0, Y 1, and Y 2 while ticking.");
    }

    [RelayCommand]
    public void LoadNegotiationShowcaseTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateNegotiationShowcaseWorld(), "Loaded negotiation showcase — talk to Scout Vexa before things escalate.");
    }

    [RelayCommand]
    public void LoadGlassAtriumFlightTestMap()
    {
        LoadTestWorld(RpWorldFactory.CreateGlassAtriumFlightTestWorld(), "Loaded glass atrium flight map. Center cells on upper slices are open space, not floors.");
    }

    private void LoadTestWorld(World testWorld, string status)
    {
        _simulation.Stop();
        _simulation.World = testWorld;
        _simulation.SliceMode = RpSliceMode.Horizontal;
        _simulation.SliceCoordinate = _simulation.PlayerCharacter?.Position.Y ?? 0;
        _map.MapActionMode = RpMapActionMode.Look;
        _simulation.StatusText = status;
    }
}
