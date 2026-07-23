using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using RPSystem.Desktop.Services;

namespace RPSystem.Desktop.ViewModels;

/// <summary>
/// Manages player movement, actions, keybindings, and inventory.
/// </summary>
public sealed partial class PlayerControlViewModel : ObservableObject
{
    private readonly WorldSimulationViewModel _simulation;
    private readonly RpSimulationService _simulationService;
    private readonly RpWorldInspectionService _inspectionService;
    private readonly RpInventoryService _inventoryService;
    private readonly RpInteractionService _interactionService;
    private readonly RpAbilityService _abilityService;
    private readonly ISettingsService _settingsService;
    private readonly RpConversationService _conversationService;

    [ObservableProperty] private Character? _playerCharacter;
    [ObservableProperty] private string _playerInput = string.Empty;
    [ObservableProperty] private string _selectedCharacterSummary = string.Empty;
    [ObservableProperty] private string _selectedTileInfo = "Click a tile to inspect it.";
    [ObservableProperty] private Vec3Int? _selectedTilePosition;
    [ObservableProperty] private Item? _selectedGroundItem;
    [ObservableProperty] private Item? _selectedInventoryItem;
    [ObservableProperty] private string _inventorySummary = "Inventory: empty";

    // Keybindings
    [ObservableProperty] private string _moveNorthKey = "W";
    [ObservableProperty] private string _moveSouthKey = "S";
    [ObservableProperty] private string _moveWestKey = "A";
    [ObservableProperty] private string _moveEastKey = "D";
    [ObservableProperty] private string _moveUpKey = "PageUp";
    [ObservableProperty] private string _moveDownKey = "PageDown";
    [ObservableProperty] private string _waitKey = "Space";
    [ObservableProperty] private string _lookModeKey = "L";
    [ObservableProperty] private string _moveModeKey = "M";

    public ObservableCollection<Item> GroundItems { get; } = [];
    public ObservableCollection<Item> InventoryItems { get; } = [];

    public string PlayerCharacterText => PlayerCharacter == null ? "No player" : $"Player: {PlayerCharacter.Name}";

    public bool IsInConversation => _simulation.World.ActiveConversation is { IsActive: true };

    public string ConversationPartnerName =>
        _simulation.World.ActiveConversation is { IsActive: true } session &&
        _simulation.World.Characters.TryGetValue(session.PartnerCharacterId, out var partner)
            ? partner.Name
            : string.Empty;

    public string ScenePhaseText =>
        RpSimulationService.GetActiveSceneContext(_simulation.World)?.SceneState.Phase.ToString() ?? "N/A";
    public string PlayerHealthText => PlayerCharacter == null ? "HP -/-" : $"HP {PlayerCharacter.Vitals.HealthCurrent:0.#}/{PlayerCharacter.Vitals.HealthMax:0.#}";
    public string PlayerManaText => PlayerCharacter == null ? "Mana -/-" : $"Mana {PlayerCharacter.Vitals.ManaCurrent:0.#}/{PlayerCharacter.Vitals.ManaMax:0.#}";
    public string PlayerFocusText => PlayerCharacter == null ? "Focus -/-" : $"Focus {PlayerCharacter.Vitals.FocusCurrent:0.#}/{PlayerCharacter.Vitals.FocusMax:0.#}";
    public string PlayerStaminaText => PlayerCharacter == null ? "Stamina -/-" : $"Stamina {PlayerCharacter.Vitals.StaminaCurrent:0.#}/{PlayerCharacter.Vitals.StaminaMax:0.#}";
    public double PlayerHealthProgress => Ratio(PlayerCharacter?.Vitals.HealthCurrent, PlayerCharacter?.Vitals.HealthMax);
    public double PlayerManaProgress => Ratio(PlayerCharacter?.Vitals.ManaCurrent, PlayerCharacter?.Vitals.ManaMax);
    public double PlayerFocusProgress => Ratio(PlayerCharacter?.Vitals.FocusCurrent, PlayerCharacter?.Vitals.FocusMax);
    public double PlayerStaminaProgress => Ratio(PlayerCharacter?.Vitals.StaminaCurrent, PlayerCharacter?.Vitals.StaminaMax);

