using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>One line of a <see cref="DecompiledDocument"/>. Its <see cref="Spans"/> are in order and
/// cover <see cref="Text"/> exactly, with no gaps.</summary>
public readonly struct DocumentLine
{
    public required string Text { get; init; }

    /// <summary>The spans of the whole document, which the lines share.</summary>
    public required ImmutableArray<DocumentSpan> DocumentSpans { get; init; }

    /// <summary>Where this line's spans start in <see cref="DocumentSpans"/>.</summary>
    public required int FirstSpan { get; init; }

    /// <summary>How many spans this line has.</summary>
    public required int SpanCount { get; init; }

    public ReadOnlySpan<DocumentSpan> Spans => DocumentSpans.AsSpan().Slice(FirstSpan, SpanCount);

    public override string ToString()
    {
        return Text;
    }
}
