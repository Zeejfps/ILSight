using ZGF.Gui;

namespace ILSight.CodeTooltipArea;

internal sealed record CodeBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
