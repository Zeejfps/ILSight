namespace ILSight;

/// <summary>A root asked for from a code reference whose symbol couldn't be found because looking for it failed: shown
/// failed with why where its loading row was, until the user removes it or the analyses are cleared. It saves as
/// nothing (no analysis was found to save), and nothing asks for it again: analyzing the reference again does.</summary>
public sealed class AnalyzerUnresolvedNode : AnalyzerNode
{
    /// <summary>What could not be read: the module the reference leads into, or the reference itself.</summary>
    public required string Name { get; init; }

    public required Exception Error { get; init; }
}
