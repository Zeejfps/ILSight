namespace ILSight;

/// <summary>A row of the analyzer: a symbol, an analysis of one, or a placeholder. The private protected
/// constructor closes it to its cases: a symbol, an analysis, a message, a root waiting for its symbol and a restored root that
/// failed.</summary>
public abstract class AnalyzerNode
{
    private protected AnalyzerNode()
    {
    }
}
