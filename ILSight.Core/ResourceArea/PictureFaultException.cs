namespace ILSight.Core.ResourceArea;

/// <summary>Bad data one of ILSight's own picture decoders found: <see cref="Fault"/> is the cause the app words, the
/// message its English diagnostic, for the record.</summary>
public sealed class PictureFaultException(PictureFault fault) : Exception(Diagnose(fault))
{
    public PictureFault Fault { get; } = fault;

    private static string Diagnose(PictureFault fault)
    {
        return fault.Match(
            static c => $"The {c.Format} is cut short.",
            static p => $"The {p.Format}'s pixels are cut short.",
            static s => $"A {s.Width}×{s.Height} picture is out of range.",
            static _ => "A palette index is out of range.",
            static _ => "Compressed BMPs are not supported.",
            static b => $"{b.Bits}-bit BMPs are not supported.",
            static n => $"The {n.Format} holds no picture.",
            static _ => "The GIF's code size is out of range.",
            static c => $"Unknown PNG color type {c.ColorType}.",
            static _ => "This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture.",
            static l => $"The picture cannot be read: {l.Message}");
    }
}
