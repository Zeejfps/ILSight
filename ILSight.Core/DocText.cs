using System.Text;

namespace ILSight.Core;

/// <summary>
/// Documentation blocks as they are read: runs join the open paragraph with their whitespace
/// collapsed, as XML documentation is rendered, and a paragraph is kept only when it has text.
/// Code blocks and lists stand on their own between paragraphs.
/// </summary>
internal sealed class DocText
{
    public readonly List<DocBlock> Blocks = new();
    public List<DocRun> Runs = new();

    public bool IsEmpty => Blocks.Count == 0 && Runs.Count == 0;
}
