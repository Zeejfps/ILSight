using ILSight.Core;

namespace ILSight;

/// <summary>The case-insensitive matches of a query in a document, found once per pair.</summary>
internal sealed class CodeFindIndex
{
    public readonly List<TextPosition> Matches = new();
    public DecompiledDocument? Document;
    public string Query = "";

    public int Length => Query.Length;
}
