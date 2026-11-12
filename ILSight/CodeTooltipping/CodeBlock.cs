using ZGF.Gui;

namespace ILSight.CodeTooltipping;

internal sealed record CodeBlock(IReadOnlyList<RichTextRun> Runs) : TooltipBlock;
