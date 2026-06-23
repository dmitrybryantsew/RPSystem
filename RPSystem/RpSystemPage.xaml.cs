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
        canvas.Clear(new SKColor(250, 251, 252));

        if (BindingContext is not RpSystemViewModel vm)
        {
            return;
        }

        using var textPaint = new SKPaint
        {
            Color = new SKColor(25, 35, 45),
            IsAntialias = true,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        using var tilePaint = new SKPaint { IsAntialias = false };
        using var borderPaint = new SKPaint
        {
            Color = new SKColor(207, 216, 224),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        using var hoverPaint = new SKPaint
        {
            Color = new SKColor(255, 193, 7, 90),
            Style = SKPaintStyle.Fill,
            IsAntialias = false
        };
        using var hoverBorderPaint = new SKPaint
        {
            Color = new SKColor(255, 193, 7),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = false
        };

        var snapshot = vm.GetMapRenderSnapshot();
        if (!snapshot.HasCells)
        {
            DrawCenteredText(canvas, e.Info.Width, e.Info.Height, "No tiles in this slice", textPaint);
            return;
        }

        _lastRenderSnapshot = snapshot;

        var contentRect = new SKRect(8, 8, e.Info.Width - 8, Math.Max(40, e.Info.Height - 34));
        _lastContentRect = contentRect;

        var cols = Math.Max(1, snapshot.MaxA - snapshot.MinA + 1);
        var rows = Math.Max(1, snapshot.MaxB - snapshot.MinB + 1);
        var tileSize = MathF.Round(26f * _zoom);
        var boardWidth = cols * tileSize;
        var boardHeight = rows * tileSize;
        var startX = contentRect.MidX - (boardWidth / 2f) + _panX;
        var startY = contentRect.MidY - (boardHeight / 2f) + _panY;
        _lastBoardRect = new SKRect(startX, startY, startX + boardWidth, startY + boardHeight);
        _lastTileSize = tileSize;
        _lastMinA = snapshot.MinA;
        _lastMaxB = snapshot.MaxB;
        textPaint.TextSize = Math.Clamp(tileSize * 0.48f, 10, 24);

        canvas.Save();
        canvas.ClipRect(contentRect);
        foreach (var cell in snapshot.Cells)
        {
            var col = cell.A - snapshot.MinA;
            var row = snapshot.MaxB - cell.B;
            var rect = new SKRect(
                startX + col * tileSize,
                startY + row * tileSize,
                startX + (col + 1) * tileSize,
                startY + (row + 1) * tileSize);

            if (!rect.IntersectsWith(contentRect))
            {
                continue;
            }

            tilePaint.Color = ColorForCell(cell);
            tilePaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, tilePaint);
            canvas.DrawRect(rect, borderPaint);

            if (_hoveredPosition.HasValue && _hoveredPosition.Value == cell.Position)
            {
                canvas.DrawRect(rect, hoverPaint);
                canvas.DrawRect(rect, hoverBorderPaint);
            }

            var displayGlyph = cell.IsOpenSpace ? cell.UnderlayGlyph : cell.Glyph;
            if (!string.IsNullOrWhiteSpace(displayGlyph))
            {
                textPaint.Color = cell.IsOpenSpace
                    ? OpenSpaceGlyphColor(cell)
                    : displayGlyph is "#" ? SKColors.White : new SKColor(23, 32, 42);
                canvas.DrawText(displayGlyph, rect.MidX, rect.MidY - ((textPaint.FontMetrics.Ascent + textPaint.FontMetrics.Descent) / 2), textPaint);
            }
        }
        canvas.Restore();

        DrawLegend(canvas, e.Info.Width, e.Info.Height, vm, textPaint);
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

    private static void DrawCenteredText(SKCanvas canvas, int width, int height, string text, SKPaint paint)
    {
        paint.TextSize = 18;
        paint.Color = new SKColor(93, 109, 126);
        canvas.DrawText(text, width / 2f, height / 2f, paint);
    }

    private static void DrawLegend(SKCanvas canvas, int width, int height, RpSystemViewModel vm, SKPaint paint)
    {
        using var backgroundPaint = new SKPaint { Color = new SKColor(250, 251, 252, 235), Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(0, height - 30, width, height), backgroundPaint);
        paint.TextSize = 13;
        paint.Color = new SKColor(86, 101, 115);
        paint.TextAlign = SKTextAlign.Left;
        canvas.DrawText("# wall  . floor  , moss  + glass  dim/darker below = open space  ? below cutoff  ▲ ramp  H ladder  * item  letters chars", 10, height - 11, paint);
        paint.TextAlign = SKTextAlign.Right;
        canvas.DrawText(vm.SliceModeText, width - 10, height - 11, paint);
        paint.TextAlign = SKTextAlign.Center;
    }

    private static SKColor ColorForCell(RpMapRenderCell cell)
    {
        if (cell.IsOpenSpace)
        {
            var shade = OpenSpaceShade(cell);
            return new SKColor(shade, (byte)Math.Clamp(shade + 2, 0, 255), (byte)Math.Clamp(shade + 5, 0, 255));
        }

        return ColorForGlyph(cell.Glyph);
    }

    private static SKColor OpenSpaceGlyphColor(RpMapRenderCell cell)
    {
        if (cell.IsUnderlayClipped)
        {
            return new SKColor(72, 84, 96, 190);
        }

        var depth = Math.Clamp(cell.UnderlayDepth, 1, 12);
        var channel = (byte)Math.Clamp(145 - (depth * 11), 55, 145);
        var alpha = (byte)Math.Clamp(180 - (depth * 8), 80, 180);
        return new SKColor(channel, (byte)Math.Clamp(channel + 8, 0, 255), (byte)Math.Clamp(channel + 16, 0, 255), alpha);
    }

    private static byte OpenSpaceShade(RpMapRenderCell cell)
    {
        if (cell.IsUnderlayClipped)
        {
            return 211;
        }

        if (cell.UnderlayDepth <= 0)
        {
            return 239;
        }

        return (byte)Math.Clamp(242 - (cell.UnderlayDepth * 9), 178, 242);
    }

    private static SKColor ColorForGlyph(string glyph)
        => glyph switch
        {
            "#" => new SKColor(72, 84, 96),
            "+" => new SKColor(188, 232, 241),
            "▲" => new SKColor(184, 134, 68),
            "▼" => new SKColor(206, 158, 91),
            "H" => new SKColor(169, 117, 74),
            "," => new SKColor(186, 213, 162),
            "." => new SKColor(224, 232, 215),
            "~" => new SKColor(94, 151, 191),
            "*" => new SKColor(242, 201, 109),
            " " => new SKColor(238, 241, 244),
            _ => new SKColor(245, 176, 65)
        };

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
