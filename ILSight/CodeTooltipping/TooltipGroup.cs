using ZGF.Gui;
using ILSight.Core;

namespace ILSight.CodeTooltipping;

/// <summary>A group of the documentation: the summary, or every section of one kind under its
/// heading, one entry per section.</summary>
internal sealed record TooltipGroup
{
    public required DocSectionKind Kind { get; init; }
    public required IReadOnlyList<RichTextRun>? Heading { get; init; }
    public required EntryShape Shape { get; init; }
    public List<TooltipEntry> Entries { get; } = [];
}
