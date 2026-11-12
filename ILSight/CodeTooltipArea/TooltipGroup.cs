using ZGF.Gui;
using ILSight.Core;

namespace ILSight.CodeTooltipArea;

/// <summary>A group of the documentation: the summary, or every section of one kind under its
/// heading, one entry per section. <see cref="Heading"/> is null for a group shown with none.</summary>
internal sealed record TooltipGroup
{
    public required IReadOnlyList<RichTextRun>? Heading { get; init; }
    public required EntryShape Shape { get; init; }
    public List<TooltipEntry> Entries { get; } = [];
}
