using ZGF.Gui;

namespace ILSight.CodeTooltipArea;

/// <summary>A section's label, the parameter or exception it is about, and its blocks.</summary>
internal sealed record TooltipEntry
{
    public required IReadOnlyList<RichTextRun>? Label { get; init; }
    public required IReadOnlyList<TooltipBlock> Blocks { get; init; }
}
