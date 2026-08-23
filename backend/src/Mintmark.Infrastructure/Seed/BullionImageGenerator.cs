using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Mintmark.Infrastructure.Seed;

/// <summary>
/// Deterministic bullion-style reference images for seeded catalog rows:
/// a rendered metal disc (silver or gold) with radial sheen, raised-rim
/// highlight, reeded edge ticks, and generic legends ("ONE TROY OUNCE",
/// "· 999 FINE ·") drawn with the built-in 5x7 pixel font — no font
/// dependency. Designs are ORIGINAL and deliberately generic: no mint
/// devices, no series artwork (those are protected designs), just the
/// visual grammar of a bullion round. Obverse and reverse remain visually
/// distinct so the two sides hash perceptually apart. These exercise the
/// retrieval plumbing end-to-end; they are rendered art, not photography,
/// and are documented as such wherever surfaced.
/// </summary>
public static class BullionImageGenerator
{
    private const int Size = 512;
    private const int Scale = 4;         // glyph pixels -> image pixels
    private const int MaxCharsPerLine = 20;

    private static readonly Dictionary<char, int[]> Glyphs = BuildGlyphs();

    /// <summary>Generates the canonical rendered PNG for a coin type and side.</summary>
    public static byte[] Generate(string coinTypeName, string metal, bool obverse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coinTypeName);

        var palette = MetalPalette(metal);

