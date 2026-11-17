namespace ILSight.Core.ResourceArea;

/// <summary>Why a picture can't be read, as a cause the app words in each language: one of ILSight's own decoders'
/// findings, or a listed decoder's failure with its own message. Causes that share a message are kept apart here.</summary>
public union PictureFault(PictureFault.CutShort, PictureFault.PixelsCutShort, PictureFault.SizeOutOfRange,
    PictureFault.PaletteIndexOutOfRange, PictureFault.CompressionNotRead, PictureFault.BitDepthNotRead, PictureFault.NoPicture,
    PictureFault.CodeSizeOutOfRange, PictureFault.ColorTypeUnknown, PictureFault.NotAPicture, PictureFault.LibraryFailure)
{
    /// <summary>The bytes end before what <see cref="Format"/> says is in them.</summary>
    public sealed record CutShort(string Format);

    /// <summary>The decoded pixels are fewer than the picture's size needs.</summary>
    public sealed record PixelsCutShort(string Format);

    /// <summary>A size no picture this shows has: none, negative, or past what an array of its pixels holds.</summary>
    public sealed record SizeOutOfRange
    {
        public required long Width { get; init; }

        public required long Height { get; init; }
    }

    public sealed record PaletteIndexOutOfRange;

    /// <summary>A BMP compressed in a way ILSight's decoder doesn't read (run-length, JPEG or PNG inside).</summary>
    public sealed record CompressionNotRead;

    /// <summary>A BMP whose bits per pixel ILSight's decoder doesn't read.</summary>
    public sealed record BitDepthNotRead(int Bits);

    /// <summary>A GIF with no frame, or an icon or cursor file with no entry.</summary>
    public sealed record NoPicture(string Format);

    /// <summary>A GIF frame's LZW code size past the widest its code table holds.</summary>
    public sealed record CodeSizeOutOfRange;

    /// <summary>A PNG color type the PNG specification doesn't define, as PngSharp names it.</summary>
    public sealed record ColorTypeUnknown(string ColorType);

    /// <summary>Bytes in none of the formats the decoders read.</summary>
    public sealed record NotAPicture;

    /// <summary>A listed library failed reading the picture or its bytes; <see cref="Message"/> is its own, as it
    /// wrote it.</summary>
    public sealed record LibraryFailure(string Message);
}
