using ILSight.Core;

namespace ILSight;

/// <summary>Decompiling what a tab points at, or reading its grid or picture, in the background, and
/// commanding the view where to stand in it once it arrives.</summary>
public static class Decompilation
{
    /// <summary>Starts decompiling <paramref name="node"/> in <paramref name="tab"/>, or reading its grid,
    /// cancelling what was in flight, then commands the view to stand where <paramref name="arrival"/> says.
    /// When the tab already shows <paramref name="node"/> as the language and options would, only does the latter.</summary>
    public static void DecompileIntoTab(AppState appState, DocumentTab tab, BrowseNode node, Arrival arrival = default)
    {
        if (Tabs.IsShowing(tab, node, appState.Language, appState.DecompilerOptions))
        {
            Arrive(appState, tab, arrival);
            return;
        }
        var load = Tabs.StartLoading(tab, node, appState.Language, appState.DecompilerOptions, arrival);
        StartDecompiling(appState, tab, node, load);
    }

    private static void StartDecompiling(AppState appState, DocumentTab tab, BrowseNode node, WorkTicket load)
    {
        Background.Start(appState.Work, nameof(DecompileAsync), DecompileAsync(appState, tab, node, load), problem => Errors.Report(appState, problem));
    }

    private static async Task DecompileAsync(AppState appState, DocumentTab tab, BrowseNode node, WorkTicket load)
    {
        var loading = NodeContentLoader.GetContentAsync(appState.Session, node, tab.Language, tab.Options, load.Token);
        await Background.Wait(loading);
        FinishLoading(appState, tab, node, load, loading);
    }

    private static void FinishLoading(AppState appState, DocumentTab tab, BrowseNode node, WorkTicket load, Task<NodeContent> loading)
    {
        if (!tab.Work.TryFinish(load))
        {
            return;
        }

        if (loading.IsCanceled)
        {
            return;
        }

        var error = loading.Exception?.GetBaseException();
        if (error is not null)
        {
            Tabs.ShowFailed(tab, error.Message);
        }
        else
        {
            Tabs.ShowLoaded(tab, loading.Result);
        }

        var arrival = Tabs.TakeArrival(tab);
        var failure = error ?? tab.Document.Error;
        if (failure is not null)
        {
            ReportFailure(appState, node, failure);
            Arrive(appState, tab, new Arrival());
            return;
        }
        Arrive(appState, tab, arrival);
    }

    /// <summary>Reports why <paramref name="node"/> could not be shown, unless its file failed to load,
    /// which its tab tells already.</summary>
    private static void ReportFailure(AppState appState, BrowseNode node, Exception error)
    {
        if (node.Assembly is FailedAssembly)
        {
            return;
        }

        Errors.Report(appState, new AppProblem.DecompilationFailed(BrowseNodes.GetFullName(node), error));
    }

    /// <summary>Commands the view to return to the arrival's position, else to reveal the grid's row or the
    /// line its bookmark marks, else the node's definition.</summary>
    private static void Arrive(AppState appState, DocumentTab tab, Arrival arrival)
    {
        var position = arrival.Position;
        if (position is not null)
        {
            appState.ViewCommands.Enqueue(new ViewCommand.RestoreView(tab, position.Value));
            return;
        }
        if (tab.Grid is not null)
        {
            RevealRow(appState, tab, arrival.Row);
            return;
        }
        var line = GetLineToReveal(tab, arrival.Target);
        if (line < 0)
        {
            return;
        }

        appState.ViewCommands.Enqueue(new ViewCommand.RevealLine(tab, line));
    }

    private static void RevealRow(AppState appState, DocumentTab tab, int? row)
    {
        if (row is null)
        {
            return;
        }

        appState.ViewCommands.Enqueue(new ViewCommand.RevealRow(tab, row.Value));
    }

    /// <summary>The line <paramref name="target"/> marks, else the node's definition, or -1.</summary>
    private static int GetLineToReveal(DocumentTab tab, Bookmark? target)
    {
        var node = tab.Node;
        if (node is null)
        {
            return -1;
        }

        var marked = GetMarkedLine(tab, target, node);
        if (marked >= 0)
        {
            return marked;
        }

        return Documents.GetDefinitionLine(tab.Document, node);
    }

    private static int GetMarkedLine(DocumentTab tab, Bookmark? target, BrowseNode node)
    {
        if (target is null)
        {
            return -1;
        }

        return Bookmarks.GetLine(target, tab.Document, node);
    }

    /// <summary>Decompiles the node <paramref name="tab"/> shows again if it was shown in another language,
    /// or with other options, than the app state's, keeping the view where it was when the language is the same.</summary>
    public static void DecompileAgainIfOutdated(AppState appState, DocumentTab tab)
    {
        var node = tab.Node;
        if (node is null)
        {
            return;
        }

        if (!NodeContentLoader.DependsOnLanguage(node))
        {
            return;
        }

        if (Tabs.IsCurrent(tab, appState.Language, appState.DecompilerOptions))
        {
            return;
        }

        var position = FindPositionToKeep(appState, tab);
        DecompileIntoTab(appState, tab, node, new Arrival(position));
    }

    private static ViewPosition? FindPositionToKeep(AppState appState, DocumentTab tab)
    {
        return Navigation.KeepPosition(tab.Position, tab.Language, appState.Language);
    }
}
