using ILSight.Core;
using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;
using ILSight.ExportArea;
using ILSight.AssemblyArea;

namespace ILSight;

/// <summary>What a tab shows: nothing, its node while it decompiles, the node's code, grid or picture,
/// an export's report, a diagram or a comparison of its own, or why it shows none of these.</summary>
public union TabContent(TabContent.Empty, TabContent.Loading, TabContent.Code, TabContent.Table, TabContent.Picture,
    TabContent.Report, TabContent.Diagram, TabContent.Comparison, TabContent.Failed, TabContent.Stopped,
    TabContent.Reloading, TabContent.Reopening, TabContent.Gone, TabContent.NotReloaded, TabContent.Unreadable)
{
    /// <summary>Nothing, as a new tab.</summary>
    public sealed record Empty;

    /// <summary><see cref="Node"/>, while it decompiles or its grid or picture is read.</summary>
    public sealed record Loading(BrowseNode Node);

    /// <summary><see cref="Node"/>'s decompiled code.</summary>
    public sealed record Code
    {
        public required BrowseNode Node { get; init; }

        public required DecompiledDocument Document { get; init; }
    }

    /// <summary>The rows of <see cref="Node"/>, which shows a grid.</summary>
    public sealed record Table
    {
        public required BrowseNode Node { get; init; }

        public required MetadataGrid Grid { get; init; }
    }

    /// <summary>The picture of <see cref="Node"/>, which shows one, with its markup when it is stored as such, such as SVG.</summary>
    public sealed record Picture
    {
        public required BrowseNode Node { get; init; }

        public required ResourceImage Image { get; init; }

        public required DecompiledDocument? Source { get; init; }
    }

    /// <summary>The report of an export, which the screen writes out.</summary>
    public sealed record Report(ExportReport Export);

    /// <summary>A diagram of types, which grows as types are shown in it, what its tab is titled, and the id the
    /// navigation gave it, never reused, which the history finds it by.</summary>
    public sealed record Diagram
    {
        public required TypeDiagram Types { get; init; }

        public required TabTitle Title { get; init; }

        public required int Id { get; init; }
    }

    /// <summary>What changed in the API from one assembly to another, what its tab is titled, and the id the navigation
    /// gave it, never reused, which the history finds it by.</summary>
    public sealed record Comparison
    {
        public required AssemblyComparison Assemblies { get; init; }

        public required TabTitle Title { get; init; }

        public required int Id { get; init; }
    }

    /// <summary>Decompiling <see cref="Node"/> failed with <see cref="Error"/>.</summary>
    public sealed record Failed
    {
        public required BrowseNode Node { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Nothing of <see cref="Node"/>: its assembly was closed while it decompiled or its grid or picture was
    /// read, and the read stopped there (<see cref="TabLoad.ShowClosed"/>).</summary>
    public sealed record Stopped(BrowseNode Node);

    /// <summary>The tab waits to find the node at <see cref="Location"/> again: its assembly is reloading,
    /// or the saved documents are reopening. <see cref="Title"/> is the node's name as it was last known,
    /// or null where the saved documents kept none.</summary>
    public sealed record Reloading
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }
    }

    /// <summary>The saved diagram or comparison waits for the assemblies to be listed to be read again,
    /// under the title it was saved with, if any.</summary>
    public sealed record Reopening(TabTitle? Title);

    /// <summary>The reloaded assemblies hold nothing at <see cref="Location"/> any more; <see cref="Title"/>
    /// is the node's name as it was last known, if it was.</summary>
    public sealed record Gone
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }
    }

    /// <summary>The reload that was to find the node at <see cref="Location"/> again ended with no answer, for
    /// <see cref="End"/>: nothing was looked for, so the node is not said gone, and the place is kept for the next reload
    /// to look for again; <see cref="Title"/> is the node's name as it was last known, if it was.</summary>
    public sealed record NotReloaded
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }

        public required ReloadEnd End { get; init; }
    }

    /// <summary>The node at <see cref="Location"/> is in <see cref="Module"/>, a file whose type system the library
    /// could not read, for <see cref="Why"/>, so it was not found again; <see cref="Title"/> is the node's name as it
    /// was last known, if it was.</summary>
    public sealed record Unreadable
    {
        public required NodePath Location { get; init; }

        public required string? Title { get; init; }

        /// <summary>The module's file name.</summary>
        public required string Module { get; init; }

        public required Exception Why { get; init; }
    }
}
