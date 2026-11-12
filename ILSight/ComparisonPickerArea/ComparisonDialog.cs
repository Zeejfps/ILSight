using System.Diagnostics;
using ILSight.Core;

namespace ILSight.ComparisonPickerArea;

/// <summary>Whether the "compare with" dialog shows, and of which assembly.</summary>
internal abstract record ComparisonDialog
{
    private ComparisonDialog()
    {
    }

    public sealed record Closed : ComparisonDialog;

    /// <summary>Asks what to compare <see cref="Compared"/> with.</summary>
    public sealed record Open(LoadedModule Compared) : ComparisonDialog;

    /// <summary>Dismissed from the dialog itself, which goes out still showing <see cref="Compared"/>.</summary>
    public sealed record Going(LoadedModule Compared) : ComparisonDialog;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Closed, TResult> closed, Func<Open, TResult> open, Func<Going, TResult> going)
    {
        return this switch
        {
            Closed c => closed(c),
            Open c => open(c),
            Going c => going(c),
            _ => throw new UnreachableException($"ComparisonDialog.Match does not handle {GetType().Name}."),
        };
    }
}
