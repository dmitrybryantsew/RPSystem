using System.Text.Json;
using System.Text.Json.Serialization;
using ChemCalculationAndManagementApp.RpSystem;
using Microsoft.Maui.Storage;

namespace ChemCalculationAndManagementApp.Services;

public sealed class RpWorldSaveService
{
    private static readonly JsonSerializerOptions SaveJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string DefaultSavePath => Path.Combine(FileSystem.AppDataDirectory, "rp-world-save.json");

    public async Task SaveAsync(World world, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultSavePath)!);
        var json = JsonSerializer.Serialize(WorldSaveDocument.FromWorld(world), SaveJsonOptions);
        await File.WriteAllTextAsync(DefaultSavePath, json, cancellationToken);
    }

    public async Task<World?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DefaultSavePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(DefaultSavePath, cancellationToken);
        var doc = JsonSerializer.Deserialize<WorldSaveDocument>(json, SaveJsonOptions);
        return doc?.ToWorld();
    }

    private sealed class WorldSaveDocument
    {
        public int Version { get; set; } = 1;
        public World World { get; set; } = new();
        public List<Tile> Tiles { get; set; } = [];

        public static WorldSaveDocument FromWorld(World world)
            => new()
            {
                Version = 1,
                World = world,
                Tiles = world.Tiles.Values.ToList()
            };

        public World ToWorld()
        {
            World.Tiles = Tiles.ToDictionary(tile => tile.Position);
            foreach (var character in World.Characters.Values)
            {
                RpBodyFactory.EnsureBody(character);
                RpCreatureService.EnsureCreatureStats(character);
            }

            return World;
        }
    }
}
