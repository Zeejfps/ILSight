using System.Diagnostics;

namespace ILSight.Core.Metadata;

/// <summary>What one column of a <see cref="MetadataGrid"/> is filtered by, as ILSpy's header filters do:
/// text for most columns, and for a column of flags which of them a row is to have.</summary>
public abstract class MetadataColumnFilter
{
    private MetadataColumnFilter()
    {
    }

    /// <summary>A <c>/regex/</c>, a comparison such as <c>&gt;= 0x100</c> for a number, or else a part
    /// of the cell's text, ignoring case; nothing filters nothing.</summary>
    public sealed class ByText : MetadataColumnFilter
    {
        public string Text { get; set; } = "";
    }

    /// <summary>The flags picked for a column of flags.</summary>
    public sealed class ByFlags : MetadataColumnFilter
    {
        public required FilterState Flags { get; init; }
    }

    public bool IsActive => this switch
    {
        ByText text => text.Text.Length > 0,
        ByFlags flags => !flags.Flags.IsEmpty,
        _ => throw new UnreachableException(),
    };
}
