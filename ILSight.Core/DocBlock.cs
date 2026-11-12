using System.Diagnostics;

namespace ILSight.Core;

/// <summary>A block of documentation: a paragraph, a code block or a list.</summary>
public abstract record DocBlock
{
    private DocBlock()
    {
    }

    /// <summary>A paragraph of styled runs.</summary>
    public sealed record Paragraph(IReadOnlyList<DocRun> Runs) : DocBlock;

    /// <summary>A code block, its lines kept and their shared indentation removed.</summary>
    public sealed record Code(string Text) : DocBlock;

    /// <summary>A list. <see cref="Header"/> is a table's heading row, or a list's leading item.</summary>
    public sealed record List : DocBlock
    {
        public required DocListKind Kind { get; init; }

        public required DocListItem? Header { get; init; }

        public required IReadOnlyList<DocListItem> Items { get; init; }
    }

    public TResult Match<TResult>(Func<Paragraph, TResult> paragraph, Func<Code, TResult> code, Func<List, TResult> list)
    {
        return this switch
        {
            Paragraph p => paragraph(p),
            Code c => code(c),
            List l => list(l),
            _ => throw new UnreachableException($"DocBlock.Match does not handle {GetType().Name}."),
        };
    }
}
