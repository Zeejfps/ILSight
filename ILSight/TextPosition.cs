using ILSight.Core;

namespace ILSight;

/// <summary>A caret position in a <see cref="DecompiledDocument"/>: a line and a UTF-16 column,
/// which is also the cell the code view draws it in.</summary>
public readonly record struct TextPosition : IComparable<TextPosition>
{
    public required int Line { get; init; }

    public required int Column { get; init; }

    public int CompareTo(TextPosition other)
    {
        return Line != other.Line ? Line.CompareTo(other.Line) : Column.CompareTo(other.Column);
    }

    public static bool operator <(TextPosition a, TextPosition b)
    {
        return a.CompareTo(b) < 0;
    }

    public static bool operator >(TextPosition a, TextPosition b)
    {
        return a.CompareTo(b) > 0;
    }

    public static bool operator <=(TextPosition a, TextPosition b)
    {
        return a.CompareTo(b) <= 0;
    }

    public static bool operator >=(TextPosition a, TextPosition b)
    {
        return a.CompareTo(b) >= 0;
    }
}
