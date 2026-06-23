# RPSystem

A desktop application for world simulation, character management, and narrative
storytelling. Built with Avalonia UI and SkiaSharp.

## Project Layout

```
RPSystem/
├── RPSystem.Core/        # Business logic, models, services (no UI dependencies)
├── RPSystem.Desktop/     # Avalonia UI (views, viewmodels, controls, converters)
└── RPSystem.Tests/       # Unit tests (xUnit)
```

### RPSystem.Core

Pure .NET 9 class library. Contains:
- `RpSystem/` — World, Character, Faction, Ability, and other domain models
- `Services/` — Simulation, pathfinding, map rendering, save/load, markdown import
- No platform-specific dependencies (no MAUI, no Avalonia).

### RPSystem.Desktop

Avalonia 11 desktop application (Windows, macOS, Linux). Contains:
- `Views/` — UserControls: MainMenu, World, Settings, WorldContext, GameOptions, DevMenu, TestMaps
- `ViewModels/` — Focused viewmodels (one per concern):
  - `MainShellViewModel` — Top-level shell, composes all child viewmodels
  - `WorldSimulationViewModel` — Ticking, save/load, model selection
  - `PlayerControlViewModel` — Player movement, actions, keybindings, inventory
  - `WorldMapViewModel` — Slice, selection, map rendering modes
  - `TestMapsViewModel` — Test map loading
  - `WorldContextEditorViewModel` — Top-level context editor, composes child editors
  - `ContextModuleEditorViewModel` — Context module editing
  - `ContextCharacterEditorViewModel` — Character profile editing
  - `ContextAbilityEditorViewModel` — Ability editing
  - `RelationshipRuleEditorViewModel` — Relationship rule editing
  - `FactionProfileEditorViewModel` — Faction profile editing
  - `SpeciesTemplateEditorViewModel` — Species template editing
  - `SceneEnvironmentContinuityEditorViewModel` — Scene/environment/continuity editing
- `Controls/` — `WorldMapCanvas` (SkiaSharp custom drawing)
- `Converters/` — Value converters (BoolToArrow, BoolToColor, etc.)
- `Services/` — Platform services (settings, clipboard, file picker)

### RPSystem.Tests

xUnit test suite covering:
- Simulation service ticking and determinism
- Pathfinding and flow-field caching
- World save/load round-trip
- Markdown import (including prompt injection handling)
- Map render projection
- Character composition and faction assignment

## How to Run

```bash
# Run the application
dotnet run --project RPSystem.Desktop

# Run all tests
dotnet test RPSystem.Tests/RPSystem.Tests.csproj

# Build the entire solution
dotnet build RPSystem.sln
```

## Architecture

The original codebase had a single 3200+ line god-object viewmodel. This was
decomposed into 13 focused viewmodels, each owning a single concern. The
`MainShellViewModel` composes them, and navigation between views is handled by
boolean flags (`IsWorldView`, `IsSettingsView`, etc.) bound to `IsVisible` on
each `UserControl`.

Shared text-formatting and parsing helpers live in `RPSystem.Core/Services/EditorTextFormat.cs`
so they can be used by multiple viewmodels without duplication.

Platform abstraction is achieved through interfaces (`ISettingsService`,
`IClipboardService`, `IFilePickerService`) implemented by the Desktop project
and registered via dependency injection at startup.

## Security Notes

- **No certificate validation bypass.** The original codebase had
  `ServerCertificateCustomValidationCallback = (_, _, _, _) => true` which
  was removed in Phase 01. Any re-introduction of TLS bypass is a regression.
- **No hardcoded endpoints.** The original codebase pointed at a personal
  server URL which was removed. All endpoints are user-configured via
  `config.yaml`. Any re-addition of a hardcoded external host is a regression.
