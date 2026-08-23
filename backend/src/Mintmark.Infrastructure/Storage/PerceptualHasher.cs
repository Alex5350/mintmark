using Mintmark.Application.Ports;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mintmark.Infrastructure.Storage;

/// <summary>
/// DCT-based perceptual hash (pHash): 32x32 grayscale → 2-D DCT-II →
/// top-left 8x8 coefficients → 64 bits of (coefficient &gt; median), MSB first
/// row-major. Deterministic and dependency-light, so identical inputs always
/// produce the identical hash and near-identical images stay within a small
/// Hamming distance (the dedupe key of the identification pipeline).
/// </summary>
public sealed class PerceptualHasher : IPerceptualHasher
{
    private const int Size = 32;
    private const int Block = 8;

    /// <inheritdoc />
    public Task<ulong> HashAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(Hash(imageBytes));
    }

    /// <summary>Computes the hash synchronously (cheap: 32x32 DCT).</summary>
    /// <param name="imageBytes">Encoded image bytes.</param>
    public static ulong Hash(byte[] imageBytes)
    {
        using var image = Image.Load<Rgba32>(imageBytes);
        image.Mutate(ctx => _ = ctx.AutoOrient().Resize(Size, Size).Grayscale());

        // Luminance matrix (Grayscale() writes the same value into R/G/B).
        var pixels = new double[Size * Size];
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < Size; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < Size; x++)
                {
                    pixels[(y * Size) + x] = row[x].R;
                }
            }
        });

        var dct = Dct2(pixels);

        // Median over the 8x8 low-frequency block (DC included — it is a
        // constant "brighter than median" bit, harmless and keeps 64 bits).
        var block = new double[Block * Block];
        for (var y = 0; y < Block; y++)
        {
            for (var x = 0; x < Block; x++)
            {
                block[(y * Block) + x] = dct[(y * Size) + x];
            }
        }

        var median = Median(block);

        ulong hash = 0;
        var bit = 63;
        for (var y = 0; y < Block; y++)
        {
            for (var x = 0; x < Block; x++)
            {
                if (dct[(y * Size) + x] > median)
                {
                    hash |= 1UL << bit;
                }

                bit--;
            }
        }

        return hash;
    }

    /// <summary>Separable 2-D DCT-II (direct O(n^4) is fine at 32x32).</summary>
    private static double[] Dct2(double[] input)
    {
        var rows = TransformRows(input);
        var result = new double[Size * Size];

        for (var x = 0; x < Size; x++)
        {
            for (var u = 0; u < Size; u++)
            {
                double sum = 0;
                for (var y = 0; y < Size; y++)
                {
                    sum += rows[(y * Size) + x]
                        * Math.Cos(((2 * y) + 1) * u * Math.PI / (2 * Size));
                }

                var scale = u == 0 ? Math.Sqrt(1.0 / Size) : Math.Sqrt(2.0 / Size);
                result[(u * Size) + x] = sum * scale;
            }
        }

        return result;
    }

    private static double[] TransformRows(double[] input)
    {
        var output = new double[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var u = 0; u < Size; u++)
            {
                double sum = 0;
                for (var x = 0; x < Size; x++)
                {
                    sum += input[(y * Size) + x]
                        * Math.Cos(((2 * x) + 1) * u * Math.PI / (2 * Size));
                }

                var scale = u == 0 ? Math.Sqrt(1.0 / Size) : Math.Sqrt(2.0 / Size);
                output[(y * Size) + u] = sum * scale;
            }
        }

        return output;
    }

    private static double Median(double[] values)
    {
        var sorted = (double[])values.Clone();
        Array.Sort(sorted);
        var middle = sorted.Length / 2;
        return (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
