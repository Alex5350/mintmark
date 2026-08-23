using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Mintmark.Infrastructure.Storage;

/// <summary>
/// Server-side image normalization applied to every stored photo (ADR 0006):
/// auto-orient, strip metadata, cap at 1600 px on the long edge, re-encode
/// JPEG quality 82. Re-encoding is what strips EXIF/GPS — nothing survives a
/// decode/encode round-trip.
/// </summary>
public static class ImageReencoder
{
    /// <summary>Maximum stored dimension on the long edge.</summary>
    public const int MaxDimension = 1600;

    /// <summary>JPEG encoder quality for stored photos.</summary>
    public const int JpegQuality = 82;

    /// <summary>Re-encodes image bytes to the canonical storage form and returns the JPEG bytes.</summary>
    /// <exception cref="SixLabors.ImageSharp.UnknownImageFormatException">Thrown when the bytes are not a decodable image.</exception>
    public static byte[] Reencode(Stream input)
    {
        using var image = Image.Load(input);
        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.XmpProfile = null;

        image.Mutate(ctx =>
        {
            _ = ctx.AutoOrient();
            if (image.Width > MaxDimension || image.Height > MaxDimension)
            {
                _ = ctx.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(MaxDimension, MaxDimension) });
            }
        });

        using var output = new MemoryStream();
        image.SaveAsJpeg(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = JpegQuality });
        return output.ToArray();
    }
}
