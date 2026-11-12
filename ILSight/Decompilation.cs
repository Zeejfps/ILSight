using ILSight.Core;
using ILSight.Bookmarking;

namespace ILSight;

/// <summary>Pointing a tab at what it is to show, decompiled or read as a grid or picture: the tab shows it
/// loading at once, and <see cref="DecompileIntoTabHandler"/> brings it in with the ticket this returns; and
/// commanding the view where to stand in it once it is there.</summary>
public static class Decompilation
{
    /// <summary>Points <paramref name="tab"/> at <paramref name="node"/>, shown loading from now on in
    /// <paramref name="language"/> with <paramref name="options"/>, cancelling what was in flight, and returns what
    /// <see cref="DecompileIntoTabHandler"/> needs to bring it in, after which the view stands where
    /// <paramref name="arrival"/> says. When the tab already shows <paramref name="node"/> as the language and options
    /// would, only commands the view, through <paramref name="viewCommands"/>, to stand there, and returns null.</summary>
    public static DecompileTicket? StartDecompiling(Queue<ViewCommand> viewCommands, DocumentTab tab, BrowseNode node, DecompilationLanguage language,
        DecompilerOptions options, Arrival arrival = default)
    {
        if (Tabs.IsShowing(tab, node, language, options))
        {
            Arrive(viewCommands, tab, arrival);
            return null;
        }

        var work = Tabs.StartLoading(tab, node, language, options, arrival);
        return new DecompileTicket { Tab = tab, Node = node, Language = language, Options = options, Work = work };
    }

    /// <summary>Commands the view, through <paramref name="viewCommands"/>, to return to the arrival's position, else
    /// to reveal the grid's row or the line its bookmark marks, else the node's definition.</summary>
    public static void Arrive(Queue<ViewCommand> viewCommands, DocumentTab tab, Arrival arrival)
    {
        var position = arrival.Position;
        if (position is not null)
        {
            viewCommands.Enqueue(new ViewCommand.RestoreView(tab, position.Value));
            return;
        }
        if (tab.Grid is not null)
        {
            RevealRow(viewCommands, tab, arrival.Row);
            return;
        }
        var line = FindLineToReveal(tab, arrival.Target);
        if (line is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.RevealLine(tab, line.Value));
    }

    private static void RevealRow(Queue<ViewCommand> viewCommands, DocumentTab tab, int? row)
    {
        if (row is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.RevealRow(tab, row.Value));
    }

    /// <summary>The line <paramref name="target"/> marks, else the node's definition; null when there is neither.</summary>
    private static int? FindLineToReveal(DocumentTab tab, Bookmark? target)
    {
        var node = tab.Node;
        if (node is null)
        {
            return null;
        }

        var marked = FindMarkedLine(tab, target, node);
        if (marked is not null)
        {
            return marked;
        }

        return Documents.FindDefinitionLine(tab.Document, node);
    }

    private static int? FindMarkedLine(DocumentTab tab, Bookmark? target, BrowseNode node)
    {
        if (target is null)
        {
            return null;
        }

        return Bookmarks.FindLine(target, tab.Document, node);
    }

    /// <summary>Points <paramref name="tab"/> again at the node it shows, as <see cref="StartDecompiling"/> does, if
    /// it was shown in another language, or with other options, than <paramref name="language"/> and
    /// <paramref name="options"/>, keeping the view where it was when the language is the same. Null when it needs
    /// no decompiling again.</summary>
    public static DecompileTicket? StartDecompilingAgainIfOutdated(Queue<ViewCommand> viewCommands, DocumentTab tab, DecompilationLanguage language,
        DecompilerOptions options)
    {
        var node = tab.Node;
        if (node is null)
        {
            return null;
        }

        if (!NodeContentLoader.DependsOnLanguage(node))
        {
            return null;
        }

        if (Tabs.IsCurrent(tab, language, options))
        {
            return null;
        }

        var position = Navigation.KeepPosition(tab.Position, tab.Language, language);
        return StartDecompiling(viewCommands, tab, node, language, options, new Arrival(position));
    }
}
