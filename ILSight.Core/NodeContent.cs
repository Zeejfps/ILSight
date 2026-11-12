using System.Diagnostics;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>What a <see cref="BrowseNode"/> shows: a <see cref="Code"/> document, a
/// <see cref="Table"/> grid or a <see cref="Picture"/>.</summary>
public abstract record NodeContent
{
    private NodeContent()
    {
    }

    public sealed record Code(DecompiledDocument Document) : NodeContent;

    public sealed record Table(MetadataGrid Grid) : NodeContent;

    /// <summary>A picture, and for one stored as markup, such as SVG, that markup as a document.</summary>
    public sealed record Picture(ResourceImage Image, DecompiledDocument? Source = null) : NodeContent;

    public TResult Match<TResult>(Func<Code, TResult> code, Func<Table, TResult> table, Func<Picture, TResult> picture)
    {
        return this switch
        {
            Code c => code(c),
            Table t => table(t),
            Picture p => picture(p),
            _ => throw new UnreachableException($"NodeContent.Match does not handle {GetType().Name}."),
        };
    }
}
