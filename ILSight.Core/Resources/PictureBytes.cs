using System.Buffers.Binary;

namespace ILSight.Core.Resources;

/// <summary>A picture's bytes as ILSight's own decoders read them (<see cref="ResourceImages"/>, <see cref="Gif"/>): a
/// read past their end, or before their start, is bad data, a <see cref="PictureFaultException"/> saying the
/// <see cref="Format"/> is cut short, never an index out of range, which stays a bug of the decoder's own.</summary>
internal readonly ref struct PictureBytes
{
    private readonly ReadOnlySpan<byte> _data;

    public PictureBytes(ReadOnlySpan<byte> data, string format)
    {
        _data = data;
        Format = format;
    }

    /// <summary>The format the bytes are read as, which the failure names.</summary>
    public string Format { get; }

    public int Length => _data.Length;

    public byte this[long at] => at >= 0 && at < _data.Length ? _data[(int)at] : throw CutShort();

    /// <summary>The bytes at <paramref name="at"/> up to, not including, <paramref name="at"/> +
    /// <paramref name="length"/>; cut short when any of them lies outside.</summary>
    public ReadOnlySpan<byte> Slice(long at, long length)
    {
        if (at < 0 || length < 0 || at + length > _data.Length)
        {
            throw CutShort();
        }

        return _data.Slice((int)at, (int)length);
    }

    /// <summary>The <paramref name="length"/> bytes from <paramref name="at"/>, read as these are.</summary>
    public PictureBytes Part(long at, long length)
    {
        return new PictureBytes(Slice(at, length), Format);
    }

    /// <summary>The bytes from <paramref name="at"/> to the end, read as these are.</summary>
    public PictureBytes From(long at)
    {
        return Part(at, _data.Length - at);
    }

    public short Int16(long at)
    {
        return BinaryPrimitives.ReadInt16LittleEndian(Slice(at, 2));
    }

    public ushort UInt16(long at)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(Slice(at, 2));
    }

    public int Int32(long at)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(Slice(at, 4));
    }

    public uint UInt32(long at)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(Slice(at, 4));
    }

    private PictureFaultException CutShort()
    {
        return new PictureFaultException(new PictureFault.CutShort(Format));
    }
}
