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

    /// <summary>A copy of this filter as it stands, for a filter run to read while this one changes.</summary>
    internal MetadataColumnFilter Copy()
    {
        return this switch
        {
            ByText text => new ByText { Text = text.Text },
            ByFlags flags => new ByFlags { Flags = CopyFlags(flags.Flags) },
            _ => throw new UnreachableException(),
        };
    }

    private static FilterState CopyFlags(FilterState state)
    {
        var copy = new FilterState { Schema = state.Schema, Mode = state.Mode, Revision = state.Revision };
        foreach (var (name, group) in state.Groups)
        {
            copy.Groups[name] = group;
        }
        foreach (var (name, flag) in state.Flags)
        {
            copy.Flags[name] = flag;
        }
        return copy;
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<ByText, TResult> byText, Func<ByFlags, TResult> byFlags)
    {
        return this switch
        {
            ByText c => byText(c),
            ByFlags c => byFlags(c),
            _ => throw new UnreachableException($"MetadataColumnFilter.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it.</summary>
    public void Switch(Action<ByText> byText, Action<ByFlags> byFlags)
    {
        switch (this)
        {
            case ByText c:
                byText(c);
                return;
            case ByFlags c:
                byFlags(c);
                return;
            default:
                throw new UnreachableException($"MetadataColumnFilter.Switch does not handle {GetType().Name}.");
        }
    }
}
