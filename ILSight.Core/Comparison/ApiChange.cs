namespace ILSight.Core.Comparison;

/// <summary>How an entry differs from the left assembly to the right one.</summary>
public enum ApiChange
{
    Unchanged,

    /// <summary>On the right only.</summary>
    Added,

    /// <summary>On the left only.</summary>
    Removed,

    /// <summary>In both, declared differently: its accessibility, modifiers, type, base types, value, or
    /// whether it is obsolete or flags.</summary>
    Changed,
}
