namespace ILSight.Core.MetadataArea;

/// <summary>What one column of a <see cref="MetadataGrid"/> is filtered by, as ILSpy's header filters do:
/// text for most columns, and for a column of flags which of them a row is to have. The cases are classes: a column's
/// filter is edited in place, every copy of the union holding the one being edited.</summary>
public union MetadataColumnFilter(MetadataColumnFilter.ByText, MetadataColumnFilter.ByFlags)
{
    /// <summary>A <c>/regex/</c>, a comparison such as <c>&gt;= 0x100</c> for a number, or else a part
    /// of the cell's text, ignoring case; nothing filters nothing.</summary>
    public sealed class ByText
    {
        public string Text { get; set; } = "";
    }

    /// <summary>The flags picked for a column of flags.</summary>
    public sealed class ByFlags
    {
        public required FilterState Flags { get; init; }
    }

    public bool IsActive => this switch
    {
        ByText text => text.Text.Length > 0,
        ByFlags flags => !flags.Flags.IsEmpty,
    };

    /// <summary>A copy of this filter as it stands, for a filter run to read while this one changes.</summary>
    internal MetadataColumnFilter Copy()
    {
        return this switch
        {
            ByText text => new ByText { Text = text.Text },
            ByFlags flags => new ByFlags { Flags = flags.Flags.Copy() },
        };
    }
}
