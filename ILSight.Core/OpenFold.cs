namespace ILSight.Core;

/// <summary>A fold the decompiler started and hasn't ended: where it starts and how it shows folded.</summary>
internal readonly record struct OpenFold
{
    public required int Line { get; init; }

    public required int Column { get; init; }

    public required string Text { get; init; }

    public required bool DefaultCollapsed { get; init; }

    public required bool IsDefinition { get; init; }
}
