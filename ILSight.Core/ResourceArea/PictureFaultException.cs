namespace ILSight.Core.ResourceArea;

/// <summary>Bad data one of ILSight's own picture decoders found: <see cref="Fault"/> is the cause the app words, the
/// message its English diagnostic, for the record.</summary>
public sealed class PictureFaultException(PictureFault fault) : Exception(Diagnose(fault))
{
    public PictureFault Fault { get; } = fault;

    private static string Diagnose(PictureFault fault)
    {
        return fault switch
        {
            PictureFault.CutShort c => $"The {c.Format} is cut short.",
            PictureFault.PixelsCutShort p => $"The {p.Format}'s pixels are cut short.",
            PictureFault.SizeOutOfRange s => $"A {s.Width}×{s.Height} picture is out of range.",
            PictureFault.PaletteIndexOutOfRange => "A palette index is out of range.",
            PictureFault.CompressionNotRead => "Compressed BMPs are not supported.",
            PictureFault.BitDepthNotRead b => $"{b.Bits}-bit BMPs are not supported.",
            PictureFault.NoPicture n => $"The {n.Format} holds no picture.",
            PictureFault.CodeSizeOutOfRange => "The GIF's code size is out of range.",
            PictureFault.ColorTypeUnknown c => $"Unknown PNG color type {c.ColorType}.",
            PictureFault.NotAPicture => "This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture.",
            PictureFault.LibraryFailure l => $"The picture cannot be read: {l.Message}",
        };
    }
}
