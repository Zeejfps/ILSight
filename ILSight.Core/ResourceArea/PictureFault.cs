using System.Diagnostics;

namespace ILSight.Core.ResourceArea;

/// <summary>Why a picture can't be read, as a cause the app words in each language: one of ILSight's own decoders'
/// findings, or a listed decoder's failure with its own message. Causes that share a message are kept apart here.</summary>
public abstract record PictureFault
{
    private PictureFault()
    {
    }

    /// <summary>The bytes end before what <see cref="Format"/> says is in them.</summary>
    public sealed record CutShort(string Format) : PictureFault;

    /// <summary>The decoded pixels are fewer than the picture's size needs.</summary>
    public sealed record PixelsCutShort(string Format) : PictureFault;

    /// <summary>A size no picture this shows has: none, negative, or past what an array of its pixels holds.</summary>
    public sealed record SizeOutOfRange : PictureFault
    {
        public required long Width { get; init; }

        public required long Height { get; init; }
    }

    public sealed record PaletteIndexOutOfRange : PictureFault;

    /// <summary>A BMP compressed in a way ILSight's decoder doesn't read (run-length, JPEG or PNG inside).</summary>
    public sealed record CompressionNotRead : PictureFault;

    /// <summary>A BMP whose bits per pixel ILSight's decoder doesn't read.</summary>
    public sealed record BitDepthNotRead(int Bits) : PictureFault;

    /// <summary>A GIF with no frame, or an icon or cursor file with no entry.</summary>
    public sealed record NoPicture(string Format) : PictureFault;

    /// <summary>A GIF frame's LZW code size past the widest its code table holds.</summary>
    public sealed record CodeSizeOutOfRange : PictureFault;

    /// <summary>A PNG color type the PNG specification doesn't define, as PngSharp names it.</summary>
    public sealed record ColorTypeUnknown(string ColorType) : PictureFault;

    /// <summary>Bytes in none of the formats the decoders read.</summary>
    public sealed record NotAPicture : PictureFault;

    /// <summary>A listed library failed reading the picture or its bytes; <see cref="Message"/> is its own, as it
    /// wrote it.</summary>
    public sealed record LibraryFailure(string Message) : PictureFault;

    public TResult Match<TResult>(Func<CutShort, TResult> cutShort, Func<PixelsCutShort, TResult> pixelsCutShort,
        Func<SizeOutOfRange, TResult> sizeOutOfRange, Func<PaletteIndexOutOfRange, TResult> paletteIndexOutOfRange,
        Func<CompressionNotRead, TResult> compressionNotRead, Func<BitDepthNotRead, TResult> bitDepthNotRead,
        Func<NoPicture, TResult> noPicture, Func<CodeSizeOutOfRange, TResult> codeSizeOutOfRange,
        Func<ColorTypeUnknown, TResult> colorTypeUnknown, Func<NotAPicture, TResult> notAPicture,
        Func<LibraryFailure, TResult> libraryFailure)
    {
        return this switch
        {
            CutShort c => cutShort(c),
            PixelsCutShort p => pixelsCutShort(p),
            SizeOutOfRange s => sizeOutOfRange(s),
            PaletteIndexOutOfRange p => paletteIndexOutOfRange(p),
            CompressionNotRead c => compressionNotRead(c),
            BitDepthNotRead b => bitDepthNotRead(b),
            NoPicture n => noPicture(n),
            CodeSizeOutOfRange c => codeSizeOutOfRange(c),
            ColorTypeUnknown c => colorTypeUnknown(c),
            NotAPicture n => notAPicture(n),
            LibraryFailure l => libraryFailure(l),
            _ => throw new UnreachableException($"PictureFault.Match does not handle {GetType().Name}."),
        };
    }
}
