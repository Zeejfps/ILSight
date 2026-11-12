using ZGF.Gui;

namespace ILSight.CodeTooltipArea;

/// <summary>A code tooltip as rich text: its declaration on one line and cut where each parameter
/// starts, the type or namespace it is in, its <c>where</c> clauses, and its documentation in
/// groups. The lists are <see cref="List{T}"/>s so that drawing them each frame walks them without
/// allocating.</summary>
internal sealed record TooltipLayout
{
    public static TooltipLayout Empty { get; } = new()
    {
        Declaration = [],
        DeclarationHead = [],
        Parameters = [],
        Container = [],
        Constraints = [],
        Groups = [],
    };

    public required IReadOnlyList<RichTextRun> Declaration { get; init; }

    /// <summary>The declaration up to its first parameter; empty when it has none.</summary>
    public required IReadOnlyList<RichTextRun> DeclarationHead { get; init; }

    /// <summary>Each parameter with its comma, the last with what follows it.</summary>
    public required List<IReadOnlyList<RichTextRun>> Parameters { get; init; }

    /// <summary>"in class Namespace.Type"; empty for the global namespace.</summary>
    public required IReadOnlyList<RichTextRun> Container { get; init; }

    public required List<IReadOnlyList<RichTextRun>> Constraints { get; init; }
    public required List<TooltipGroup> Groups { get; init; }
}
