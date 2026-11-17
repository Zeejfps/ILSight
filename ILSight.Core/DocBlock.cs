namespace ILSight.Core;

/// <summary>A block of documentation: a paragraph, a code block or a list.</summary>
public union DocBlock(DocBlock.Paragraph, DocBlock.Code, DocBlock.List)
{
    /// <summary>A paragraph of styled runs.</summary>
    public sealed record Paragraph(IReadOnlyList<DocRun> Runs);

    /// <summary>A code block, its lines kept and their shared indentation removed.</summary>
    public sealed record Code(string Text);

    /// <summary>A list. <see cref="Header"/> is a table's heading row, or a list's leading item.</summary>
    public sealed record List
    {
        public required DocListKind Kind { get; init; }

        public required DocListItem? Header { get; init; }

        public required IReadOnlyList<DocListItem> Items { get; init; }
    }
}
