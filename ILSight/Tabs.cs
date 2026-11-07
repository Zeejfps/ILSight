using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight;

/// <summary>What is done to the open tabs and to each of them: opening, closing and choosing among them, what
/// a tab shows, and the load that brings it.</summary>
public static class Tabs
{
    public static void Add(DocumentTabs tabs, DocumentTab tab)
    {
        tabs.Open.Add(tab);
        tabs.OwnChanges++;
    }

    /// <summary>Cancels the tab's work and closes it. The tab stops being active if it was.</summary>
    public static void Remove(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Remove(tab))
            return;
        Tabs.Cancel(tab);
        tabs.ClosedChanges += tab.Changes;
        tabs.OwnChanges++;
        if (tab == tabs.Active)
            tabs.Active = null;
        if (tab == tabs.Preview)
            tabs.Preview = null;
        if (tab == tabs.LastDiagram)
            tabs.LastDiagram = null;
    }

    /// <summary>Makes <paramref name="tab"/> the active tab, and the diagram tab when it shows a diagram.</summary>
    public static void MakeActive(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
            throw new InvalidOperationException("Only an open tab can be active.");
        if (tab != tabs.Active)
            tabs.OwnChanges++;
        tabs.Active = tab;
        if (tab.Diagram != null)
            tabs.LastDiagram = tab;
    }

    /// <summary>Makes <paramref name="tab"/> the preview tab; the one before it stays open, kept.</summary>
    public static void MakePreview(DocumentTabs tabs, DocumentTab tab)
    {
        if (!tabs.Open.Contains(tab))
            throw new InvalidOperationException("Only an open tab can be the preview tab.");
        if (tab != tabs.Preview)
            tabs.OwnChanges++;
        tabs.Preview = tab;
    }

    /// <summary>Keeps <paramref name="tab"/> open: it is no longer the preview tab, if it was.</summary>
    public static void Keep(DocumentTabs tabs, DocumentTab tab)
    {
        if (tab != tabs.Preview)
            return;
        tabs.Preview = null;
        tabs.OwnChanges++;
    }

    public static bool IsOpen(DocumentTabs tabs, DocumentTab tab)
    {
        return tabs.Open.Contains(tab);
    }

    /// <summary><paramref name="tab"/> if it is still open, else null.</summary>
    public static DocumentTab? KeepIfOpen(DocumentTabs tabs, DocumentTab? tab)
    {
        if (tab == null)
            return null;
        if (!tabs.Open.Contains(tab))
            return null;
        return tab;
    }

    /// <summary>Keeps where the view of what the tab shows stands, for <see cref="Position"/>. Not counted
    /// in <see cref="Changes"/>: it moves with every scroll, so it is saved with whatever else is, and on exit,
    /// as the window's bounds are.</summary>
    public static void KeepPosition(DocumentTab tab, ViewPosition position)
    {
        tab.ViewPosition = position;
    }

    internal static int CountContentChanges(DocumentTab tab)
    {
        var diagram = tab.Diagram;
        if (diagram != null)
            return diagram.Version;
        var comparison = tab.Comparison;
        if (comparison != null)
            return comparison.Version;
        return 0;
    }

    /// <summary>Whether the tab shows <paramref name="node"/>, done loading, as <paramref name="language"/>
    /// and <paramref name="options"/> would show it.</summary>
    public static bool IsShowing(DocumentTab tab, BrowseNode node, DecompilationLanguage language, DecompilerOptions options)
    {
        if (!ReferenceEquals(node, tab.Node))
            return false;
        if (tab.IsBusy)
            return false;
        if (!NodeContentLoader.DependsOnLanguage(node))
            return true;
        return IsCurrent(tab, language, options);
    }

    /// <summary>Whether what the tab shows was decompiled in <paramref name="language"/> with options
    /// that produce the same code as <paramref name="options"/>.</summary>
    public static bool IsCurrent(DocumentTab tab, DecompilationLanguage language, DecompilerOptions options)
    {
        if (language != tab.Language)
            return false;
        return tab.Options.ProducesSameCode(options, language);
    }

    /// <summary>Points the tab at <paramref name="node"/>, cancelling what was in flight, and shows it
    /// loading until <see cref="FinishLoading"/>; keeps <paramref name="arrival"/> until then.</summary>
    public static LoadTicket StartLoading(DocumentTab tab, BrowseNode node, DecompilationLanguage language, DecompilerOptions options, Arrival arrival)
    {
        PointElsewhere(tab);
        var work = new CancellationTokenSource();
        tab.Work = work;
        tab.Arrival = arrival;
        tab.Title = NodeText.Name(node);
        tab.Language = language;
        tab.Options = options;
        tab.Content = new TabContent.Loading(node);
        return new LoadTicket { Work = work };
    }

    /// <summary>Ends the load <paramref name="load"/> was given for. False when the tab was pointed
    /// elsewhere or cancelled since, and what the load brought should be dropped.</summary>
    public static bool FinishLoading(DocumentTab tab, LoadTicket load)
    {
        load.Work.Dispose();
        if (tab.Work != load.Work)
            return false;
        tab.Work = null;
        return true;
    }

    /// <summary>Shows <paramref name="loaded"/>, which the finished load of the tab's node brought.</summary>
    public static void ShowLoaded(DocumentTab tab, NodeContent loaded)
    {
        var node = GetLoadingNode(tab);
        tab.Content = loaded switch
        {
            GridContent grid => new TabContent.Table(node, grid.Grid),
            ImageContent image => new TabContent.Picture(node, image.Image, image.Source),
            DocumentContent document => new TabContent.Code(node, document.Document),
            _ => throw new InvalidOperationException($"No tab shows {loaded.GetType().Name}."),
        };
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
            throw new InvalidOperationException("Only a loading tab can show what its load brought.");
        return loading.Node;
    }

    /// <summary>Where the view was to stand once the load arrived, given when it started; forgotten once taken.</summary>
    public static Arrival TakeArrival(DocumentTab tab)
    {
        var arrival = tab.Arrival;
        tab.Arrival = new Arrival();
        return arrival;
    }

    /// <summary>Shows <paramref name="document"/>, which no node stands for, titled
    /// <paramref name="title"/>, cancelling what was in flight.</summary>
    public static void ShowReport(DocumentTab tab, ExportReport report)
    {
        PointElsewhere(tab);
        tab.Title = null;
        tab.Content = new TabContent.Report(report);
    }

    /// <summary>Shows <paramref name="diagram"/>, titled <paramref name="title"/>, cancelling what was in flight.</summary>
    public static void ShowDiagram(DocumentTab tab, string title, TypeDiagram diagram)
    {
        PointElsewhere(tab);
        tab.Title = title;
        tab.Content = new TabContent.Diagram(diagram);
    }

    /// <summary>Shows <paramref name="comparison"/>, titled <paramref name="title"/>, cancelling what was in flight.</summary>
    public static void ShowComparison(DocumentTab tab, string title, AssemblyComparison comparison)
    {
        PointElsewhere(tab);
        tab.Title = title;
        tab.Content = new TabContent.Comparison(comparison);
    }

    /// <summary>Shows nothing, as a new tab does, cancelling what was in flight.</summary>
    public static void Clear(DocumentTab tab)
    {
        PointElsewhere(tab);
        tab.Title = null;
        tab.Content = new TabContent.Empty();
    }

    /// <summary>Lets go of the node while its assembly reloads, keeping <paramref name="location"/> to find it again.</summary>
    public static void ShowReloading(DocumentTab tab, NodePath location)
    {
        ReleaseNode(tab, new TabContent.Reloading(location));
    }

    /// <summary>Lets go of the node, which the reloaded assemblies no longer hold at <paramref name="location"/>.</summary>
    public static void ShowGone(DocumentTab tab, NodePath location)
    {
        ReleaseNode(tab, new TabContent.Gone(location));
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
        tab.Title = title;
        tab.Language = language;
        tab.Content = new TabContent.Reloading(location);
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
        tab.Title = title;
        tab.Content = new TabContent.Reopening();
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
        tab.Work?.Cancel();
        tab.Work = null;
        if (tab.Diagram != null)
            Diagrams.CloseDiagram(tab.Diagram);
        if (tab.Comparison != null)
            Comparisons.CloseComparison(tab.Comparison);
    }
}