        using var image = new Image<Rgba32>(Size, Size);
        var center = Size / 2;
        var discRadius = 216;
        var lightDir = ((float)-0.55, (float)-0.83); // upper-left key light

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dy = y - center;
                for (var x = 0; x < row.Length; x++)
                {
                    var dx = x - center;
                    var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                    if (distance > discRadius)
                    {
                        row[x] = new Rgba32(20, 20, 26);
                        continue;
                    }

                    // Reeded edge: alternating ticks around the rim band.
                    if (distance > discRadius - 14)
                    {
                        var angle = MathF.Atan2(dy, dx);
                        var tick = MathF.Sin(angle * 72f) > 0f;
                        row[x] = tick ? palette.RimBright : palette.RimDark;
                        continue;
                    }

                    // Radial sheen: bright toward the key light, falloff away.
                    var nx = dx / discRadius;
                    var ny = dy / discRadius;
                    var lambert = MathF.Max(0f, nx * lightDir.Item1 + ny * lightDir.Item2);
                    var rimCloseness = 1f - (distance / discRadius); // 1 at center
                    var shade = 0.35f + (0.65f * MathF.Pow(lambert, 1.6f))
                              + (0.10f * rimCloseness);
                    shade = Math.Clamp(shade, 0f, 1.25f);

                    var r = (byte)Math.Clamp(palette.Field.R * shade, 0, 255);
                    var g = (byte)Math.Clamp(palette.Field.G * shade, 0, 255);
                    var b = (byte)Math.Clamp(palette.Field.B * shade, 0, 255);
                    row[x] = new Rgba32(r, g, b);
                }
            }
        });

        // Raised inner design disc on the obverse only — the two sides must
        // hash apart. Rendered as a beveled medallion, not series artwork.
        if (obverse)
        {
            var innerRadius = 150;
            image.ProcessPixelRows(accessor =>
            {
                for (var y = Math.Max(0, center - innerRadius); y < Math.Min(accessor.Height, center + innerRadius); y++)
                {
                    var row = accessor.GetRowSpan(y);
                    var dy = y - center;
                    for (var x = Math.Max(0, center - innerRadius); x < Math.Min(row.Length, center + innerRadius); x++)
                    {
                        var dx = x - center;
                        var d = MathF.Sqrt((dx * dx) + (dy * dy));
                        if (d > innerRadius)
                        {
                            continue;
                        }

                        // Bevel: light top-left edge, shadow bottom-right.
                        var bevel = MathF.Max(0f, (-dx - dy) / innerRadius) * 0.35f
                                  + MathF.Max(0f, (dx + dy) / innerRadius) * -0.20f;
                        var baseShade = 0.85f + bevel;
                        var r = (byte)Math.Clamp(palette.Field.R * baseShade, 0, 255);
                        var g = (byte)Math.Clamp(palette.Field.G * baseShade, 0, 255);
                        var b = (byte)Math.Clamp(palette.Field.B * baseShade, 0, 255);
                        row[x] = new Rgba32(r, g, b);
                    }
                }
            });
        }

        // Legends: coin name across the middle; metal purity below; weight
        // statement on the reverse. Engraved-look: dark text with a one-pixel
        // light offset (incuse relief).
        var legend = new Rgba32(38, 36, 34);
        var highlight = new Rgba32(250, 248, 242);
        DrawText(image, coinTypeName.ToUpperInvariant(), legend, highlight, center);
        if (obverse)
        {
            DrawText(image, MetalWord(metal).ToUpperInvariant(), legend, highlight, center + 118);
        }
        else
        {
            DrawText(image, "ONE TROY OUNCE", legend, highlight, center + 88);
            DrawText(image, PurityWord(metal), legend, highlight, center + 128);
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static string MetalWord(string metal) => metal.ToLowerInvariant() switch
    {
        "gold" => "GOLD",
        "platinum" => "PLATINUM",
        "palladium" => "PALLADIUM",
        _ => "SILVER",
    };

    private static string PurityWord(string metal) => metal.ToLowerInvariant() switch
    {
        "gold" => "- 9999 FINE -",
        _ => "- 999 FINE -",
    };

    private readonly record struct Palette(Rgba32 Field, Rgba32 RimBright, Rgba32 RimDark);

    private static Palette MetalPalette(string metal) => metal.ToLowerInvariant() switch
    {
        "gold" => new Palette(
            Field: new Rgba32(196, 158, 74),
            RimBright: new Rgba32(232, 198, 116),
            RimDark: new Rgba32(120, 92, 40)),
        "platinum" => new Palette(
            Field: new Rgba32(178, 184, 194),
            RimBright: new Rgba32(226, 230, 238),
            RimDark: new Rgba32(112, 118, 128)),
        "palladium" => new Palette(
            Field: new Rgba32(160, 172, 186),
            RimBright: new Rgba32(212, 220, 230),
            RimDark: new Rgba32(98, 108, 122)),
        _ => new Palette(
            Field: new Rgba32(158, 166, 176),
            RimBright: new Rgba32(214, 220, 228),
            RimDark: new Rgba32(94, 100, 110)),
    };

    private static void DrawText(Image<Rgba32> image, string text, Rgba32 color, Rgba32 offset, int centerYPx)
    {
        var lines = Wrap(text);
        var lineHeight = 7 * Scale + 6;
        var startY = centerYPx - (lines.Count * lineHeight / 2) + 40;
        for (var i = 0; i < lines.Count; i++)
        {
            DrawLine(image, lines[i], color, offset, startY + (i * lineHeight));
        }
    }

    private static List<string> Wrap(string text)
    {
        var result = new List<string>();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = result.Count > 0 ? result[^1] + " " + word : word;
            if (candidate.Length <= MaxCharsPerLine)
            {
                if (result.Count > 0)
                {
                    result[^1] = candidate;
                }
                else
                {
                    result.Add(candidate);
                }
            }
            else
            {
                result.Add(word);
            }
        }

        return result;
    }

    private static void DrawLine(Image<Rgba32> image, string line, Rgba32 color, Rgba32 offset, int top)
    {
        var width = line.Length * (6 * Scale);
        var left = (Size - width) / 2;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == ' ')
            {
                continue;
            }

            if (!Glyphs.TryGetValue(char.ToUpperInvariant(ch), out var glyph))
            {
                continue;
            }

            for (var gy = 0; gy < 7; gy++)
            {
                for (var gx = 0; gx < 5; gx++)
                {
                    if ((glyph[gy] & (1 << (4 - gx))) == 0)
                    {
                        continue;
                    }

                    for (var sy = 0; sy < Scale; sy++)
                    {
                        for (var sx = 0; sx < Scale; sx++)
                        {
                            var px = left + (i * 6 * Scale) + (gx * Scale) + sx;
                            var py = top + (gy * Scale) + sy;
                            if (px >= 0 && px < Size && py >= 0 && py < Size)
                            {
                                image[px, py] = color;
                            }
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<char, int[]> BuildGlyphs() => new()
    {
        ['A'] = [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['B'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        ['C'] = [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
        ['D'] = [0b11110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b11110],
        ['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        ['F'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        ['G'] = [0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01110],
        ['H'] = [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['I'] = [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b11111],
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
        ['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        ['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['2'] = [0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111],
        ['3'] = [0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110],
        ['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        ['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
        ['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        ['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        ['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        ['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
        ['-'] = [0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000],
        ['.'] = [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b01100, 0b01100],
    };
}
