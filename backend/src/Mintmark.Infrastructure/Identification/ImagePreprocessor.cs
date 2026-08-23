using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Mintmark.Infrastructure.Identification;

/// <summary>
/// Canonical normalization for identification inputs: center-crop to square,
/// resize to 512x512, mask to a circle (coins are round; corners are
/// background), emit PNG. Deskew is deliberately skipped: single-image
 /// rotation estimation is unreliable for round subjects and a wrong deskew
/// corrupts the perceptual hash — the pipeline tolerates small rotations via
/// Hamming distance instead. EXIF/GPS never survive the decode/encode
/// round-trip.
/// </summary>
public static class ImagePreprocessor
{
    /// <summary>Canonical edge length in pixels.</summary>
    public const int CanonicalSize = 512;

    /// <summary>Preprocesses image bytes into the canonical 512x512 circular PNG.</summary>
    /// <exception cref="SixLabors.ImageSharp.UnknownImageFormatException">Thrown when the bytes are not a decodable image.</exception>
    public static byte[] Preprocess(byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var image = Image.Load<Rgba32>(source);
        image.Mutate(ctx => _ = ctx.AutoOrient());

        var side = Math.Min(image.Width, image.Height);
        var offsetX = (image.Width - side) / 2;
        var offsetY = (image.Height - side) / 2;
        image.Mutate(ctx => _ = ctx.Crop(new Rectangle(offsetX, offsetY, side, side)));
        image.Mutate(ctx => _ = ctx.Resize(CanonicalSize, CanonicalSize));

        ApplyCircularMask(image);

        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return output.ToArray();
    }

    private static void ApplyCircularMask(Image<Rgba32> image)
    {
        var radius = CanonicalSize / 2.0;
        var center = (CanonicalSize - 1) / 2.0;
        var limit = radius * radius;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dy = y - center;
                for (var x = 0; x < row.Length; x++)
                {
                    var dx = x - center;
                    if ((dx * dx) + (dy * dy) > limit)
                    {
                        row[x] = default;
                    }
                }
            }
        });
    }
}
