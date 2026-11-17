namespace ILSight.CodeTooltipArea;

/// <summary>A block of a tooltip entry: a paragraph, a code block or a list.</summary>
internal union TooltipBlock(TextBlock, CodeBlock, ListBlock);
