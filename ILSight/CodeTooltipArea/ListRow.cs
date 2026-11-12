using ZGF.Gui;

namespace ILSight.CodeTooltipArea;

internal sealed record ListRow
{
    public required IReadOnlyList<RichTextRun> Lead { get; init; }
    public required IReadOnlyList<RichTextRun> Content { get; init; }
}
