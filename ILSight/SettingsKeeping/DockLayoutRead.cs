using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;

namespace ILSight.SettingsKeeping;

/// <summary>What a dock layout a settings file holds reads as.</summary>
internal abstract record DockLayoutRead
{
    private DockLayoutRead()
    {
    }

    /// <summary>The layout, with <see cref="DockLayout.NextId"/> above every id it uses.</summary>
    public sealed record Parsed : DockLayoutRead
    {
        public required DockLayout Layout { get; init; }
    }

    /// <summary>A part of the layout is held as null.</summary>
    public sealed record PartsMissing : DockLayoutRead;

    /// <summary>Two of its nodes or windows have the same id, so they would move together.</summary>
    public sealed record IdsRepeated : DockLayoutRead;

    public TResult Match<TResult>(Func<Parsed, TResult> parsed, Func<PartsMissing, TResult> partsMissing, Func<IdsRepeated, TResult> idsRepeated)
    {
        return this switch
        {
            Parsed p => parsed(p),
            PartsMissing m => partsMissing(m),
            IdsRepeated r => idsRepeated(r),
            _ => throw new UnreachableException($"DockLayoutRead.Match does not handle {GetType().Name}."),
        };
    }
}
