using System.Buffers.Binary;

namespace ILSight.Core.Resources;

/// <summary>The first frame of a GIF, as ILSpy shows it.</summary>
internal static class Gif
{
    public static ResourceImage Decode(ReadOnlySpan<byte> data)
    {
        var width = BinaryPrimitives.ReadUInt16LittleEndian(data[6..]);
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data[8..]);
        var flags = data[10];
        var at = 13;
        ReadOnlySpan<byte> global = default;
        if ((flags & 0x80) != 0)
        {
            var size = 3 << ((flags & 7) + 1);
            global = data.Slice(at, size);
            at += size;
        }
        var transparent = -1;
        var rgba = new byte[width * height * 4];
        while (at < data.Length)
        {
            switch (data[at])
            {
                case 0x21:
                    if (data[at + 1] == 0xF9 && (data[at + 3] & 1) != 0)
                    {
                        transparent = data[at + 6];
                    }

                    at += 2;
                    at = SkipBlocks(data, at);
                    break;
                case 0x2C:
                    var left = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 1)..]);
                    var top = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 3)..]);
                    var frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 5)..]);
                    var frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 7)..]);
                    var frameFlags = data[at + 9];
                    at += 10;
                    var palette = global;
                    if ((frameFlags & 0x80) != 0)
                    {
                        var size = 3 << ((frameFlags & 7) + 1);
                        palette = data.Slice(at, size);
                        at += size;
                    }
                    var minimumCodeSize = data[at++];
                    var compressed = new List<byte>();
                    while (data[at] != 0)
                    {
                        compressed.AddRange(data.Slice(at + 1, data[at]));
                        at += data[at] + 1;
                    }
                    var indices = Lzw(compressed, minimumCodeSize, frameWidth * frameHeight);
                    var interlaced = (frameFlags & 0x40) != 0;
                    for (var i = 0; i < indices.Length; i++)
                    {
                        var row = interlaced ? InterlacedRow(i / frameWidth, frameHeight) : i / frameWidth;
                        int x = left + i % frameWidth, y = top + row;
                        if (x >= width || y >= height)
                        {
                            continue;
                        }

                        var index = indices[i];
                        var pixel = (y * width + x) * 4;
                        if (index == transparent || index * 3 + 2 >= palette.Length)
                        {
                            continue;
                        }

                        rgba[pixel] = palette[index * 3];
                        rgba[pixel + 1] = palette[index * 3 + 1];
                        rgba[pixel + 2] = palette[index * 3 + 2];
                        rgba[pixel + 3] = 255;
                    }
                    return new ResourceImage { Width = width, Height = height, Rgba = rgba, Format = "GIF" };
                default:
                    throw new InvalidDataException("The GIF has no picture.");
            }
        }
        throw new InvalidDataException("The GIF has no picture.");
    }

    private static int SkipBlocks(ReadOnlySpan<byte> data, int at)
    {
        while (data[at] != 0)
        {
            at += data[at] + 1;
        }

        return at + 1;
    }

    private static int InterlacedRow(int pass, int height)
    {
        ReadOnlySpan<(int Start, int Step)> passes = [(0, 8), (4, 8), (2, 4), (1, 2)];
        foreach (var (start, step) in passes)
        {
            var rows = (height - start + step - 1) / step;
            if (pass < rows)
            {
                return start + pass * step;
            }

            pass -= rows;
        }
        return height;
    }

    private static byte[] Lzw(List<byte> data, int minimumCodeSize, int pixels)
    {
        var output = new byte[pixels];
        var written = 0;
        var clear = 1 << minimumCodeSize;
        var end = clear + 1;
        var prefix = new short[4096];
        var suffix = new byte[4096];
        var length = new short[4096];
        for (var i = 0; i < clear; i++)
        {
            prefix[i] = -1;
            suffix[i] = (byte)i;
            length[i] = 1;
        }
        var next = end + 1;
        var codeSize = minimumCodeSize + 1;
        var previous = -1;
        int bits = 0, buffer = 0, position = 0;
        Span<byte> stack = stackalloc byte[4096];
        while (written < pixels)
        {
            while (bits < codeSize)
            {
                if (position >= data.Count)
                {
                    return output;
                }

                buffer |= data[position++] << bits;
                bits += 8;
            }
            var code = buffer & ((1 << codeSize) - 1);
            buffer >>= codeSize;
            bits -= codeSize;
            if (code == clear)
            {
                next = end + 1;
                codeSize = minimumCodeSize + 1;
                previous = -1;
                continue;
            }
            if (code == end)
            {
                break;
            }

            int entry;
            if (previous < 0)
            {
                entry = code;
            }
            else
            {
                var first = code < next ? FirstOf(code) : FirstOf(previous);
                if (next < 4096)
                {
                    prefix[next] = (short)previous;
                    suffix[next] = first;
                    length[next] = (short)(length[previous] + 1);
                    next++;
                    if (next == 1 << codeSize && codeSize < 12)
                    {
                        codeSize++;
                    }
                }
                entry = code;
            }
            if (entry >= next)
            {
                return output;
            }

            var count = length[entry];
            for (int c = entry, i = count - 1; c >= 0 && i >= 0; c = prefix[c], i--)
            {
                stack[i] = suffix[c];
            }

            for (var i = 0; i < count && written < pixels; i++)
            {
                output[written++] = stack[i];
            }

            previous = code;
        }
        return output;

        byte FirstOf(int code)
        {
            while (prefix[code] >= 0)
            {
                code = prefix[code];
            }

            return suffix[code];
        }
    }
}
