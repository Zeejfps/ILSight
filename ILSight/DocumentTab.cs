using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>An open tab: where it points, the node found there, and what it shows of it, which arrives
/// from the background. Changed through <see cref="Tabs"/>, on the UI thread only.</summary>
public sealed class DocumentTab
{
    /// <summary>The tab's id, which <see cref="DocumentTabs.AddNew"/> hands out and never hands out again, so that
    /// with <see cref="Version"/> it names what the tab showed wherever several tabs' content meets
    /// (<see cref="TabVersion"/>).</summary>
    public required int Id { get; init; }

    /// <summary>The load of what the tab shows: one started cancels the one before, whose result is then
    /// dropped; once the tab is closed, what is in flight is cancelled and its result dropped.</summary>
    public LatestWork Work { get; } = new();

    /// <summary>Where the view is to stand once the load in flight arrives.</summary>
    public Arrival Arrival;

    /// <summary>Where the view of the shown node last stood, as <see cref="Position"/> gives it.</summary>
    public ViewPosition ViewPosition;

    /// <summary>What <see cref="Changes"/> had counted when the tab was last pointed somewhere, past what
    /// its diagram or comparison counts now.</summary>
    public int PointedChanges;

    /// <summary>Where the tab points: the path of its node, or while the node is away the path it is
    /// found again by or no longer exists at; outlives reloads of the assembly.</summary>
    public NodePath? Location => Content switch
    {
        TabContent.Reloading reloading => reloading.Location,
        TabContent.Gone gone => gone.Location,
        TabContent.Loading or TabContent.Code or TabContent.Table or TabContent.Picture or TabContent.Failed => Node is null ? null : BrowseNodes.GetPath(Node),
        TabContent.Empty or TabContent.Report or TabContent.Diagram or TabContent.Comparison or TabContent.Reopening => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>The node shown, or null while <see cref="Location"/> is being found again after a
    /// reload, or when it no longer exists.</summary>
    public BrowseNode? Node => Content switch
    {
        TabContent.Loading loading => loading.Node,
        TabContent.Code code => code.Node,
        TabContent.Table table => table.Node,
        TabContent.Picture picture => picture.Node,
        TabContent.Failed failed => failed.Node,
        TabContent.Empty or TabContent.Report or TabContent.Diagram or TabContent.Comparison or TabContent.Reloading
            or TabContent.Reopening or TabContent.Gone => null,
        _ => throw new UnreachableException(),
    };

    public DecompilationLanguage Language;

    /// <summary>The options <see cref="Document"/> was decompiled with.</summary>
    public DecompilerOptions Options = DecompilerOptions.Default;

    public TabContent Content = new TabContent.Empty();

    /// <summary>The code, document or markup the tab shows, or an empty document while it shows none.</summary>
    public DecompiledDocument Document => Content switch
    {
        TabContent.Code code => code.Document,
        TabContent.Picture picture => picture.Source ?? DecompiledDocument.Empty,
        TabContent.Empty or TabContent.Loading or TabContent.Table or TabContent.Report or TabContent.Diagram or TabContent.Comparison
            or TabContent.Failed or TabContent.Reloading or TabContent.Reopening or TabContent.Gone => DecompiledDocument.Empty,
        _ => throw new UnreachableException(),
    };

    /// <summary>The rows the tab shows, or null while it shows no grid.</summary>
    public MetadataGrid? Grid => (Content as TabContent.Table)?.Grid;

    /// <summary>The picture the tab shows, or null while it shows none.</summary>
    public ResourceImage? Image => (Content as TabContent.Picture)?.Image;

    /// <summary>The diagram the tab shows, or null while it shows none.</summary>
    public TypeDiagram? Diagram => (Content as TabContent.Diagram)?.Types;

    /// <summary>The comparison the tab shows, or null while it shows none.</summary>
    public AssemblyComparison? Comparison => (Content as TabContent.Comparison)?.Assemblies;

    /// <summary>Whether navigating from the tab may show what it is asked for in its place: not while it
    /// shows a diagram or a comparison, which are worked in rather than passed through.</summary>
    public bool IsReplaceable
    {
        get
        {
            if (Diagram is not null)
            {
                return false;
            }

            return Comparison is null;
        }
    }

    /// <summary>Where the view of the shown node stands, as the view last kept it, or null while there is none.</summary>
    public ViewPosition? Position
    {
        get
        {
            if (IsBusy)
            {
                return null;
            }

            if (Node is null)
            {
                return null;
            }

            return ViewPosition;
        }
    }

    /// <summary>Where the view of the shown node is to stand when the tab is opened again: where it
    /// stands, or while the node loads, where it is to stand once loaded.</summary>
    public ViewPosition? PositionToReopen
    {
        get
        {
            if (IsBusy)
            {
                return Arrival.Position;
            }

            return Position;
        }
    }

    /// <summary>Counts up whenever the tab is pointed somewhere, the diagram it shows gains or loses a
    /// card, or the comparison it shows swaps its sides or takes in more or less; it never counts down.</summary>
    public int Changes => PointedChanges + Tabs.CountContentChanges(this);

    public bool IsBusy => Work.IsRunning;

    /// <summary>The name of what the tab shows: its node's, kept by what it shows while the node is away, or
    /// its diagram's or comparison's own; null for a new tab or a report.</summary>
    public string? Title => Content switch
    {
        TabContent.Loading or TabContent.Code or TabContent.Table or TabContent.Picture or TabContent.Failed => Node is null ? null : NodeText.Name(Node),
        TabContent.Diagram diagram => diagram.Title,
        TabContent.Comparison comparison => comparison.Title,
        TabContent.Reloading reloading => reloading.Title,
        TabContent.Reopening reopening => reopening.Title,
        TabContent.Gone gone => gone.Title,
        TabContent.Empty or TabContent.Report => null,
        _ => throw new UnreachableException(),
    };

    /// <summary>The full identity of what the tab shows, or null for a new tab.</summary>
    public string? Tooltip => Node is not null ? NodeText.Tooltip(Node) : Title;

    /// <summary>Changes whenever the tab is pointed somewhere, or loses its node.</summary>
    public int Version;
}
