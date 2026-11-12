using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>An open tab as <see cref="Navigation"/> hands it out: which document it is (the node it points at, a
/// diagram, a comparison, a report), read only, and the load of its node (<see cref="Load"/>), which keeps its own
/// rule. Which document it is changes only through the navigation, which holds it (<see cref="TabDocument"/>). Used
/// on the UI thread only.</summary>
public sealed class DocumentTab
{
    private readonly TabDocument _document;

    /// <param name="id">The tab's id, never given to another tab.</param>
    /// <param name="document">Which document the tab is, which only its holder changes.</param>
    public DocumentTab(int id, TabDocument document)
    {
        Id = id;
        _document = document;
    }

    /// <summary>The tab's id, which <see cref="DocumentTabs.AddNew"/> hands out and never hands out again, so that
    /// with <see cref="Version"/> it names what the tab showed wherever several tabs' content meets
    /// (<see cref="TabVersion"/>).</summary>
    public int Id { get; }

    /// <summary>The load of the node the tab points at: what it shows of it, the language and options it is shown
    /// in, and where its view stands; a load of no node while it points at none.</summary>
    public TabLoad Load => _document.Load;

    public TabContent Content => _document.Content;

    /// <summary>Where the tab points: the path of its node, or while the node is away the path it is
    /// found again by or no longer exists at; outlives reloads of the assembly.</summary>
    public NodePath? Location => Load.Location;

    /// <summary>The node shown, or null while <see cref="Location"/> is being found again after a
    /// reload, or when it no longer exists.</summary>
    public BrowseNode? Node => Load.Node;

    /// <summary>The code, document or markup the tab shows, or an empty document while it shows none.</summary>
    public DecompiledDocument Document => Load.Document;

    /// <summary>The rows the tab shows, or null while it shows no grid.</summary>
    public MetadataGrid? Grid => (Content as TabContent.Table)?.Grid;

    /// <summary>The picture the tab shows, or null while it shows none.</summary>
    public ResourceImage? Image => (Content as TabContent.Picture)?.Image;

    /// <summary>The diagram the tab shows, or null while it shows none.</summary>
    public TypeDiagram? Diagram => (Content as TabContent.Diagram)?.Types;

    /// <summary>The comparison the tab shows, or null while it shows none.</summary>
    public AssemblyComparison? Comparison => (Content as TabContent.Comparison)?.Assemblies;

    /// <summary>The export's report the tab shows, or null while it shows none.</summary>
    public ExportReport? Report => (Content as TabContent.Report)?.Export;

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

    /// <summary>Counts up whenever the tab is pointed somewhere, its load starts again or loses its node, the diagram
    /// it shows gains or loses a card, or the comparison it shows swaps its sides or takes in more or less; it never
    /// counts down.</summary>
    public int Changes => _document.Changes;

    /// <summary>The name of what the tab shows: its node's, kept by what it shows while the node is away, or
    /// its diagram's or comparison's own; null for a new tab or a report.</summary>
    public string? Title => _document.Title;

    /// <summary>The full identity of what the tab shows, or null for a new tab.</summary>
    public string? Tooltip => Node is not null ? NodeText.Tooltip(Node) : Title;

    /// <summary>Changes whenever the tab is pointed somewhere, its load starts again, or it loses its node.</summary>
    public int Version => _document.Version;
}
