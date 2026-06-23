using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
using RPSystem.Core.RpSystem;
using RPSystem.Core.Services;
using SkiaSharp;

namespace RPSystem.Desktop.Controls;

/// <summary>
/// Avalonia custom-drawn control that renders the RP world map using SkiaSharp.
/// Replaces the MAUI SKCanvasView from RpSystemPage.xaml.cs.
/// </summary>
public class WorldMapCanvas : Control
{
    private RpMapRenderSnapshot? _snapshot;
    private Vec3Int? _hoveredPosition;
    private float _zoom = 1f;
    private float _panX;
    private float _panY;
    private Point? _lastPointer;
    private bool _pointerMoved;
    private string _sliceModeText = string.Empty;

    // Layout state (mirrors _lastBoardRect / _lastTileSize from MAUI code)
    private SKRect _lastBoardRect;
    private float _lastTileSize;
    private int _lastMinA;
    private int _lastMaxB;

    public RpMapRenderSnapshot? Snapshot
    {
        get => _snapshot;
        set { _snapshot = value; InvalidateVisual(); }
    }

    public Vec3Int? HoveredPosition
    {
        get => _hoveredPosition;
        set { _hoveredPosition = value; InvalidateVisual(); }
    }

    public float Zoom
    {
        get => _zoom;
        set { _zoom = Math.Clamp(value, 0.45f, 3.5f); InvalidateVisual(); }
    }

    public float PanX
    {
        get => _panX;
        set { _panX = value; InvalidateVisual(); }
    }

    public float PanY
    {
        get => _panY;
        set { _panY = value; InvalidateVisual(); }
    }

    public string SliceModeText
    {
        get => _sliceModeText;
        set { _sliceModeText = value; InvalidateVisual(); }
    }

    /// <summary>Raised when a tile is clicked (not dragged).</summary>
    public event Action<Vec3Int>? TileClicked;

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0, 0, Bounds.Width, Bounds.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using var lease = SKBitmapLease.Acquire((int)bounds.Width, (int)bounds.Height);
        var canvas = lease.Canvas;
        var info = new SKImageInfo((int)bounds.Width, (int)bounds.Height);

        MapRenderer.DrawWorld(canvas, info, _snapshot ?? RpMapRenderSnapshot.Empty(RpSliceMode.Horizontal, 0),
            _hoveredPosition, _zoom, _panX, _panY, _sliceModeText);

        // Draw the SKBitmap onto the Avalonia DrawingContext
        var avaloniaBitmap = lease.ToAvaloniaBitmap();
        context.DrawImage(avaloniaBitmap, bounds);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        _lastPointer = e.GetPosition(this);
        _pointerMoved = false;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_lastPointer is { } last)
        {
            var current = e.GetPosition(this);
            var delta = current - last;

            if (Math.Abs(delta.X) > 1 || Math.Abs(delta.Y) > 1)
                _pointerMoved = true;

            _panX += (float)delta.X;
            _panY += (float)delta.Y;
            _lastPointer = current;
            UpdateHoveredTile(current);
            InvalidateVisual();
        }
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_pointerMoved && _lastPointer is { } pt && _lastTileSize > 0)
        {
            var pos = ScreenToTile(pt);
            if (pos.HasValue)
            {
                _hoveredPosition = pos;
                TileClicked?.Invoke(pos.Value);
                InvalidateVisual();
            }
        }
        _lastPointer = null;
        e.Handled = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var factor = e.Delta.Y > 0 ? 1.1f : 0.9f;
        Zoom *= factor;
        e.Handled = true;
    }

    private Vec3Int? ScreenToTile(Point pt)
    {
        if (_snapshot is not { HasCells: true } || _lastTileSize <= 0)
            return null;

        var col = (int)((pt.X - _lastBoardRect.Left) / _lastTileSize);
        var row = (int)((pt.Y - _lastBoardRect.Top) / _lastTileSize);
        var a = _lastMinA + col;
        var b = _lastMaxB - row;
        return _snapshot.PositionFromSlice(a, b);
    }

    private void UpdateHoveredTile(Point pt)
    {
        var pos = ScreenToTile(pt);
        if (pos != _hoveredPosition)
        {
            _hoveredPosition = pos;
        }
    }
}

/// <summary>
/// Helper to lease an SKBitmap for drawing and convert to Avalonia IBitmap.
/// </summary>
internal static class SKBitmapLease
{
    public static Lease Acquire(int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        return new Lease(bitmap);
    }

    public sealed class Lease : IDisposable
    {
        public SKCanvas Canvas { get; }
        private readonly SKBitmap _bitmap;

        public Lease(SKBitmap bitmap)
        {
            _bitmap = bitmap;
            Canvas = new SKCanvas(bitmap);
        }

        public Avalonia.Media.Imaging.Bitmap ToAvaloniaBitmap()
        {
            using var image = SKImage.FromBitmap(_bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new System.IO.MemoryStream(data.ToArray());
            return new Avalonia.Media.Imaging.Bitmap(stream);
        }

        public void Dispose()
        {
            Canvas.Dispose();
            _bitmap.Dispose();
        }
    }
}
