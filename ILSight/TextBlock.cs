using ZGF.Gui;

namespace ILSight;

internal sealed record TextBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