    public PlayerControlViewModel(
        WorldSimulationViewModel simulation,
        RpSimulationService simulationService,
        RpWorldInspectionService inspectionService,
        RpInventoryService inventoryService,
        RpInteractionService interactionService,
        RpAbilityService abilityService,
        ISettingsService settingsService,
        RpConversationService conversationService)
    {
        _simulation = simulation;
        _simulationService = simulationService;
        _inspectionService = inspectionService;
        _inventoryService = inventoryService;
        _interactionService = interactionService;
        _abilityService = abilityService;
        _settingsService = settingsService;
        _conversationService = conversationService;

        _moveNorthKey = settingsService.GetString("RpMoveNorthKey", "W");
        _moveSouthKey = settingsService.GetString("RpMoveSouthKey", "S");
        _moveWestKey = settingsService.GetString("RpMoveWestKey", "A");
        _moveEastKey = settingsService.GetString("RpMoveEastKey", "D");
        _moveUpKey = settingsService.GetString("RpMoveUpKey", "PageUp");
        _moveDownKey = settingsService.GetString("RpMoveDownKey", "PageDown");
        _waitKey = settingsService.GetString("RpWaitKey", "Space");
        _lookModeKey = settingsService.GetString("RpLookModeKey", "L");
        _moveModeKey = settingsService.GetString("RpMoveModeKey", "M");

        // Subscribe to simulation property changes
        _simulation.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorldSimulationViewModel.PlayerCharacter))
            {
                PlayerCharacter = _simulation.PlayerCharacter;
                RaisePlayerStatsChanged();
                RefreshInventory();
            }
        };

        PlayerCharacter = _simulation.PlayerCharacter;
    }

    partial void OnPlayerCharacterChanged(Character? value)
    {
        OnPropertyChanged(nameof(PlayerCharacterText));
        RaisePlayerStatsChanged();
        RefreshInventory();
    }

    partial void OnSelectedTilePositionChanged(Vec3Int? value)
    {
        RefreshGroundItems();
    }

    partial void OnMoveNorthKeyChanged(string value) => _settingsService.SetString("RpMoveNorthKey", NormalizeKeyName(value, "W"));
    partial void OnMoveSouthKeyChanged(string value) => _settingsService.SetString("RpMoveSouthKey", NormalizeKeyName(value, "S"));
    partial void OnMoveWestKeyChanged(string value) => _settingsService.SetString("RpMoveWestKey", NormalizeKeyName(value, "A"));
    partial void OnMoveEastKeyChanged(string value) => _settingsService.SetString("RpMoveEastKey", NormalizeKeyName(value, "D"));
    partial void OnMoveUpKeyChanged(string value) => _settingsService.SetString("RpMoveUpKey", NormalizeKeyName(value, "PageUp"));
    partial void OnMoveDownKeyChanged(string value) => _settingsService.SetString("RpMoveDownKey", NormalizeKeyName(value, "PageDown"));
    partial void OnWaitKeyChanged(string value) => _settingsService.SetString("RpWaitKey", NormalizeKeyName(value, "Space"));
    partial void OnLookModeKeyChanged(string value) => _settingsService.SetString("RpLookModeKey", NormalizeKeyName(value, "L"));
    partial void OnMoveModeKeyChanged(string value) => _settingsService.SetString("RpMoveModeKey", NormalizeKeyName(value, "M"));

    [RelayCommand]
    public void ResetControls()
    {
        MoveNorthKey = "W";
        MoveSouthKey = "S";
        MoveWestKey = "A";
        MoveEastKey = "D";
        MoveUpKey = "PageUp";
        MoveDownKey = "PageDown";
        WaitKey = "Space";
        LookModeKey = "L";
        MoveModeKey = "M";
        _simulation.StatusText = "RP controls reset to defaults.";
    }

    [RelayCommand]
    public void SetSelectedAsPlayer()
    {
        if (_simulation.SelectedCharacter == null)
        {
            _simulation.StatusText = "Select a character first.";
            return;
        }

        PlayerCharacter = _simulation.SelectedCharacter;
        _simulation.PlayerCharacter = _simulation.SelectedCharacter;
        _simulation.StatusText = $"{PlayerCharacter.Name} is now the player character.";
    }

    [RelayCommand]
    public Task MovePlayerNorth() => MovePlayer(Direction.North);

    [RelayCommand]
    public Task MovePlayerSouth() => MovePlayer(Direction.South);

    [RelayCommand]
    public Task MovePlayerEast() => MovePlayer(Direction.East);

    [RelayCommand]
    public Task MovePlayerWest() => MovePlayer(Direction.West);

    [RelayCommand]
    public Task MovePlayerUp() => MovePlayer(Direction.Ceil);

    [RelayCommand]
    public Task MovePlayerDown() => MovePlayer(Direction.Floor);

    private Task MovePlayer(Direction direction)
    {
        if (_simulation.IsBusy)
        {
            _simulation.StatusText = "Wait for the current AI/world turn to finish.";
            return Task.CompletedTask;
        }

        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return Task.CompletedTask;
        }

        var target = PlayerCharacter.Position + RpSimulationService.OffsetFor(direction);
        return TryMovePlayerTo(target, direction.ToString());
    }

    public async Task TryMovePlayerTo(Vec3Int target, string directionOrReason)
    {
        if (_simulation.IsBusy)
        {
            _simulation.StatusText = "Wait for the current AI/world turn to finish.";
            return;
        }

        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (!RpSimulationService.TryMoveCharacter(_simulation.World, PlayerCharacter, target, out var error))
        {
            _simulation.StatusText = error;
            return;
        }

        _simulation.SelectedCharacter = PlayerCharacter;
        if (_simulation.SliceMode == RpSliceMode.Horizontal)
        {
            _simulation.SliceCoordinate = PlayerCharacter.Position.Y;
        }
        else
        {
            _simulation.SliceCoordinate = PlayerCharacter.Position.Z;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = _simulation.World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} moved {directionOrReason} to {PlayerCharacter.Position}."
        });
        _simulation.StatusText = $"{PlayerCharacter.Name} moved to {PlayerCharacter.Position}.";
        RefreshInventory();
        RefreshGroundItems();
        _simulation.RaiseWorldChanged();
        await _simulation.AdvanceWorldAfterPlayerActionAsync(_simulation.StatusText);
    }

    [RelayCommand]
    public void CastFireballAtInspectedTile()
    {
        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedTilePosition == null)
        {
            _simulation.StatusText = "Inspect a tile first.";
            return;
        }

        if (_abilityService.TryCastFireball(_simulation.World, PlayerCharacter, SelectedTilePosition.Value, out var evt, out var status))
        {
            AddEventToAll(evt);
            LookAtTile(SelectedTilePosition.Value);
            _simulation.RaiseWorldChanged();
            _ = _simulation.AdvanceWorldAfterPlayerActionAsync(status);
            return;
        }

        _simulation.StatusText = status;
    }

    [RelayCommand]
    public void TalkToInspectedCharacter()
    {
        var target = _interactionService.GetInspectedCharacters(_simulation.World, SelectedTilePosition)
            .FirstOrDefault(c => PlayerCharacter == null || c.Id != PlayerCharacter.Id);

        if (target == null)
        {
            _simulation.StatusText = "No character on inspected tile to talk to.";
            return;
        }

        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        var (success, status, openingEvent) = _conversationService.StartConversation(_simulation.World, PlayerCharacter.Id, target.Id);
        _simulation.StatusText = status;
        if (success && openingEvent != null)
        {
            AddEventToAll(openingEvent);
        }

        OnPropertyChanged(nameof(IsInConversation));
        OnPropertyChanged(nameof(ConversationPartnerName));
    }

    [RelayCommand]
    public void EndConversation()
    {
        var closingEvent = _conversationService.EndConversation(_simulation.World);
        if (closingEvent != null)
        {
            AddEventToAll(closingEvent);
            _simulation.StatusText = "Conversation ended.";
        }

        OnPropertyChanged(nameof(IsInConversation));
        OnPropertyChanged(nameof(ConversationPartnerName));
    }

    [RelayCommand]
    public void InteractWithInspectedTile()
    {
        var evt = _interactionService.CreateInteractEvent(_simulation.World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        _simulation.StatusText = status;
    }

    [RelayCommand]
    public void UseHeldItemOnInspectedTile()
    {
        var evt = _interactionService.CreateUseEvent(_simulation.World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        _simulation.StatusText = status;
    }

    [RelayCommand]
    public void AttackInspectedTarget()
    {
        var evt = _interactionService.CreateAttackEvent(_simulation.World, PlayerCharacter, SelectedTilePosition, out var status);
        AddEventIfAny(evt);
        _simulation.StatusText = status;
    }

    [RelayCommand]
    public void AddPlayerInput()
    {
        if (string.IsNullOrWhiteSpace(PlayerInput)) return;

        if (_simulation.World.ActiveConversation is { IsActive: true })
        {
            var evt = _conversationService.AddPlayerLine(_simulation.World, PlayerInput.Trim());
            _simulation.EventLog.Insert(0, FormatEvent(evt));
            PlayerInput = string.Empty;
            TrimEventLog();
            _simulation.StatusText = "Sent.";
            return;
        }

        var evt2 = new NarrativeEvent
        {
            Tick = _simulation.World.Clock.TickCount,
            ActorName = "Narrator",
            Description = PlayerInput.Trim()
        };

        foreach (var character in _simulation.World.Characters.Values)
        {
            character.PerceivedLog.Add(evt2);
            if (character.PerceivedLog.Count > 25)
            {
                character.PerceivedLog = character.PerceivedLog.TakeLast(25).ToList();
            }
        }

        _simulation.EventLog.Insert(0, FormatEvent(evt2));
        PlayerInput = string.Empty;
        TrimEventLog();
        _simulation.StatusText = "Narrator event added.";
    }

    [RelayCommand]
    public void PickUpSelectedGroundItem()
    {
        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedGroundItem == null)
        {
            _simulation.StatusText = "Select a ground item first.";
            return;
        }

        if (!_inventoryService.TryPickUp(_simulation.World, PlayerCharacter, SelectedGroundItem, out var error))
        {
            _simulation.StatusText = error;
            return;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = _simulation.World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} picked up {SelectedGroundItem.Name}."
        });
        RefreshInventory();
        RefreshGroundItems();
        LookAtTile(PlayerCharacter.Position);
    }

    [RelayCommand]
    public void DropSelectedInventoryItem()
    {
        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedInventoryItem == null)
        {
            _simulation.StatusText = "Select an inventory item first.";
            return;
        }

        if (!_inventoryService.TryDrop(_simulation.World, PlayerCharacter, SelectedInventoryItem, out var error))
        {
            _simulation.StatusText = error;
            return;
        }

        AddEventToAll(new NarrativeEvent
        {
            Tick = _simulation.World.Clock.TickCount,
            ActorName = PlayerCharacter.Name,
            Description = $"{PlayerCharacter.Name} dropped {SelectedInventoryItem.Name}."
        });
        RefreshInventory();
        RefreshGroundItems();
        LookAtTile(PlayerCharacter.Position);
    }

    [RelayCommand]
    public void HoldSelectedInventoryItem()
    {
        if (PlayerCharacter == null)
        {
            _simulation.StatusText = "Choose a player character first.";
            return;
        }

        if (SelectedInventoryItem == null)
        {
            _simulation.StatusText = "Select an inventory item first.";
            return;
        }

        if (!_inventoryService.TryHold(PlayerCharacter, SelectedInventoryItem, out var error))
        {
            _simulation.StatusText = error;
            return;
        }

        RefreshInventory();
        _simulation.StatusText = $"Holding {SelectedInventoryItem.Name}.";
    }

    public void LookAtTile(Vec3Int position)
    {
        SelectedTilePosition = position;
        SelectedTileInfo = _inspectionService.InspectTile(_simulation.World, position).Description;
        _simulation.StatusText = $"Looked at {position}.";
        RefreshGroundItems();
    }

    public bool TryHandleKey(string keyName)
    {
        var normalized = NormalizeKeyName(keyName, string.Empty);
        if (KeyMatches(normalized, MoveNorthKey, "Up", "NumberPad8"))
        {
            _ = MovePlayerNorth();
            return true;
        }
        if (KeyMatches(normalized, MoveSouthKey, "Down", "NumberPad2"))
        {
            _ = MovePlayerSouth();
            return true;
        }
        if (KeyMatches(normalized, MoveWestKey, "Left", "NumberPad4"))
        {
            _ = MovePlayerWest();
            return true;
        }
        if (KeyMatches(normalized, MoveEastKey, "Right", "NumberPad6"))
        {
            _ = MovePlayerEast();
            return true;
        }
        if (KeyMatches(normalized, MoveUpKey, "NumberPad9"))
        {
            _ = MovePlayerUp();
            return true;
        }
        if (KeyMatches(normalized, MoveDownKey, "NumberPad3"))
        {
            _ = MovePlayerDown();
            return true;
        }
        if (KeyMatches(normalized, WaitKey))
        {
            _ = _simulation.StepTick();
            return true;
        }
        if (KeyMatches(normalized, LookModeKey))
        {
            // Mode switch handled by MapActionMode — forwarded through Map
            return true;
        }
        if (KeyMatches(normalized, MoveModeKey))
        {
            return true;
        }
        return false;
    }

    private void RefreshGroundItems()
    {
        GroundItems.Clear();
        foreach (var item in _inventoryService.GetGroundItems(_simulation.World, SelectedTilePosition))
        {
            GroundItems.Add(item);
        }
        if (SelectedGroundItem == null || !GroundItems.Contains(SelectedGroundItem))
        {
            SelectedGroundItem = GroundItems.FirstOrDefault();
        }
    }

    private void RefreshInventory()
    {
        InventoryItems.Clear();
        if (PlayerCharacter == null)
        {
            InventorySummary = _inventoryService.GetInventorySummary(_simulation.World, PlayerCharacter);
            SelectedInventoryItem = null;
            return;
        }

        foreach (var item in _inventoryService.GetInventoryItems(_simulation.World, PlayerCharacter))
        {
            InventoryItems.Add(item);
        }
        if (SelectedInventoryItem == null || !InventoryItems.Contains(SelectedInventoryItem))
        {
            SelectedInventoryItem = InventoryItems.FirstOrDefault();
        }
        InventorySummary = _inventoryService.GetInventorySummary(_simulation.World, PlayerCharacter);
    }

    private void RaisePlayerStatsChanged()
    {
        OnPropertyChanged(nameof(PlayerHealthText));
        OnPropertyChanged(nameof(PlayerManaText));
        OnPropertyChanged(nameof(PlayerFocusText));
        OnPropertyChanged(nameof(PlayerStaminaText));
        OnPropertyChanged(nameof(PlayerHealthProgress));
        OnPropertyChanged(nameof(PlayerManaProgress));
        OnPropertyChanged(nameof(PlayerFocusProgress));
        OnPropertyChanged(nameof(PlayerStaminaProgress));
    }

    private void AddEventIfAny(NarrativeEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.Description))
        {
            AddEventToAll(evt);
        }
    }

    private void AddEventToAll(NarrativeEvent evt)
    {
        foreach (var character in _simulation.World.Characters.Values)
        {
            character.PerceivedLog.Add(evt);
            if (character.PerceivedLog.Count > 25)
            {
                character.PerceivedLog = character.PerceivedLog.TakeLast(25).ToList();
            }
        }
        _simulation.EventLog.Insert(0, FormatEvent(evt));
        TrimEventLog();
    }

    private void TrimEventLog()
    {
        while (_simulation.EventLog.Count > 80)
        {
            _simulation.EventLog.RemoveAt(_simulation.EventLog.Count - 1);
        }
    }

    private static bool KeyMatches(string key, string configured, params string[] aliases)
    {
        var configuredNormalized = NormalizeKeyName(configured, string.Empty);
        return string.Equals(key, configuredNormalized, StringComparison.OrdinalIgnoreCase) ||
            aliases.Any(alias => string.Equals(key, NormalizeKeyName(alias, alias), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeKeyName(string? value, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Equals("PgUp", StringComparison.OrdinalIgnoreCase) ? "PageUp" :
            normalized.Equals("PgDn", StringComparison.OrdinalIgnoreCase) ? "PageDown" :
            normalized.Equals("Esc", StringComparison.OrdinalIgnoreCase) ? "Escape" :
            normalized.Length == 1 ? normalized.ToUpperInvariant() :
            normalized;
    }

    private static string FormatEvent(NarrativeEvent evt)
        => $"T{evt.Tick} {evt.ActorName}: {evt.Description}";

    private static double Ratio(float? current, float? max)
    {
        var maxValue = max.GetValueOrDefault();
        return maxValue <= 0 ? 0 : Math.Clamp(current.GetValueOrDefault() / maxValue, 0, 1);
    }
}
