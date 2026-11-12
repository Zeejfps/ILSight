using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;
using ILSight.Exporting;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>What is done to the open tabs and to each of them: opening, closing and choosing among them, what
/// a tab shows, and the load that brings it.</summary>
public static class Tabs
{
    /// <summary>Cancels the tab's work and closes it. The tab stops being active if it was.</summary>
    public static void Remove(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.TryRemove(tab))
        {
            return;
        }

        tab.Work.Close();
        Tabs.Cancel(tab);
        tabs.ClosedChanges += tab.Changes;
        if (tab == tabs.Active)
        {
            tabs.LastWorkedIn.Value = null;
        }

        if (tab == tabs.Preview)
        {
            tabs.Previewing.Value = null;
        }

        if (tab == tabs.LastDiagram)
        {
            tabs.LastDiagram = null;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the active tab, and the diagram tab when it shows a diagram.</summary>
    public static void MakeActive(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be active.");
        }

        tabs.LastWorkedIn.Value = tab;
        if (tab.Diagram is not null)
        {
            tabs.LastDiagram = tab;
        }
    }

    /// <summary>Makes <paramref name="tab"/> the preview tab; the one before it stays open, kept.</summary>
    public static void MakePreview(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
        {
            throw new InvalidOperationException("Only an open tab can be the preview tab.");
        }

        tabs.Previewing.Value = tab;
    }

    /// <summary>Keeps <paramref name="tab"/> open: it is no longer the preview tab, if it was.</summary>
    public static void Keep(DocumentTabs tabs, DocumentTab tab)
    {
        if (tab != tabs.Preview)
        {
            return;
        }

        tabs.Previewing.Value = null;
    }

    public static bool IsOpen(DocumentTabs tabs, DocumentTab tab)
    {
        return tabs.Open.Contains(tab);
    }

    /// <summary><paramref name="tab"/> if it is still open, else null.</summary>
    public static DocumentTab? KeepIfOpen(DocumentTabs tabs, DocumentTab? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (!tabs.Open.Contains(tab))
        {
            return null;
        }

        return tab;
    }

    /// <summary>Keeps where the view of what the tab shows stands, for <see cref="DocumentTab.Position"/>. Not counted
    /// in <see cref="DocumentTab.Changes"/>: it moves with every scroll, so it is saved with whatever else is, and on exit,
    /// as the window's bounds are.</summary>
    public static void KeepPosition(DocumentTab tab, ViewPosition position)
    {
        tab.ViewPosition = position;
    }

    internal static int CountContentChanges(DocumentTab tab)
    {
        var diagram = tab.Diagram;
        if (diagram is not null)
        {
            return diagram.Version;
        }

        var comparison = tab.Comparison;
        if (comparison is not null)
        {
            return comparison.Version;
        }

        return 0;
    }

    /// <summary>Whether the tab shows <paramref name="node"/>, done loading, as <paramref name="language"/>
    /// and <paramref name="options"/> would show it.</summary>
    public static bool IsShowing(DocumentTab tab, BrowseNode node, DecompilationLanguage language, DecompilerOptions options)
    {
        if (!ReferenceEquals(node, tab.Node))
        {
            return false;
        }

        if (tab.IsBusy)
        {
            return false;
        }

        if (!NodeContentLoader.DependsOnLanguage(node))
        {
            return true;
        }

        return IsCurrent(tab, language, options);
    }

    /// <summary>Whether what the tab shows was decompiled in <paramref name="language"/> with options
    /// that produce the same code as <paramref name="options"/>.</summary>
    public static bool IsCurrent(DocumentTab tab, DecompilationLanguage language, DecompilerOptions options)
    {
        if (language != tab.Language)
        {
            return false;
        }

        return DecompilerToggles.ProducesSameCode(tab.Options, options, language);
    }

    /// <summary>Points the tab at <paramref name="node"/>, cancelling what was in flight, and shows it
    /// loading until its <see cref="DocumentTab.Work"/> finishes; keeps <paramref name="arrival"/> until then.</summary>
    public static WorkTicket StartLoading(DocumentTab tab, BrowseNode node, DecompilationLanguage language, DecompilerOptions options, Arrival arrival)
    {
        PointElsewhere(tab);
        var load = tab.Work.Start();
        tab.Arrival = arrival;
        tab.Language = language;
        tab.Options = options;
        tab.Content = new TabContent.Loading(node);
        return load;
    }

    /// <summary>Shows <paramref name="loaded"/>, which the finished load of the tab's node brought.</summary>
    public static void ShowLoaded(DocumentTab tab, NodeContent loaded)
    {
        var node = GetLoadingNode(tab);
        tab.Content = loaded.Match<TabContent>(
            code: code => new TabContent.Code(node, code.Document),
            table: table => new TabContent.Table(node, table.Grid),
            picture: picture => new TabContent.Picture(node, picture.Image, picture.Source));
    }

