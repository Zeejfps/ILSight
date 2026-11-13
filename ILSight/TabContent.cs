using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;
using ILSight.ExportArea;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>What a tab shows: nothing, its node while it decompiles, the node's code, grid or picture,
/// an export's report, a diagram or a comparison of its own, or why it shows none of these.</summary>
public abstract record TabContent
{
    private TabContent()
    {
    }

    /// <summary>Nothing, as a new tab.</summary>
    public sealed record Empty : TabContent;

    /// <summary><see cref="Node"/>, while it decompiles or its grid or picture is read.</summary>
    public sealed record Loading(BrowseNode Node) : TabContent;

    /// <summary><see cref="Node"/>'s decompiled code.</summary>
    public sealed record Code : TabContent
    {
        public required BrowseNode Node { get; init; }

        public required DecompiledDocument Document { get; init; }
    }

    /// <summary>The rows of <see cref="Node"/>, which shows a grid.</summary>
    public sealed record Table : TabContent
    {
        public required BrowseNode Node { get; init; }

        public required MetadataGrid Grid { get; init; }
    }

    /// <summary>The picture of <see cref="Node"/>, which shows one, with its markup when it is stored as such, such as SVG.</summary>
    public sealed record Picture : TabContent
    {
        public required BrowseNode Node { get; init; }

        public required ResourceImage Image { get; init; }

        public required DecompiledDocument? Source { get; init; }
    }

    /// <summary>The report of an export, which the screen writes out.</summary>
    public sealed record Report(ExportReport Export) : TabContent;

    /// <summary>A diagram of types, which grows as types are shown in it, what its tab is titled, and the id the
    /// navigation gave it, never reused, which the history finds it by.</summary>
    public sealed record Diagram : TabContent
    {
        public required TypeDiagram Types { get; init; }

        public required string Title { get; init; }

        public required int Id { get; init; }
    }

    /// <summary>What changed in the API from one assembly to another, what its tab is titled, and the id the navigation
    /// gave it, never reused, which the history finds it by.</summary>
    public sealed record Comparison : TabContent
    {
        public required AssemblyComparison Assemblies { get; init; }

        public required string Title { get; init; }

        public required int Id { get; init; }
    }

    /// <summary>Decompiling <see cref="Node"/> failed with <see cref="Message"/>.</summary>
    public sealed record Failed : TabContent
    {
        public required BrowseNode Node { get; init; }

        public required string Message { get; init; }
    }

    /// <summary>Nothing of <see cref="Node"/>: its assembly was closed while it decompiled or its grid or picture was
    /// read, and the read stopped there (<see cref="TabLoad.ShowClosed"/>).</summary>
    public sealed record Stopped(BrowseNode Node) : TabContent;

    /// <summary>The tab waits to find the node at <see cref="Location"/> again: its assembly is reloading,
    /// or the saved documents are reopening. <see cref="Title"/> is the node's name as it was last known,
    /// or null where the saved documents kept none.</summary>
    public sealed record Reloading : TabContent
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }
    }

    /// <summary>The saved diagram or comparison waits for the assemblies to be listed to be read again,
    /// under the title it was saved with, if any.</summary>
    public sealed record Reopening(string? Title) : TabContent;

    /// <summary>The reloaded assemblies hold nothing at <see cref="Location"/> any more; <see cref="Title"/>
    /// is the node's name as it was last known, if it was.</summary>
    public sealed record Gone : TabContent
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }
    }

    /// <summary>The node at <see cref="Location"/> is in <see cref="Module"/>, a file whose type system the library
    /// could not read, for <see cref="Why"/>, so it was not found again; <see cref="Title"/> is the node's name as it
    /// was last known, if it was.</summary>
    public sealed record Unreadable : TabContent
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }

        /// <summary>The module's file name.</summary>
        public required string Module { get; init; }

        public required string Why { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<Empty, TResult> empty, Func<Loading, TResult> loading, Func<Code, TResult> code,
        Func<Table, TResult> table, Func<Picture, TResult> picture, Func<Report, TResult> report,
        Func<Diagram, TResult> diagram, Func<Comparison, TResult> comparison, Func<Failed, TResult> failed,
        Func<Stopped, TResult> stopped, Func<Reloading, TResult> reloading, Func<Reopening, TResult> reopening, Func<Gone, TResult> gone,
        Func<Unreadable, TResult> unreadable)
    {
        return this switch
        {
            Empty c => empty(c),
            Loading c => loading(c),
            Code c => code(c),
            Table c => table(c),
            Picture c => picture(c),
            Report c => report(c),
            Diagram c => diagram(c),
            Comparison c => comparison(c),
            Failed c => failed(c),
            Stopped c => stopped(c),
            Reloading c => reloading(c),
            Reopening c => reopening(c),
            Gone c => gone(c),
            Unreadable c => unreadable(c),
            _ => throw new UnreachableException($"TabContent.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which it
    /// is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Empty> empty, Action<TState, Loading> loading,
        Action<TState, Code> code, Action<TState, Table> table, Action<TState, Picture> picture,
        Action<TState, Report> report, Action<TState, Diagram> diagram, Action<TState, Comparison> comparison,
        Action<TState, Failed> failed, Action<TState, Stopped> stopped, Action<TState, Reloading> reloading, Action<TState, Reopening> reopening,
        Action<TState, Gone> gone, Action<TState, Unreadable> unreadable)
    {
        switch (this)
        {
            case Empty c:
                empty(state, c);
                return;
            case Loading c:
                loading(state, c);
                return;
            case Code c:
                code(state, c);
                return;
            case Table c:
                table(state, c);
                return;
            case Picture c:
                picture(state, c);
                return;
            case Report c:
                report(state, c);
                return;
            case Diagram c:
                diagram(state, c);
                return;
            case Comparison c:
                comparison(state, c);
                return;
            case Failed c:
                failed(state, c);
                return;
            case Stopped c:
                stopped(state, c);
                return;
            case Reloading c:
                reloading(state, c);
                return;
            case Reopening c:
                reopening(state, c);
                return;
            case Gone c:
                gone(state, c);
                return;
            case Unreadable c:
                unreadable(state, c);
                return;
            default:
                throw new UnreachableException($"TabContent.Switch does not handle {GetType().Name}.");
        }
    }
}
