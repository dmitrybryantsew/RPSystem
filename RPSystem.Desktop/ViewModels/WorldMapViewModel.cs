using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Manages map rendering state: slice mode, selection, action modes, tile inspection.
/// </summary>
public sealed partial class WorldMapViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly PlayerControlViewModel _player;
    private readonly RpWorldInspectionService _inspectionService;
    private readonly RpMapRenderProjectionService _mapRenderProjectionService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private RpMapActionMode _mapActionMode = RpMapActionMode.Look;
    [ObservableProperty] private int _openSpaceLookDepth = 5;

    public string MapModeText => MapActionMode.ToString();
    public string SliceModeText => _simulation.SliceMode == RpSliceMode.Horizontal ? "Horizontal X/Z" : "Vertical X/Y";
    public string SliceCoordinateText => _simulation.SliceMode == RpSliceMode.Horizontal ? $"Y {_simulation.SliceCoordinate}" : $"Z {_simulation.SliceCoordinate}";

    public WorldMapViewModel(
        WorldSimulationViewModel simulation,
        PlayerControlViewModel player,
        RpWorldInspectionService inspectionService,
        RpMapRenderProjectionService mapRenderProjectionService,
        ISettingsService settingsService)
    {
        _simulation = simulation;
        _player = player;
        _inspectionService = inspectionService;
        _mapRenderProjectionService = mapRenderProjectionService;
        _settingsService = settingsService;

        _openSpaceLookDepth = settingsService.GetInt("RpOpenSpaceLookDepth", 5);

        _simulation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorldSimulationViewModel.SliceMode))
            {
                OnPropertyChanged(nameof(SliceModeText));
                OnPropertyChanged(nameof(SliceCoordinateText));
            }
            if (e.PropertyName == nameof(WorldSimulationViewModel.SliceCoordinate))
            {
                OnPropertyChanged(nameof(SliceCoordinateText));
            }
        };
    }

    partial void OnMapActionModeChanged(RpMapActionMode value)
    {
        OnPropertyChanged(nameof(MapModeText));
    }

    partial void OnOpenSpaceLookDepthChanged(int value)
    {
        var clamped = Math.Clamp(value, 0, 20);
        if (clamped != value)
        {
            OpenSpaceLookDepth = clamped;
            return;
        }
        _settingsService.SetInt("RpOpenSpaceLookDepth", clamped);
    }

    [RelayCommand]
    public void SetLookMode()
    {
        MapActionMode = RpMapActionMode.Look;
        _simulation.StatusText = "Look mode: hover tiles, click to inspect.";
    }

    [RelayCommand]
    public void SetMoveMode()
    {
        MapActionMode = RpMapActionMode.Move;
        _simulation.StatusText = "Move mode: click an adjacent tile to move.";
    }

    [RelayCommand]
    public void SetTalkMode()
    {
        MapActionMode = RpMapActionMode.Talk;
        _simulation.StatusText = "Talk mode: click a character tile.";
    }

    [RelayCommand]
    public void SetInteractMode()
    {
        MapActionMode = RpMapActionMode.Interact;
        _simulation.StatusText = "Interact mode: click a tile or object.";
    }

    [RelayCommand]
    public void SetUseMode()
    {
        MapActionMode = RpMapActionMode.Use;
        _simulation.StatusText = "Use mode: click a target tile or object.";
    }

    [RelayCommand]
    public void SetAttackMode()
    {
        MapActionMode = RpMapActionMode.Attack;
        _simulation.StatusText = "Attack mode: click a target tile.";
    }

    [RelayCommand]
    public void ToggleSliceMode()
    {
        _simulation.SliceMode = _simulation.SliceMode == RpSliceMode.Horizontal ? RpSliceMode.Vertical : RpSliceMode.Horizontal;
        _simulation.SliceCoordinate = 0;
    }

    [RelayCommand]
    public void SliceUp()
    {
        _simulation.SliceCoordinate++;
    }

    [RelayCommand]
    public void SliceDown()
    {
        _simulation.SliceCoordinate--;
    }

    [RelayCommand]
    public void LookAtPlayerTile()
    {
        if (_simulation.PlayerCharacter == null)
        {
            _player.SelectedTileInfo = "Choose a player character first.";
            return;
        }
        _player.LookAtTile(_simulation.PlayerCharacter.Position);
    }

    [RelayCommand]
    public void MoveToInspectedTile()
    {
        if (_player.SelectedTilePosition == null)
        {
            _simulation.StatusText = "Inspect a tile first.";
            return;
        }

        if (_simulation.PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        var delta = _player.SelectedTilePosition.Value - _simulation.PlayerCharacter.Position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) + Math.Abs(delta.Z) != 1)
        {
            _simulation.StatusText = "Move Here only supports adjacent tiles for now.";
            return;
        }

        _ = _player.TryMovePlayerTo(_player.SelectedTilePosition.Value, "to inspected tile");
    }

    public void HandleMapTileClick(Vec3Int position)
    {
        _player.LookAtTile(position);
        if (MapActionMode == RpMapActionMode.Look) return;

        if (_simulation.PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (MapActionMode == RpMapActionMode.Talk)
        {
            _player.TalkToInspectedCharacter();
            return;
        }

        if (MapActionMode == RpMapActionMode.Interact)
        {
            _player.InteractWithInspectedTile();
            return;
        }

        if (MapActionMode == RpMapActionMode.Use)
        {
            _player.UseHeldItemOnInspectedTile();
            return;
        }

        if (MapActionMode == RpMapActionMode.Attack)
        {
            _player.AttackInspectedTarget();
            return;
        }

        if (position == _simulation.PlayerCharacter.Position) return;

        var delta = position - _simulation.PlayerCharacter.Position;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) + Math.Abs(delta.Z) != 1)
        {
            _simulation.StatusText = "Move mode only supports adjacent tiles for now.";
            return;
        }

        _ = _player.TryMovePlayerTo(position, "clicked");
    }

    public string GetTileGlyph(Vec3Int position)
        => _inspectionService.GetTileGlyph(_simulation.World, position);

    public bool TryGetVisibleBounds(out int minA, out int maxA, out int minB, out int maxB)
        => _inspectionService.TryGetVisibleBounds(_simulation.World, _simulation.SliceMode, _simulation.SliceCoordinate, out minA, out maxA, out minB, out maxB);

    public RpMapRenderSnapshot GetMapRenderSnapshot()
        => _mapRenderProjectionService.CreateSnapshot(
            _simulation.World,
            _simulation.SliceMode,
            _simulation.SliceCoordinate,
            OpenSpaceLookDepth,
            _player.SelectedTilePosition,
            _simulation.PlayerCharacter?.Position,
            _simulation.SelectedCharacter?.Position);

    public Vec3Int PositionFromSlice(int a, int b)
        => _simulation.SliceMode == RpSliceMode.Horizontal
            ? new Vec3Int(a, _simulation.SliceCoordinate, b)
            : new Vec3Int(a, b, _simulation.SliceCoordinate);
}
