using ZGF.Gui;

namespace ILSight;

internal sealed record CodeBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
