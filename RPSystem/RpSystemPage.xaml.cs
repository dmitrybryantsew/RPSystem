using RPSystem.Core.RpSystem;
using RPSystem.Services;
using RPSystem.Core.Services;
using RPSystem.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace RPSystem;

public partial class RpSystemPage : ContentPage
{
    private SKRect _lastBoardRect;
    private SKRect _lastContentRect;
    private float _lastTileSize;
    private int _lastMinA;
    private int _lastMaxB;
    private RpMapRenderSnapshot? _lastRenderSnapshot;
    private float _zoom = 1f;
    private float _panX;
    private float _panY;
    private SKPoint? _lastTouchPoint;
    private bool _touchMoved;
    private Vec3Int? _hoveredPosition;
#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _windowsKeyRoot;
#endif

    public RpSystemPage(RpSystemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += (_, _) => WorldCanvas?.InvalidateSurface();
        viewModel.EventLog.CollectionChanged += (_, _) => WorldCanvas?.InvalidateSurface();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        AttachWindowsKeyboardHandling();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if WINDOWS
        AttachWindowsKeyboardHandling();
        if (_windowsKeyRoot is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        }
#endif
    }

    private void OnWorldCanvasPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        if (BindingContext is not RpSystemViewModel vm)
        {
            canvas.Clear(new SKColor(250, 251, 252));
            return;
        }

        var snapshot = vm.GetMapRenderSnapshot();
        _lastRenderSnapshot = snapshot;
        _lastContentRect = new SKRect(8, 8, e.Info.Width - 8, Math.Max(40, e.Info.Height - 34));
        _lastTileSize = MathF.Round(26f * _zoom);
        _lastMinA = snapshot.MinA;
        _lastMaxB = snapshot.MaxB;

        if (snapshot.HasCells)
        {
            var cols = Math.Max(1, snapshot.MaxA - snapshot.MinA + 1);
            var rows = Math.Max(1, snapshot.MaxB - snapshot.MinB + 1);
            var boardWidth = cols * _lastTileSize;
            var boardHeight = rows * _lastTileSize;
            _lastBoardRect = new SKRect(
                _lastContentRect.MidX - (boardWidth / 2f) + _panX,
                _lastContentRect.MidY - (boardHeight / 2f) + _panY,
                _lastContentRect.MidX + (boardWidth / 2f) + _panX,
                _lastContentRect.MidY + (boardHeight / 2f) + _panY);
        }

