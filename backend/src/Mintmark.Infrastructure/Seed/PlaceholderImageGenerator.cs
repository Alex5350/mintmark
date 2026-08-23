using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Mintmark.Infrastructure.Seed;

/// <summary>
/// Deterministic placeholder reference images for seeded catalog rows: a
/// dark disc with the coin-type name rendered in a tiny built-in 5x7 pixel
/// font (no font package dependency), obverse and reverse visually distinct.
/// These exist so the retrieval plumbing (pHash matching, offline evaluator,
/// presigned URLs) is exercisable end-to-end; they are placeholders, not
/// real coin imagery, and are documented as such wherever surfaced.
/// </summary>
public static class PlaceholderImageGenerator
{
    private const int Size = 512;
    private const int Scale = 4;         // glyph pixels -> image pixels
    private const int MaxCharsPerLine = 20;

    private static readonly Dictionary<char, int[]> Glyphs = BuildGlyphs();

    /// <summary>Generates the canonical placeholder PNG for a coin type and side.</summary>
    public static byte[] Generate(string coinTypeName, bool obverse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coinTypeName);

        using var image = new Image<Rgba32>(Size, Size);
        var background = new Rgba32(24, 24, 30);
        var disc = new Rgba32(58, 58, 70);
        var ring = new Rgba32(96, 96, 112);
        var text = new Rgba32(232, 232, 236);

        var center = Size / 2;
        var discRadius = 216;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dy = y - center;
                for (var x = 0; x < row.Length; x++)
                {
                    var dx = x - center;
                    var distance = Math.Sqrt((dx * dx) + (dy * dy));
                    row[x] = distance > discRadius
                        ? background
                        : distance > discRadius - 8
                            ? ring
                            : disc;
                }
            }
        });

        // Obverse carries an inner disc; the reverse keeps a plain field, so
        // the two sides hash perceptually apart.
        if (obverse)
        {
            var innerRadius = 150;
            var inner = new Rgba32(74, 74, 88);
            image.ProcessPixelRows(accessor =>
            {
                for (var y = Math.Max(0, center - innerRadius); y < Math.Min(accessor.Height, center + innerRadius); y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var dy = y - center;
                    for (var x = Math.Max(0, center - innerRadius); x < Math.Min(row.Length, center + innerRadius); x++)
                    {
                        var dx = x - center;
                        if ((dx * dx) + (dy * dy) <= innerRadius * innerRadius)
                        {
                            row[x] = inner;
                        }
                    }
                }
            });
        }

        DrawText(image, coinTypeName.ToUpperInvariant(), text, center);
        DrawText(image, obverse ? "OBV" : "REV", ring, center + 170);

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static void DrawText(Image<Rgba32> image, string text, Rgba32 color, int centerYPx)
    {
        var lines = Wrap(text);
        var lineHeight = (7 * Scale) + (2 * Scale);
        var startY = centerYPx - ((lines.Count * lineHeight) / 2);

        for (var i = 0; i < lines.Count; i++)
        {
            DrawLine(image, lines[i], color, startY + (i * lineHeight));
        }
    }

    private static void DrawLine(Image<Rgba32> image, string line, Rgba32 color, int top)
    {
        var width = line.Length * 6 * Scale;
        var left = (Size - width) / 2;

        image.ProcessPixelRows(accessor =>
        {
            for (var c = 0; c < line.Length; c++)
            {
                if (!Glyphs.TryGetValue(line[c], out var glyph))
                {
                    continue;
                }

                for (var gy = 0; gy < 7; gy++)
                {
                    var bits = glyph[gy];
                    var rowY = top + (gy * Scale);
                    for (var gx = 0; gx < 5; gx++)
                    {
                        if ((bits & (1 << (4 - gx))) == 0)
                        {
                            continue;
                        }

                        for (var py = 0; py < Scale; py++)
                        {
                            if (rowY + py < 0 || rowY + py >= accessor.Height)
                            {
                                continue;
                            }

                            var row = accessor.GetRowSpan(rowY + py);
                            for (var px = 0; px < Scale; px++)
                            {
                                var x = left + (c * 6 * Scale) + (gx * Scale) + px;
                                if (x >= 0 && x < row.Length)
                                {
                                    row[x] = color;
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    private static List<string> Wrap(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (candidate.Length > MaxCharsPerLine && current.Length > 0)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        // Placeholder: keep at most four lines.
        return lines.Take(4).ToList();
    }

    private static Dictionary<char, int[]> BuildGlyphs() => new()
    {
        [' '] = [0, 0, 0, 0, 0, 0, 0],
        ['-'] = [0, 0, 0, 0b11111, 0, 0, 0],
        ['.'] = [0, 0, 0, 0, 0, 0b01100, 0b01100],
        ['('] = [0b00010, 0b00100, 0b01000, 0b01000, 0b01000, 0b00100, 0b00010],
        [')'] = [0b01000, 0b00100, 0b00010, 0b00010, 0b00010, 0b00100, 0b01000],
        ['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        ['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['2'] = [0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111],
        ['3'] = [0b01110, 0b10001, 0b00001, 0b00110, 0b00001, 0b10001, 0b01110],
        ['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        ['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
        ['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        ['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        ['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        ['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
        ['A'] = [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['B'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        ['C'] = [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
        ['D'] = [0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110],
        ['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        ['F'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        ['G'] = [0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111],
        ['H'] = [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['I'] = [0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['J'] = [0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100],
        ['K'] = [0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001],
        ['L'] = [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
        ['M'] = [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        ['N'] = [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
        ['O'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['P'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
        ['Q'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
        ['R'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
        ['S'] = [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
        ['T'] = [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        ['U'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['V'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100],
        ['W'] = [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001],
        ['X'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001],
        ['Y'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100],
        ['Z'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
    };
}
