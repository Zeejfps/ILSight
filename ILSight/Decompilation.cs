using ILSight.Core;
using ILSight.BookmarkArea;

namespace ILSight;

/// <summary>Decompiling what a tab shows again in another language or with other options: the tab shows it loading
/// at once, and <see cref="DecompileIntoTabHandler"/> brings it in with the ticket this returns; and commanding the
/// view where to stand in what a tab shows once it is there. Pointing a tab at a node is
/// <see cref="Navigation"/>'s.</summary>
public static class Decompilation
{
    /// <summary>Commands the view, through <paramref name="viewCommands"/>, to return to the arrival's position, else
    /// to reveal the grid's row or the line its bookmark marks, else the node's definition.</summary>
    public static void Arrive(Queue<ViewCommand> viewCommands, DocumentTab tab, Arrival arrival)
    {
        var position = arrival.Position;
        if (position is not null)
        {
            viewCommands.Enqueue(new ViewCommand.RestoreView { Tab = tab, Position = position.Value });
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

        viewCommands.Enqueue(new ViewCommand.RevealLine { Tab = tab, Line = line.Value });
    }

    private static void RevealRow(Queue<ViewCommand> viewCommands, DocumentTab tab, int? row)
    {
        if (row is null)
        {
            return;
        }

        viewCommands.Enqueue(new ViewCommand.RevealRow { Tab = tab, Row = row.Value });
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

    /// <summary>Starts the load of the node <paramref name="tab"/> shows again, as <see cref="TabLoad.StartAgainIfOutdated"/>
    /// does, if it was shown in another language, or with other options, than <paramref name="language"/> and
    /// <paramref name="options"/>. Returns what <see cref="DecompileIntoTabHandler"/> needs to bring it in; null when it
    /// needs no decompiling again, or its assembly was closed (<see cref="TabLoad.ShowClosed"/>).</summary>
    public static DecompileTicket? StartDecompilingAgainIfOutdated(DocumentTab tab, DecompilationLanguage language, DecompilerOptions options)
    {
        var load = tab.Load;
        var work = load.StartAgainIfOutdated(language, options);
        if (work is null)
        {
            return null;
        }

        var node = load.Node ?? throw new InvalidOperationException("A load started again shows its node loading.");
        return new DecompileTicket { Tab = tab, Load = load, Node = node, Language = language, Options = options, Work = work };
    }
}
