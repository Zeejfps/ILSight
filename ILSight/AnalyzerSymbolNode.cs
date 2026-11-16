using ILSight.Core.AnalysisArea;

namespace ILSight;

/// <summary>A symbol analyzed or found. Its children are its accessors, if it is a property or
/// event, then each analysis that applies to it.</summary>
public sealed class AnalyzerSymbolNode : AnalyzerNode
{
    private List<AnalyzerNode>? _children;

    public required AnalysisSymbol Symbol { get; init; }

    /// <summary>The children, or null while they were never asked for.</summary>
    public IReadOnlyList<AnalyzerNode>? Children => _children;

    /// <summary>The children, listed the first time they are asked for.</summary>
    public IReadOnlyList<AnalyzerNode> EnsureChildren()
    {
        _children ??= ListChildren();
        return _children;
    }

    private List<AnalyzerNode> ListChildren()
    {
        var children = new List<AnalyzerNode>();
        foreach (var accessor in Symbol.Accessors)
        {
            children.Add(new AnalyzerSymbolNode { Symbol = accessor });
        }

        foreach (var kind in Symbol.Kinds)
        {
            children.Add(new AnalyzerKindNode { Subject = Symbol, Kind = kind });
        }

        return children;
    }
}
