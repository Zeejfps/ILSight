using ILSight.Core;

namespace ILSight;

/// <summary>What the open assembly list holds while it is open, which switching to another list changes together:
/// its assemblies, the analyses, the tabs and their history, the tabs reopened from its saved documents, its reload
/// and its opens. The lists and delete dialogs and the toolbar's list menu take it to switch lists in the frame of
/// their input, without the rest of the app's state.</summary>
public sealed class OpenListSession
{
    public required AssemblyList Assemblies { get; init; }

    /// <summary>The symbols being analyzed and what the analyses found.</summary>
    public required AnalyzerTree Analyzer { get; init; }

    /// <summary>The open tabs, the history of what they showed and the newest thing asked for.</summary>
    public required Navigation Navigation { get; init; }

    /// <summary>The tabs reopened from the open list's saved documents that wait for what they showed.</summary>
    public required ReopenedTabs Reopened { get; init; }

    public required Reload Reload { get; init; }

    public required OpenRequests Opens { get; init; }
}
