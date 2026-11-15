using System.Buffers.Binary;
using PngSharp.Spec.Chunks.IHDR;

namespace ILSight.Core.Resources;

/// <summary>Decoding pictures into pixels, and drawing them at other sizes.</summary>
public static class ResourceImages
{
    /// <summary>The picture at <paramref name="scale"/> times its size: drawn again from its shapes
    /// when it has them, so it stays sharp; else this picture, to be stretched.</summary>
    public static ResourceImage DrawAt(ResourceImage image, float scale)
    {
        if (image.Shapes is null)
        {
            return image;
        }

        return SvgPicture.Rasterize(image.Shapes, image.Width * scale, image.Height * scale);
    }

    /// <summary>The picture shrunk to fit a square <paramref name="side"/> pixels across, keeping its
    /// aspect: drawn again from its shapes when it has them, else each pixel the average of those it
    /// covers. A picture that fits already is itself.</summary>
    public static ResourceImage FitInto(ResourceImage image, int side)
    {
        var longest = Math.Max(image.Width, image.Height);
        if (longest <= side)
        {
            return image;
        }

        var scale = (float)side / longest;
        if (image.Shapes is not null)
        {
            return DrawAt(image, scale);
        }

        var width = Math.Max(1, (int)MathF.Round(image.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(image.Height * scale));
        return new ResourceImage { Width = width, Height = height, Rgba = AverageOver(image, width, height), Format = image.Format };
    }

    private static byte[] AverageOver(ResourceImage image, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                AverageOverCell(image, rgba, x, y, width, height);
            }
        }
        return rgba;
    }

    // Colors are weighed by their alpha, so clear pixels lend no color.
    private static void AverageOverCell(ResourceImage image, byte[] target, int x, int y, int width, int height)
    {
        var left = x * image.Width / width;
        var right = Math.Max(left + 1, (x + 1) * image.Width / width);
        var top = y * image.Height / height;
        var bottom = Math.Max(top + 1, (y + 1) * image.Height / height);
        long red = 0, green = 0, blue = 0, alpha = 0;
        for (var sy = top; sy < bottom; sy++)
        {
            for (var sx = left; sx < right; sx++)
            {
                var at = (sy * image.Width + sx) * 4;
                var a = image.Rgba[at + 3];
                red += image.Rgba[at] * a;
                green += image.Rgba[at + 1] * a;
                blue += image.Rgba[at + 2] * a;
                alpha += a;
            }
        }
        var to = (y * width + x) * 4;
        var count = (right - left) * (bottom - top);
        target[to + 3] = (byte)(alpha / count);
        if (alpha == 0)
        {
            return;
        }

        target[to] = (byte)(red / alpha);
        target[to + 1] = (byte)(green / alpha);
        target[to + 2] = (byte)(blue / alpha);
    }

    /// <summary>Whether the picture is dark marks on a see-through background, which a dark backdrop
    /// would hide: a good share of it clear, and what shows mostly dark.</summary>
    public static bool IsDarkOnClear(ResourceImage image)
    {
        long shown = 0;
        long clear = 0;
        var luminance = 0.0;
        for (var at = 0; at < image.Rgba.Length; at += 4)
        {
            if (image.Rgba[at + 3] < ClearAlpha)
            {
                clear++;
                continue;
            }
            shown++;
            luminance += GetLuminanceAt(image, at);
        }
        if (shown == 0)
        {
            return false;
        }

        var clearShare = (double)clear / (clear + shown);
        var meanLuminance = luminance / shown;
        return clearShare >= MinClearShare && meanLuminance <= MaxDarkLuminance;
    }

    private const byte ClearAlpha = 32;
    private const double MinClearShare = 0.2;
    private const double MaxDarkLuminance = 0.3;

    private static double GetLuminanceAt(ResourceImage image, int at)
    {
        return (0.2126 * image.Rgba[at] + 0.7152 * image.Rgba[at + 1] + 0.0722 * image.Rgba[at + 2]) / 255.0;
    }

    /// <summary>The formats ILSpy shows as pictures, by the file extension of the resource name.</summary>
    internal static bool IsImageName(string name)
    {
        return Path.GetExtension(name).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".ico" or ".cur" or ".svg";
    }

    /// <summary>The picture in <paramref name="data"/>, recognized by its content rather than its
    /// name; <see cref="ResourceImageRead.Unreadable"/> when it is no picture this can decode, or one the decoders fail
    /// on. A listed decoder (PngSharp, JpegSharp, ZGF.Svg) failing on the bytes, whatever it throws, is the picture's
    /// failure; ILSight's own decoders (BMP, GIF, ICO and CUR, and what it makes of a listed decoder's pixels) fail on
    /// bad bytes with an <see cref="InvalidDataException"/> alone, and anything else they throw is a bug, for the
    /// caller's edge.</summary>
    internal static ResourceImageRead DecodeImage(byte[] data)
    {
        ReadOnlySpan<byte> span = data;
        if (span.StartsWith(PngSignature))
        {
            return DecodePng(data, "PNG");
        }

        if (span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            return DecodeJpeg(data);
        }

        if (span.StartsWith("BM"u8))
        {
            return DecodeBitmap(data);
        }

        if (span.StartsWith("GIF8"u8))
        {
            return DecodeGif(data);
        }

        if (IsIcon(span))
        {
            return DecodeIcon(data);
        }

        if (SvgPicture.IsSvg(data))
        {
            return DecodeSvg(data);
        }

        return new ResourceImageRead.Unreadable { Why = "This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture." };
    }

    private static ReadOnlySpan<byte> PngSignature => [0x89, (byte)'P', (byte)'N', (byte)'G'];

    // Why a listed decoder failed on the bytes: what it says of bad data as it says it, else what it threw, after
    // "cannot be read".
    private static ResourceImageRead.Unreadable CannotBeRead(Exception e)
    {
        if (e is InvalidDataException)
        {
            return new ResourceImageRead.Unreadable { Why = e.Message };
        }

        return new ResourceImageRead.Unreadable { Why = $"The picture cannot be read: {e.Message}" };
    }

    // Why one of ILSight's own decoders failed on the bytes.
    private static ResourceImageRead.Unreadable BadData(InvalidDataException e)
    {
        return new ResourceImageRead.Unreadable { Why = e.Message };
    }

    // A PNG's facts read by PngSharp (a listed library) inside its catch; its pixels made into RGBA by ILSight after it.
    private static ResourceImageRead DecodePng(byte[] data, string format)
    {
        PngRead read;
        try
        {
            var png = PngSharp.Api.Png.DecodeFromByteArray(data);
            var ihdr = png.Ihdr;
            read = new PngRead
            {
                Width = (int)ihdr.Width,
                Height = (int)ihdr.Height,
                Depth = ihdr.BitDepth,
                ColorType = ihdr.ColorType,
                BytesPerPixel = ihdr.GetBytesPerPixel(),
                Pixels = png.PixelData,
                Palette = png.Plte?.Entries,
                Transparency = png.Trns?.Data,
            };
        }
        catch (Exception e) when (e is not OperationCanceledException && e is not OutOfMemoryException)
        {
            return CannotBeRead(e);
        }

        try
        {
            return new ResourceImageRead.Decoded { Image = MakePng(read, format) };
        }
        catch (InvalidDataException e)
        {
            return BadData(e);
        }
    }

    private static ResourceImage MakePng(PngRead read, string format)
    {
        int width = read.Width, height = read.Height;
        if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue / 4)
        {
            throw new InvalidDataException($"A {width}×{height} picture is out of range.");
        }

        var depth = read.Depth;
        var src = read.Pixels;
        var bytesPerPixel = read.BytesPerPixel;
        var wide = depth == 16;
        var palette = read.Palette;
        var transparency = read.Transparency;
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var at = i * bytesPerPixel;
            if (at + bytesPerPixel > src.Length)
            {
                throw new InvalidDataException($"The {format}'s pixels are cut short.");
            }

            byte r, g, b, a = 255;
            switch (read.ColorType)
            {
                case ColorType.Grayscale:
                    r = g = b = Sample(src, at, depth);
                    if (IsTransparentGray(src, at, wide, transparency))
                    {
                        a = 0;
                    }

                    break;
                case ColorType.GrayscaleWithAlpha:
                    r = g = b = Sample(src, at, depth);
                    a = Sample(src, at + (wide ? 2 : 1), depth);
                    break;
                case ColorType.TrueColor:
                    r = Sample(src, at, depth);
                    g = Sample(src, at + (wide ? 2 : 1), depth);
                    b = Sample(src, at + (wide ? 4 : 2), depth);
                    if (IsTransparentColor(src, at, wide, transparency))
                    {
                        a = 0;
                    }

                    break;
                case ColorType.TrueColorWithAlpha:
                    r = Sample(src, at, depth);
                    g = Sample(src, at + (wide ? 2 : 1), depth);
                    b = Sample(src, at + (wide ? 4 : 2), depth);
                    a = Sample(src, at + (wide ? 6 : 3), depth);
                    break;
                case ColorType.IndexedColor:
                    var index = src[at];
                    if (palette is null || index * 3 + 2 >= palette.Length)
                    {
                        throw new InvalidDataException("A palette index is out of range.");
                    }

                    (r, g, b) = (palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2]);
                    if (transparency is not null && index < transparency.Length)
                    {
                        a = transparency[index];
                    }

                    break;
                default:
                    throw new InvalidDataException($"Unknown PNG color type {read.ColorType}.");
            }
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = a;
        }
        return new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = format };

        // Whether the gray sample at src[at] is the one tRNS makes transparent.
        static bool IsTransparentGray(byte[] src, int at, bool wide, byte[]? transparency)
        {
            if (transparency is null || transparency.Length < 2)
            {
                return false;
            }

            var gray = Raw(src, at, wide);
            return gray == BinaryPrimitives.ReadUInt16BigEndian(transparency);
        }

        // Whether the RGB samples at src[at] are the color tRNS makes transparent.
        static bool IsTransparentColor(byte[] src, int at, bool wide, byte[]? transparency)
        {
            if (transparency is null || transparency.Length < 6)
            {
                return false;
            }

            var red = Raw(src, at, wide);
            var green = Raw(src, at + (wide ? 2 : 1), wide);
            var blue = Raw(src, at + (wide ? 4 : 2), wide);
            return red == BinaryPrimitives.ReadUInt16BigEndian(transparency)
                && green == BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2))
                && blue == BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4));
        }

        static int Raw(byte[] src, int at, bool wide)
        {
            return wide ? src[at] << 8 | src[at + 1] : src[at];
        }

        static byte Sample(byte[] src, int at, int depth)
        {
            return depth switch
            {
                16 => src[at],
                8 => src[at],
                _ => (byte)(src[at] * 255 / ((1 << depth) - 1)),
            };
        }
    }

    // A JPEG's size and pixels read by JpegSharp (a listed library) inside its catch; laid out as RGBA after it.
    private static ResourceImageRead DecodeJpeg(byte[] data)
    {
        int width, height;
        int[] pixels;
        try
        {
            var jpeg = JpegSharp.Api.Jpeg.DecodeAnyPrecision(data);
            (width, height, pixels) = (jpeg.Width, jpeg.Height, jpeg.ToRgba8888());
        }
        catch (Exception e) when (e is not OperationCanceledException && e is not OutOfMemoryException)
        {
            return CannotBeRead(e);
        }

        var rgba = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(rgba.AsSpan(i * 4), pixels[i]);
        }

        return new ResourceImageRead.Decoded { Image = new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = "JPEG" } };
    }

    // An SVG parsed and drawn by ZGF.Svg (a listed library), each under its catch; the size it is drawn at is ILSight's,
    // worked out between them.
    private static ResourceImageRead DecodeSvg(byte[] data)
    {
        ParsedSvg parsed;
        try
        {
            parsed = SvgPicture.Parse(data);
        }
        catch (Exception e) when (e is not OperationCanceledException && e is not OutOfMemoryException)
        {
            return CannotBeRead(e);
        }

        var (width, height) = SvgPicture.PixelsFor(SvgPicture.OwnSize(parsed));
        byte[] rgba;
        try
        {
            rgba = parsed.Document.Rasterize(width, height);
        }
        catch (Exception e) when (e is not OperationCanceledException && e is not OutOfMemoryException)
        {
            return CannotBeRead(e);
        }

        return new ResourceImageRead.Decoded { Image = SvgPicture.Picture(parsed.Document, width, height, rgba) };
    }

    private static ResourceImageRead DecodeBitmap(byte[] data)
    {
        try
        {
            var bytes = new PictureBytes(data, "BMP");
            var pixels = bytes.Int32(10);
            return new ResourceImageRead.Decoded { Image = DecodeDib(bytes.From(14), "BMP", isIcon: false, pixelOffset: pixels - 14L) };
        }
        catch (InvalidDataException e)
        {
            return BadData(e);
        }
    }

    private static ResourceImageRead DecodeGif(byte[] data)
    {
        try
        {
            return new ResourceImageRead.Decoded { Image = Gif.Decode(new PictureBytes(data, "GIF")) };
        }
        catch (InvalidDataException e)
        {
            return BadData(e);
        }
    }

    // An icon or a cursor file starts with a reserved zero, then type 1 (icon) or 2 (cursor), with the count after it.
    private static bool IsIcon(ReadOnlySpan<byte> span)
    {
        return span.Length >= 6 && span[0] == 0 && span[1] == 0 && span[2] is 1 or 2;
    }

    // The largest of the icon's pictures: a PNG one decoded by PngSharp, as a PNG is; any other ILSight's own.
    private static ResourceImageRead DecodeIcon(byte[] data)
    {
        var format = data[2] == 2 ? "CUR" : "ICO";
        byte[] payload;
        try
        {
            payload = ChooseIconPicture(new PictureBytes(data, format), format);
        }
        catch (InvalidDataException e)
        {
            return BadData(e);
        }

        ReadOnlySpan<byte> picture = payload;
        if (picture.StartsWith(PngSignature))
        {
            return DecodePng(payload, format);
        }

        try
        {
            return new ResourceImageRead.Decoded { Image = DecodeDib(new PictureBytes(payload, format), format, isIcon: true) };
        }
        catch (InvalidDataException e)
        {
            return BadData(e);
        }
    }

    private static byte[] ChooseIconPicture(PictureBytes data, string format)
    {
        var count = data.UInt16(4);
        var best = -1;
        var bestArea = -1;
        for (var i = 0; i < count; i++)
        {
            var entry = data.Part(6 + i * 16L, 16);
            var area = (entry[0] == 0 ? 256 : entry[0]) * (entry[1] == 0 ? 256 : entry[1]);
            if (area > bestArea)
            {
                (best, bestArea) = (i, area);
            }
        }
        if (best < 0)
        {
            throw new InvalidDataException($"The {format} holds no pictures.");
        }

        var chosen = data.Part(6 + best * 16L, 16);
        var size = chosen.Int32(8);
        var offset = chosen.Int32(12);
        return data.Slice(offset, size).ToArray();
    }

    private static ResourceImage DecodeDib(PictureBytes dib, string format, bool isIcon, long pixelOffset = -1)
    {
        var headerSize = dib.Int32(0);
        int width, height, bits, compression = 0, colors = 0;
        if (headerSize == 12)
        {
            width = dib.Int16(4);
            height = dib.Int16(6);
            bits = dib.UInt16(10);
        }
        else
        {
            width = dib.Int32(4);
            height = dib.Int32(8);
            bits = dib.UInt16(14);
            compression = dib.Int32(16);
            colors = dib.Int32(32);
        }
        var topDown = height < 0;
        height = height == int.MinValue ? int.MaxValue : Math.Abs(height);
        // An icon's picture's height counts its AND mask too.
        if (isIcon)
        {
            height /= 2;
        }

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        {
            throw new InvalidDataException($"A {width}×{height} picture is out of range.");
        }

        uint redMask = 0x00FF0000, greenMask = 0x0000FF00, blueMask = 0x000000FF, alphaMask = bits == 32 && !isIcon ? 0u : 0xFF000000;
        long tableAt = headerSize;
        if (compression == 3 || compression == 6)
        {
            long masksAt = headerSize >= 52 ? 40 : headerSize;
            redMask = dib.UInt32(masksAt);
            greenMask = dib.UInt32(masksAt + 4);
            blueMask = dib.UInt32(masksAt + 8);
            alphaMask = headerSize >= 56 || compression == 6 ? dib.UInt32(masksAt + 12) : 0;
            if (headerSize == 40)
            {
                tableAt += compression == 6 ? 16 : 12;
            }
        }
        else if (compression != 0)
        {
            throw new InvalidDataException("Compressed BMPs are not supported.");
        }

        var entrySize = headerSize == 12 ? 3 : 4;
        var tableLength = bits <= 8 ? (colors > 0 ? colors : 1L << bits) : 0L;
        var start = pixelOffset >= 0 ? pixelOffset : tableAt + tableLength * entrySize;
        var stride = ((long)width * bits + 31) / 32 * 4;
        var rgba = new byte[width * height * 4];
        var anyAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var row = dib.Part(start + (topDown ? y : height - 1 - y) * stride, stride);
            for (var x = 0; x < width; x++)
            {
                byte r, g, b, a = 255;
                switch (bits)
                {
                    case 1 or 4 or 8:
                        var index = bits switch
                        {
                            1 => row[x >> 3] >> (7 - (x & 7)) & 1,
                            4 => row[x >> 1] >> ((x & 1) == 0 ? 4 : 0) & 0xF,
                            _ => row[x],
                        };
                        var entry = dib.Part(tableAt + Math.Min(index, tableLength - 1) * entrySize, 3);
                        (b, g, r) = (entry[0], entry[1], entry[2]);
                        break;
                    case 24:
                        (b, g, r) = (row[x * 3], row[x * 3 + 1], row[x * 3 + 2]);
                        break;
                    case 16 or 32:
                        var value = bits == 16 ? row.UInt16(x * 2) : row.UInt32(x * 4);
                        if (bits == 16 && compression == 0)
                        {
                            (redMask, greenMask, blueMask) = (0x7C00, 0x03E0, 0x001F);
                        }

                        r = Channel(value, redMask);
                        g = Channel(value, greenMask);
                        b = Channel(value, blueMask);
                        if (alphaMask != 0)
                        {
                            a = Channel(value, alphaMask);
                            anyAlpha |= a != 0;
                        }
                        break;
                    default:
                        throw new InvalidDataException($"{bits}-bit BMPs are not supported.");
                }
                var at = (y * width + x) * 4;
                rgba[at] = r;
                rgba[at + 1] = g;
                rgba[at + 2] = b;
                rgba[at + 3] = a;
            }
        }
        if (alphaMask != 0 && !anyAlpha)
        {
            for (var i = 3; i < rgba.Length; i += 4)
            {
                rgba[i] = 255;
            }
        }
        if (isIcon && (bits != 32 || !anyAlpha))
        {
            var maskStride = ((long)width + 31) / 32 * 4;
            var maskStart = start + stride * height;
            if (maskStart + maskStride * height <= dib.Length)
            {
                for (var y = 0; y < height; y++)
                {
                    var row = dib.Part(maskStart + (height - 1 - y) * maskStride, maskStride);
                    for (var x = 0; x < width; x++)
                    {
                        if ((row[x >> 3] >> (7 - (x & 7)) & 1) != 0)
                        {
                            rgba[(y * width + x) * 4 + 3] = 0;
                        }
                    }
                }
            }
        }
        return new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = format };

        static byte Channel(uint value, uint mask)
        {
            if (mask == 0)
            {
                return 0;
            }

            var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
            var max = mask >> shift;
            return (byte)(((value & mask) >> shift) * 255 / max);
        }
    }

    // What PngSharp read of a PNG: plain values, which MakePng makes into RGBA.
    private sealed record PngRead
    {
        public required int Width { get; init; }

        public required int Height { get; init; }

        public required int Depth { get; init; }

        public required ColorType ColorType { get; init; }

        public required int BytesPerPixel { get; init; }

        public required byte[] Pixels { get; init; }

        public required byte[]? Palette { get; init; }

        public required byte[]? Transparency { get; init; }
    }
}