        MapRenderer.DrawWorld(canvas, e.Info, snapshot, _hoveredPosition, _zoom, _panX, _panY, vm.SliceModeText);
    }

    private void OnWorldCanvasTouch(object sender, SKTouchEventArgs e)
    {
        if (BindingContext is not RpSystemViewModel vm || _lastTileSize <= 0)
        {
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Pressed)
        {
            _lastTouchPoint = e.Location;
            _touchMoved = false;
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Moved && _lastTouchPoint.HasValue)
        {
            var delta = e.Location - _lastTouchPoint.Value;
            if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
            {
                _touchMoved = true;
            }

            _panX += delta.X;
            _panY += delta.Y;
            _lastTouchPoint = e.Location;
            UpdateHoveredTile(vm, e.Location);
            WorldCanvas?.InvalidateSurface();
            e.Handled = true;
            return;
        }

        if (e.ActionType == SKTouchAction.Moved)
        {
            UpdateHoveredTile(vm, e.Location);
            WorldCanvas?.InvalidateSurface();
            e.Handled = true;
            return;
        }

        if (e.ActionType != SKTouchAction.Released ||
            _touchMoved ||
            !_lastContentRect.Contains(e.Location) ||
            !_lastBoardRect.Contains(e.Location))
        {
            _lastTouchPoint = null;
            e.Handled = true;
            return;
        }

        var col = (int)((e.Location.X - _lastBoardRect.Left) / _lastTileSize);
        var row = (int)((e.Location.Y - _lastBoardRect.Top) / _lastTileSize);
        var a = _lastMinA + col;
        var b = _lastMaxB - row;
        var position = _lastRenderSnapshot?.PositionFromSlice(a, b) ?? vm.PositionFromSlice(a, b);
        _hoveredPosition = position;
        vm.HandleMapTileClick(position);

        var character = vm.World.Characters.Values.FirstOrDefault(c => c.Position == position);
        if (character != null)
        {
            vm.SelectedCharacter = character;
        }

        _lastTouchPoint = null;
        e.Handled = true;
    }

    private void UpdateHoveredTile(RpSystemViewModel vm, SKPoint location)
    {
        if (!_lastContentRect.Contains(location) || !_lastBoardRect.Contains(location))
        {
            _hoveredPosition = null;
            return;
        }

        var col = (int)((location.X - _lastBoardRect.Left) / _lastTileSize);
        var row = (int)((location.Y - _lastBoardRect.Top) / _lastTileSize);
        var a = _lastMinA + col;
        var b = _lastMaxB - row;
        _hoveredPosition = _lastRenderSnapshot?.PositionFromSlice(a, b) ?? vm.PositionFromSlice(a, b);
    }

    private void OnZoomInClicked(object sender, EventArgs e)
    {
        _zoom = Math.Min(3.5f, _zoom * 1.2f);
        WorldCanvas?.InvalidateSurface();
    }

    private void OnZoomOutClicked(object sender, EventArgs e)
    {
        _zoom = Math.Max(0.45f, _zoom / 1.2f);
        WorldCanvas?.InvalidateSurface();
    }

    private void OnCenterMapClicked(object sender, EventArgs e)
    {
        CenterOnSelectedCharacter();
        WorldCanvas?.InvalidateSurface();
    }

    private void CenterOnSelectedCharacter()
    {
        if (BindingContext is not RpSystemViewModel vm ||
            !vm.TryGetVisibleBounds(out var minA, out var maxA, out var minB, out var maxB))
        {
            _panX = 0;
            _panY = 0;
            return;
        }

        var target = vm.PlayerCharacter?.Position ?? vm.SelectedCharacter?.Position;
        if (target == null)
        {
            _panX = 0;
            _panY = 0;
            return;
        }

        var a = target.Value.X;
        var b = vm.SliceMode == RpSliceMode.Horizontal ? target.Value.Z : target.Value.Y;
        var cols = Math.Max(1, maxA - minA + 1);
        var rows = Math.Max(1, maxB - minB + 1);
        var tileSize = 26f * _zoom;
        var characterXFromBoardCenter = ((a - minA) + 0.5f) * tileSize - ((cols * tileSize) / 2f);
        var characterYFromBoardCenter = ((maxB - b) + 0.5f) * tileSize - ((rows * tileSize) / 2f);
        _panX = -characterXFromBoardCenter;
        _panY = -characterYFromBoardCenter;
    }

#if WINDOWS
    private void AttachWindowsKeyboardHandling()
    {
        Microsoft.UI.Xaml.UIElement? root = null;
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            root = nativeWindow.Content;
        }

        root ??= Handler?.PlatformView as Microsoft.UI.Xaml.UIElement;
        if (root == null)
        {
            return;
        }

        if (_windowsKeyRoot != null)
        {
            _windowsKeyRoot.KeyDown -= OnWindowsRootKeyDown;
        }

        _windowsKeyRoot = root;
        if (_windowsKeyRoot is Microsoft.UI.Xaml.Controls.Control control)
        {
            control.IsTabStop = true;
        }
        _windowsKeyRoot.KeyDown += OnWindowsRootKeyDown;
    }

    private void OnWindowsRootKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (BindingContext is not RpSystemViewModel vm)
        {
            return;
        }

        if (vm.TryHandleKey(e.Key.ToString()))
        {
            e.Handled = true;
        }

        if (!e.Handled)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.T:
                    vm.SetTalkModeCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.I:
                    vm.SetInteractModeCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.U:
                    vm.SetUseModeCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F:
                    vm.SetAttackModeCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        if (e.Handled)
        {
            WorldCanvas?.InvalidateSurface();
        }
    }
#endif
}
