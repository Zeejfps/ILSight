using ILSight.Core;
using ILSight.AssemblyArea;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>Which document a tab is (the node it is pointed at, a diagram, a comparison, an export's report, a saved
/// document it waits for, or nothing) and the load of its node. <see cref="Navigation"/>'s, which keeps its rule over
/// it: <see cref="DocumentTabs"/>, which the navigation holds privately, keeps each tab's, and the tab handed out
/// (<see cref="DocumentTab"/>) only reads it. Used on the UI thread only.</summary>
public sealed class TabDocument
{
    /// <summary>The tab's work, which each of its loads runs as: one started cancels the one before; once the tab is
    /// closed, what is in flight is cancelled and its result dropped.</summary>
    private readonly LatestWork _work = new();

    /// <summary>What the tab shows when it is not a node's: a diagram, a comparison, a report, a saved diagram or
    /// comparison it waits for, or nothing; null while it is pointed at a node, which <see cref="_load"/> shows.</summary>
    private TabContent? _shown = new TabContent.Empty();

    /// <summary>The title of <see cref="_shown"/>, where it has one.</summary>
    private string? _title;

    private TabLoad _load;

    // The file of the assembly the user closed that the diagram or comparison shown reads; null while none is.
    private AssemblyPath? _closedFile;

    /// <summary>What <see cref="Changes"/> had counted when the tab was last pointed somewhere, past what its load,
    /// diagram or comparison counts now.</summary>
    private int _pointedChanges;

    /// <summary>What <see cref="Version"/> had counted when the tab was last pointed somewhere, past what its load
    /// counts now.</summary>
    private int _pointedVersion;

    public TabDocument()
    {
        _load = new TabLoad(_work);
    }

    /// <summary>The load of the node the tab is pointed at; a load of no node while it is pointed at none.</summary>
    public TabLoad Load => _load;

    public TabContent Content => _shown ?? _load.Content;

    /// <summary>The name of what the tab shows: its node's, kept while the node is away, or its diagram's or
    /// comparison's own; null for a new tab or a report.</summary>
    public string? Title => _shown is null ? _load.Title : _title;

    /// <summary>The file name of the closed assembly what the tab shows was read from: its node's
    /// (<see cref="TabLoad.ShowClosed"/>), or one its diagram or comparison reads (<see cref="ShowClosed"/>); null
    /// while none is closed.</summary>
    public string? ClosedAssembly => _shown is null ? _load.ClosedAssembly : _closedFile?.FileName;

    /// <summary>The file of the closed assembly <see cref="ClosedAssembly"/> names, for the tab to read again once
    /// the list holds it again; null while none is closed.</summary>
    public AssemblyPath? ClosedFile => _shown is null ? FindClosedNodeFile() : _closedFile;

    /// <summary>Changes whenever the tab is pointed somewhere, its load starts again, or it loses its node.</summary>
    public int Version => _pointedVersion + _load.Changes;

    /// <summary>Counts up whenever the tab is pointed somewhere, its load starts again or loses its node, the diagram
    /// it shows gains or loses a card, or the comparison it shows swaps its sides or takes in more or less; it never
    /// counts down.</summary>
    public int Changes => _pointedChanges + _load.Changes + CountContentChanges();

    private int CountContentChanges()
    {
        if (_shown is TabContent.Diagram diagram)
        {
            return diagram.Types.Version;
        }

        if (_shown is TabContent.Comparison comparison)
        {
            return comparison.Assemblies.Version;
        }

        return 0;
    }

    private AssemblyPath? FindClosedNodeFile()
    {
        if (_load.ClosedAssembly is null)
        {
            return null;
        }

        return _load.Location?.FilePath;
    }

    /// <summary>Keeps what the tab shows of a diagram or a comparison that reads <paramref name="file"/>, which the user
    /// closed, and reads it no more: what was in flight for it is cancelled for good. A tab showing anything else, or
    /// shown closed already, stays as it is (a node's load is closed through <see cref="TabLoad.ShowClosed"/>).</summary>
    public void ShowClosed(AssemblyPath file)
    {
        if (_closedFile is not null)
        {
            return;
        }

        if (!ReadsDocumentOf(file))
        {
            return;
        }

        _closedFile = file;
        _pointedChanges++;
        _pointedVersion++;
        Cancel();
    }

    private bool ReadsDocumentOf(AssemblyPath file)
    {
        if (_shown is TabContent.Diagram diagram)
        {
            return diagram.Types.HasTypesOf(file);
        }

        if (_shown is TabContent.Comparison comparison)
        {
            return comparison.Assemblies.HasSide(file);
        }

        return false;
    }

    /// <summary>Points the tab at <paramref name="node"/>, cancelling what was in flight, and shows it loading in
    /// <paramref name="language"/> with <paramref name="options"/> until the load finishes; the view is to stand where
    /// <paramref name="arrival"/> says then. Returns the ticket the load's result lands with.</summary>
    public WorkTicket PointAt(BrowseNode node, DecompilationLanguage language, DecompilerOptions options, Arrival arrival)
    {
        PointElsewhere();
        _shown = null;
        _load = new TabLoad(_work, node, language, options, arrival);
        return _load.Start();
    }

    /// <summary>Shows <paramref name="report"/>, which no node stands for, cancelling what was in flight.</summary>
    public void ShowReport(ExportReport report)
    {
        Show(new TabContent.Report(report), title: null);
    }

    /// <summary>Shows <paramref name="diagram"/>, titled <paramref name="title"/> and given <paramref name="id"/>,
    /// cancelling what was in flight.</summary>
    public void ShowDiagram(TypeDiagram diagram, string title, int id)
    {
        Show(new TabContent.Diagram { Types = diagram, Title = title, Id = id }, title);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/> and given <paramref name="id"/>,
    /// cancelling what was in flight.</summary>
    public void ShowComparison(AssemblyComparison comparison, string title, int id)
    {
        Show(new TabContent.Comparison { Assemblies = comparison, Title = title, Id = id }, title);
    }

    /// <summary>Shows nothing, as a new tab does, cancelling what was in flight.</summary>
    public void Clear()
    {
        Show(new TabContent.Empty(), title: null);
    }

    /// <summary>Shows the tab, titled <paramref name="title"/>, waiting for the diagram or comparison it showed when the
    /// documents were saved to be read again once the assemblies are listed.</summary>
    public void WaitForDocument(string? title)
    {
        Show(new TabContent.Reopening(title), title);
    }

    /// <summary>Shows the tab, titled <paramref name="title"/>, waiting for the node at <paramref name="location"/>,
    /// which it showed in <paramref name="language"/> when the documents were saved, to be found once the assemblies
    /// are listed.</summary>
    public void WaitForNode(NodePath location, string? title, DecompilationLanguage language)
    {
        PointElsewhere();
        _shown = null;
        _load = new TabLoad(_work, location, title, language);
    }

    private void Show(TabContent shown, string? title)
    {
        PointElsewhere();
        _shown = shown;
        _title = title;
        _load = new TabLoad(_work);
    }

    /// <summary>Counts the tab as pointed somewhere else, past what it counted, and cancels what was in flight.</summary>
    private void PointElsewhere()
    {
        _pointedChanges = Changes + 1;
        _pointedVersion = Version + 1;
        Cancel();
        _closedFile = null;
    }

    /// <summary>Cancels what is in flight for what the tab shows: its load, its diagram's reading and layout, or its
    /// comparison, the diagram or comparison closed for good.</summary>
    public void Cancel()
    {
        _load.Cancel();
        if (_shown is TabContent.Diagram diagram)
        {
            diagram.Types.Close();
        }

        if (_shown is TabContent.Comparison comparison)
        {
            comparison.Assemblies.Work.Close();
        }
    }

    /// <summary>Cancels what is in flight for good, as the tab closes: its result is dropped, and so is that of work
    /// started later.</summary>
    public void Close()
    {
        _work.Close();
        Cancel();
    }
}