    /// <summary>Shows that the finished load of the tab's node failed with <paramref name="message"/>.</summary>
    public static void ShowFailed(DocumentTab tab, string message)
    {
        tab.Content = new TabContent.Failed(GetLoadingNode(tab), message);
    }

    /// <summary>The node the tab is loading; only a loading tab can show what its load brought.</summary>
    private static BrowseNode GetLoadingNode(DocumentTab tab)
    {
        if (tab.Content is not TabContent.Loading loading)
        {
            throw new InvalidOperationException("Only a loading tab can show what its load brought.");
        }

        return loading.Node;
    }

    /// <summary>Where the view was to stand once the load arrived, given when it started; forgotten once taken.</summary>
    public static Arrival TakeArrival(DocumentTab tab)
    {
        var arrival = tab.Arrival;
        tab.Arrival = new Arrival();
        return arrival;
    }

    /// <summary>Shows <paramref name="report"/>, which no node stands for, cancelling what was in flight.</summary>
    public static void ShowReport(DocumentTab tab, ExportReport report)
    {
        PointElsewhere(tab);
        tab.Content = new TabContent.Report(report);
    }

    /// <summary>Shows <paramref name="diagram"/>, titled <paramref name="title"/>, cancelling what was in flight.</summary>
    public static void ShowDiagram(DocumentTab tab, string title, TypeDiagram diagram)
    {
        PointElsewhere(tab);
        tab.Content = new TabContent.Diagram(diagram, title);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/>, cancelling what was in flight.</summary>
    public static void ShowComparison(DocumentTab tab, string title, AssemblyComparison comparison)
    {
        PointElsewhere(tab);
        tab.Content = new TabContent.Comparison(comparison, title);
    }

    /// <summary>Shows nothing, as a new tab does, cancelling what was in flight.</summary>
    public static void Clear(DocumentTab tab)
    {
        PointElsewhere(tab);
        tab.Content = new TabContent.Empty();
    }

    /// <summary>Lets go of the node while its assembly reloads, keeping <paramref name="location"/> to find it again.</summary>
    public static void ShowReloading(DocumentTab tab, NodePath location)
    {
        ReleaseNode(tab, new TabContent.Reloading(location, tab.Title));
    }

    /// <summary>Lets go of the node, which the reloaded assemblies no longer hold at <paramref name="location"/>.</summary>
    public static void ShowGone(DocumentTab tab, NodePath location)
    {
        ReleaseNode(tab, new TabContent.Gone(location, tab.Title));
    }

    private static void ReleaseNode(DocumentTab tab, TabContent content)
    {
        PointElsewhere(tab);
        tab.Content = content;
    }

    /// <summary>Shows the tab, titled <paramref name="title"/>, waiting for the node at
    /// <paramref name="location"/>, which it showed in <paramref name="language"/> when the documents were
    /// saved, to be found once the assemblies are listed.</summary>
    public static void WaitForNode(DocumentTab tab, NodePath location, string? title, DecompilationLanguage language)
    {
        PointElsewhere(tab);
        tab.Language = language;
        tab.Content = new TabContent.Reloading(location, title);
    }

    /// <summary>Shows the tab, titled <paramref name="title"/>, waiting for the diagram it showed when the
    /// documents were saved to be read once the assemblies are listed.</summary>
    public static void WaitForDiagram(DocumentTab tab, string? title)
    {
        WaitForDocument(tab, title);
    }

    /// <summary>Shows the tab, titled <paramref name="title"/>, waiting for the assemblies the comparison it
    /// showed when the documents were saved compared to be listed.</summary>
    public static void WaitForComparison(DocumentTab tab, string? title)
    {
        WaitForDocument(tab, title);
    }

    private static void WaitForDocument(DocumentTab tab, string? title)
    {
        PointElsewhere(tab);
        tab.Content = new TabContent.Reopening(title);
    }

    /// <summary>Counts the tab as pointed somewhere else, past what its diagram counted, and cancels
    /// what was in flight.</summary>
    private static void PointElsewhere(DocumentTab tab)
    {
        tab.PointedChanges = tab.Changes + 1;
        tab.Version++;
        Cancel(tab);
    }

    /// <summary>Cancels what is in flight for what the tab shows: its load, its diagram's reading and
    /// layout, or its comparison, the diagram or comparison closed for good.</summary>
    public static void Cancel(DocumentTab tab)
    {
        tab.Work.Cancel();
        if (tab.Diagram is not null)
        {
            tab.Diagram.Work.Close();
        }

        if (tab.Comparison is not null)
        {
            tab.Comparison.Work.Close();
        }
    }
}
