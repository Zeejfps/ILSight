namespace ILSight.Core;

/// <summary>How a run of documentation text is shown.</summary>
public enum DocStyle
{
    Plain,
    Bold,
    Italic,
    /// <summary>Code: a <c>&lt;c&gt;</c> element, a keyword, or a parameter named in the text.</summary>
    Code,
    /// <summary>A type or member the text refers to; <see cref="DocRun.Reference"/> names it when it was found.</summary>
    Reference,
}
