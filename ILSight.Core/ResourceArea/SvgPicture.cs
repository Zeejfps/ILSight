using System.Text;
using ZGF.Svg;

namespace ILSight.Core.ResourceArea;

/// <summary>An SVG resource drawn to pixels by ZGF.Svg: at its own size first, shrunk to fit
/// <see cref="MaxSide"/> when larger.</summary>
internal static class SvgPicture
{
    private const string Format = "SVG";
    private const string SvgElement = "<svg";
    private const int MaxSide = 2048;
    private const int FallbackSide = 256;

    /// <summary>Whether <paramref name="data"/> is markup with an <c>svg</c> element.</summary>
    public static bool IsSvg(byte[] data)
    {
        var text = ReadText(data);
        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith('<'))
        {
            return false;
        }

        return trimmed.Contains(SvgElement, StringComparison.Ordinal);
    }

    /// <summary>The markup in <paramref name="data"/> parsed by ZGF.Svg, with its own size as the document gives
    /// it: the library's read, for the caller's catch.</summary>
    public static ParsedSvg Parse(byte[] data)
    {
        var document = SvgDocument.Parse(ReadText(data));
        return new ParsedSvg { Document = document, Width = document.IntrinsicWidth, Height = document.IntrinsicHeight };
    }

    /// <summary><paramref name="document"/> drawn <paramref name="width"/> by
    /// <paramref name="height"/>, shrunk evenly to fit <see cref="MaxSide"/>.</summary>
    public static ResourceImage Rasterize(SvgDocument document, float width, float height)
    {
        var (widthPx, heightPx) = PixelsFor((width, height));
        return Picture(document, widthPx, heightPx, document.Rasterize(widthPx, heightPx));
    }

    /// <summary>The whole pixels a picture of <paramref name="size"/> is drawn at, shrunk evenly to fit
    /// <see cref="MaxSide"/>.</summary>
    public static (int Width, int Height) PixelsFor((float Width, float Height) size)
    {
        var scale = MathF.Min(1f, MaxSide / MathF.Max(size.Width, size.Height));
        return (WholePixels(size.Width * scale), WholePixels(size.Height * scale));
    }

    /// <summary>The picture of <paramref name="document"/> drawn <paramref name="width"/> by <paramref name="height"/>
    /// into <paramref name="rgba"/>.</summary>
    public static ResourceImage Picture(SvgDocument document, int width, int height, byte[] rgba)
    {
        return new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = Format, Shapes = document };
    }

    /// <summary>The size <paramref name="parsed"/> is drawn at first: its own, or a square of
    /// <see cref="FallbackSide"/> when it gives none.</summary>
    public static (float Width, float Height) OwnSize(ParsedSvg parsed)
    {
        if (parsed.Width <= 0f || parsed.Height <= 0f)
        {
            return (FallbackSide, FallbackSide);
        }

        return (parsed.Width, parsed.Height);
    }

    private static string ReadText(byte[] data)
    {
        using var reader = new StreamReader(new MemoryStream(data), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static int WholePixels(float size)
    {
        return Math.Max(1, (int)MathF.Ceiling(size));
    }
}
