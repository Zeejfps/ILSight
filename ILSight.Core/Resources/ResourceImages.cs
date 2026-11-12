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

    /// <summary>Gives pixel <paramref name="x"/>, <paramref name="y"/> of the shrunk picture the average
    /// of the pixels it covers, their colors weighed by their alpha so clear pixels lend no color.</summary>
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
    /// on.</summary>
    internal static ResourceImageRead DecodeImage(byte[] data)
    {
        ResourceImage? image;
        try
        {
            image = DecodeKnown(data);
        }
        catch (InvalidDataException e)
        {
            return new ResourceImageRead.Unreadable { Why = e.Message };
        }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException)
        {
            return new ResourceImageRead.Unreadable { Why = $"The picture cannot be read: {e.Message}" };
        }

        if (image is null)
        {
            return new ResourceImageRead.Unreadable { Why = "This is no PNG, JPEG, BMP, GIF, ICO, CUR or SVG picture." };
        }

        return new ResourceImageRead.Decoded { Image = image };
    }

    /// <summary>The picture in <paramref name="data"/>, decoded by the format its first bytes name; null when they name
    /// none of these.</summary>
    private static ResourceImage? DecodeKnown(byte[] data)
    {
        ReadOnlySpan<byte> span = data;
        if (span.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
        {
            return DecodePng(data);
        }

        if (span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8]))
        {
            return DecodeJpeg(data);
        }

        if (span.StartsWith("BM"u8))
        {
            return DecodeBitmap(span, "BMP");
        }

        if (span.StartsWith("GIF8"u8))
        {
            return Gif.Decode(span);
        }

        if (span.Length >= 6 && span[0] == 0 && span[1] == 0 && span[2] is 1 or 2)
        {
            return DecodeIcon(span);
        }

        if (SvgPicture.IsSvg(data))
        {
            return SvgPicture.Rasterize(data);
        }

        return null;
    }

    private static ResourceImage DecodePng(byte[] data)
    {
        var png = PngSharp.Api.Png.DecodeFromByteArray(data);
        var ihdr = png.Ihdr;
        int width = (int)ihdr.Width, height = (int)ihdr.Height;
        var depth = ihdr.BitDepth;
        var src = png.PixelData;
        var bytesPerPixel = ihdr.GetBytesPerPixel();
        var wide = depth == 16;
        var palette = png.Plte?.Entries;
        var transparency = png.Trns?.Data;
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var at = i * bytesPerPixel;
            byte r, g, b, a = 255;
            switch (ihdr.ColorType)
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
                    throw new InvalidDataException($"Unknown PNG color type {ihdr.ColorType}.");
            }
            rgba[i * 4] = r;
            rgba[i * 4 + 1] = g;
            rgba[i * 4 + 2] = b;
            rgba[i * 4 + 3] = a;
        }
        return new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = "PNG" };

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

    private static ResourceImage DecodeJpeg(byte[] data)
    {
        var jpeg = JpegSharp.Api.Jpeg.DecodeAnyPrecision(data);
        var pixels = jpeg.ToRgba8888();
        var rgba = new byte[pixels.Length * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(rgba.AsSpan(i * 4), pixels[i]);
        }

        return new ResourceImage { Width = jpeg.Width, Height = jpeg.Height, Rgba = rgba, Format = "JPEG" };
    }

    private static ResourceImage DecodeIcon(ReadOnlySpan<byte> data)
    {
        var format = data[2] == 2 ? "CUR" : "ICO";
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data[4..]);
        var best = -1;
        var bestArea = -1;
        for (var i = 0; i < count; i++)
        {
            var entry = data.Slice(6 + i * 16, 16);
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

        var chosen = data.Slice(6 + best * 16, 16);
        var size = BinaryPrimitives.ReadInt32LittleEndian(chosen[8..]);
        var offset = BinaryPrimitives.ReadInt32LittleEndian(chosen[12..]);
        var payload = data.Slice(offset, size);
        if (payload.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
        {
            var png = DecodePng(payload.ToArray());
            return new ResourceImage { Width = png.Width, Height = png.Height, Rgba = png.Rgba, Format = format };
        }
        return DecodeDib(payload, format, isIcon: true);
    }

    private static ResourceImage DecodeBitmap(ReadOnlySpan<byte> data, string format)
    {
        var pixels = BinaryPrimitives.ReadInt32LittleEndian(data[10..]);
        return DecodeDib(data[14..], format, isIcon: false, pixelOffset: pixels - 14);
    }

    /// <summary>A device-independent bitmap: a BMP after its file header, or a picture of an icon,
    /// whose height counts its AND mask too.</summary>
    private static ResourceImage DecodeDib(ReadOnlySpan<byte> dib, string format, bool isIcon, int pixelOffset = -1)
    {
        var headerSize = BinaryPrimitives.ReadInt32LittleEndian(dib);
        int width, height, bits, compression = 0, colors = 0;
        if (headerSize == 12)
        {
            width = BinaryPrimitives.ReadInt16LittleEndian(dib[4..]);
            height = BinaryPrimitives.ReadInt16LittleEndian(dib[6..]);
            bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[10..]);
        }
        else
        {
            width = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
            height = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
            bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
            compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
            colors = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        }
        var topDown = height < 0;
        height = Math.Abs(height);
        if (isIcon)
        {
            height /= 2;
        }

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        {
            throw new InvalidDataException($"A {width}×{height} picture is out of range.");
        }

        uint redMask = 0x00FF0000, greenMask = 0x0000FF00, blueMask = 0x000000FF, alphaMask = bits == 32 && !isIcon ? 0u : 0xFF000000;
        var tableAt = headerSize;
        if (compression == 3 || compression == 6)
        {
            var masksAt = headerSize >= 52 ? 40 : headerSize;
            redMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[masksAt..]);
            greenMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[(masksAt + 4)..]);
            blueMask = BinaryPrimitives.ReadUInt32LittleEndian(dib[(masksAt + 8)..]);
            alphaMask = headerSize >= 56 || compression == 6 ? BinaryPrimitives.ReadUInt32LittleEndian(dib[(masksAt + 12)..]) : 0;
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
        var tableLength = bits <= 8 ? (colors > 0 ? colors : 1 << bits) : 0;
        var start = pixelOffset >= 0 ? pixelOffset : tableAt + tableLength * entrySize;
        var stride = (width * bits + 31) / 32 * 4;
        var rgba = new byte[width * height * 4];
        var anyAlpha = false;
        for (var y = 0; y < height; y++)
        {
            var row = dib.Slice(start + (topDown ? y : height - 1 - y) * stride, stride);
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
                        var entry = dib.Slice(tableAt + Math.Min(index, tableLength - 1) * entrySize, 3);
                        (b, g, r) = (entry[0], entry[1], entry[2]);
                        break;
                    case 24:
                        (b, g, r) = (row[x * 3], row[x * 3 + 1], row[x * 3 + 2]);
                        break;
                    case 16 or 32:
                        var value = bits == 16 ? BinaryPrimitives.ReadUInt16LittleEndian(row[(x * 2)..]) : BinaryPrimitives.ReadUInt32LittleEndian(row[(x * 4)..]);
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
            var maskStride = (width + 31) / 32 * 4;
            var maskStart = start + stride * height;
            if (maskStart + maskStride * height <= dib.Length)
            {
                for (var y = 0; y < height; y++)
                {
                    var row = dib.Slice(maskStart + (height - 1 - y) * maskStride, maskStride);
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
}
