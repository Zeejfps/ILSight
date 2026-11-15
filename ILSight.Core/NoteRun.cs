namespace ILSight.Core;

/// <summary>A run of a note's text as it is written into a line, a link to <see cref="Reference"/> when set.</summary>
internal readonly record struct NoteRun
{
    public required string Text { get; init; }

    public required SpanKind Kind { get; init; }

    public CodeReference? Reference { get; init; }
}
