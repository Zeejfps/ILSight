using ZGF.Gui;

namespace ILSight.CodeTooltipping;

internal sealed record TextBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
