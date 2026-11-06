using System.Diagnostics;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight;

/// <summary>Whether the "compare with" dialog shows, and of which assembly.</summary>
public abstract record ComparisonDialog
{
    private ComparisonDialog()
    {
    }

    public sealed record Closed : ComparisonDialog;

    /// <summary>Asks what to compare <see cref="Compared"/> with.</summary>
    public sealed record Open(LoadedModule Compared) : ComparisonDialog;

    /// <summary>Dismissed from the dialog itself, which goes out still showing <see cref="Compared"/>.</summary>
    public sealed record Going(LoadedModule Compared) : ComparisonDialog;
}

/// <summary>The "compare with" dialog: the assembly to compare, and the other open assemblies whose API
/// it can be compared with, those of its name first, filtered by name or path, one of them selected. It
/// closes when the assembly to compare is closed.</summary>
public sealed class ComparisonPicker
{
    public readonly List<LoadedModule> Candidates = new();

    /// <summary>The assemblies offered, in the order shown.</summary>
    public readonly List<LoadedModule> Filtered = new();
    public int ListedVersion = -1;
    public string? AppliedFilter;
    public bool FocusFilter;

    public ComparisonDialog Dialog = new ComparisonDialog.Closed();
    public string Filter = "";

    public ListSelection Selection { get; } = new();

    public bool IsOpen => Dialog is ComparisonDialog.Open;

    /// <summary>The assembly to compare, or null while the dialog is closed.</summary>
    public LoadedModule? Compared
    {
        get
        {
            return Dialog switch
            {
                ComparisonDialog.Closed => null,
                ComparisonDialog.Open open => open.Compared,
                ComparisonDialog.Going going => going.Compared,
                _ => throw new UnreachableException(),
            };
        }
    }

    /// <summary>The assembly selected to compare with, or null when none is.</summary>
    public LoadedModule? Selected
    {
        get
        {
            if (!Selection.HasSelection)
                return null;
            return Filtered[Selection.Index];
        }
    }
}
