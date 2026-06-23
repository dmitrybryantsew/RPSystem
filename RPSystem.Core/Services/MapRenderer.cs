using SkiaSharp;
using RPSystem.Core.RpSystem;

namespace RPSystem.Core.Services;

/// <summary>
/// Pure SkiaSharp drawing logic for the world map canvas.
/// Platform-agnostic: no MAUI or Avalonia dependencies.
/// Used by both MAUI (via service locator) and Avalonia (via DI).
/// </summary>
public static class MapRenderer
{
    public static void DrawWorld(
        SKCanvas canvas,
        SKImageInfo info,
        RpMapRenderSnapshot snapshot,
        Vec3Int? hoveredPosition,
        float zoom,
        float panX,
        float panY,
        string sliceModeText)
    {
        canvas.Clear(new SKColor(250, 251, 252));

        if (!snapshot.HasCells)
        {
            using var noDataPaint = new SKPaint
            {
                Color = new SKColor(93, 109, 126),
                IsAntialias = true,
                TextAlign = SKTextAlign.Center,
                TextSize = 18,
                Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
            };
            canvas.DrawText("No tiles in this slice", info.Width / 2f, info.Height / 2f, noDataPaint);
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

        var contentRect = new SKRect(8, 8, info.Width - 8, Math.Max(40, info.Height - 34));

        var cols = Math.Max(1, snapshot.MaxA - snapshot.MinA + 1);
        var rows = Math.Max(1, snapshot.MaxB - snapshot.MinB + 1);
        var tileSize = MathF.Round(26f * zoom);
        var boardWidth = cols * tileSize;
        var boardHeight = rows * tileSize;
        var startX = contentRect.MidX - (boardWidth / 2f) + panX;
        var startY = contentRect.MidY - (boardHeight / 2f) + panY;
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
                continue;

            tilePaint.Color = ColorForCell(cell);
            tilePaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(rect, tilePaint);
            canvas.DrawRect(rect, borderPaint);

            if (hoveredPosition.HasValue && hoveredPosition.Value == cell.Position)
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

        DrawLegend(canvas, info.Width, info.Height, sliceModeText, textPaint);
    }

    public static void DrawLegend(SKCanvas canvas, int width, int height, string sliceModeText, SKPaint paint)
    {
        using var backgroundPaint = new SKPaint { Color = new SKColor(250, 251, 252, 235), Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(0, height - 30, width, height), backgroundPaint);
        paint.TextSize = 13;
        paint.Color = new SKColor(86, 101, 115);
        paint.TextAlign = SKTextAlign.Left;
        canvas.DrawText("# wall  . floor  , moss  + glass  dim/darker below = open space  ? below cutoff  ▲ ramp  H ladder  * item  letters chars", 10, height - 11, paint);
        paint.TextAlign = SKTextAlign.Right;
        canvas.DrawText(sliceModeText, width - 10, height - 11, paint);
        paint.TextAlign = SKTextAlign.Center;
    }

    public static SKColor ColorForCell(RpMapRenderCell cell)
    {
        if (cell.IsOpenSpace)
        {
            var shade = OpenSpaceShade(cell);
            return new SKColor(shade, (byte)Math.Clamp(shade + 2, 0, 255), (byte)Math.Clamp(shade + 5, 0, 255));
        }
        return ColorForGlyph(cell.Glyph);
    }

    public static SKColor OpenSpaceGlyphColor(RpMapRenderCell cell)
    {
        if (cell.IsUnderlayClipped)
            return new SKColor(72, 84, 96, 190);

        var depth = Math.Clamp(cell.UnderlayDepth, 1, 12);
        var channel = (byte)Math.Clamp(145 - (depth * 11), 55, 145);
        var alpha = (byte)Math.Clamp(180 - (depth * 8), 80, 180);
        return new SKColor(channel, (byte)Math.Clamp(channel + 8, 0, 255), (byte)Math.Clamp(channel + 16, 0, 255), alpha);
    }

    public static byte OpenSpaceShade(RpMapRenderCell cell)
    {
        if (cell.IsUnderlayClipped) return 211;
        if (cell.UnderlayDepth <= 0) return 239;
        return (byte)Math.Clamp(242 - (cell.UnderlayDepth * 9), 178, 242);
    }

    public static SKColor ColorForGlyph(string glyph)
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
}
