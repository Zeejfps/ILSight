using ZGF.Gui;

namespace ILSight.CodeTooltipArea;

internal sealed record TextBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
