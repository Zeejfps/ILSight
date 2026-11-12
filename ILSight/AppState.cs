using ZGF.Gui.Immediate;
using ZGF.Desktop;
using ILSight.Core;
using ILSight.Localization;
using ILSight.Exporting;
using ILSight.Bookmarking;

namespace ILSight;

/// <summary>
/// The app's data, and which tab, list and history entry are current. What the user can do to it are
/// static functions grouped by feature, such as <see cref="Docking"/>, and the methods of the parts that change
/// together, such as <see cref="ILSight.Navigation"/>'s.
/// It never sees the screen; a feature that needs the view to act later commands
/// it through <see cref="ViewCommands"/>. Lives on the UI thread: background work (opening, decompiling,
/// resolving, searching) is awaited there through <see cref="Background"/>. A result is dropped when
/// the user asked for something else in the meantime.
/// </summary>
public sealed class AppState
{
    /// <summary>Where the settings are written, and the user's themes are found.</summary>
    public required SettingsPlace SettingsPlace { get; init; }

    /// <summary>What the settings keep of the screen, as the screen last handed it over, or as the
    /// settings held it until it does.</summary>
    public required ViewSettings View;

    /// <summary>The main window as it was left: as the settings held it until the app closes.</summary>
    public WindowBounds? Window;

    public required AssemblyList Assemblies { get; init; }

    /// <summary>The browse tree of the assemblies: their roots, the children built so far and the
    /// nodes of their types and members.</summary>
    public required BrowseSession Session { get; init; }

    /// <summary>The named assembly lists, one of which <see cref="Assemblies"/> holds open.</summary>
    public SavedAssemblyLists Lists { get; } = new();

    /// <summary>The open tabs, the history of what they showed and the newest thing asked for, which change
    /// together: showing a node is one of its methods.</summary>
    public required Navigation Navigation { get; init; }

    /// <summary>The open tabs, <see cref="Navigation"/>'s.</summary>
    public DocumentTabs Tabs => Navigation.Tabs;

    /// <summary>Back and forward over what the tabs showed, <see cref="Navigation"/>'s.</summary>
    public NavigationHistory History => Navigation.History;

    /// <summary>What the user last asked for, <see cref="Navigation"/>'s.</summary>
    public UserIntent Intent => Navigation.Intent;
    public Reload Reload { get; } = new();

    /// <summary>The files of the list being read again one at a time, as when another framework or
    /// PDB is picked for an assembly.</summary>
    public Reopens Reopens { get; } = new();
    public required OpenRequests Opens { get; init; }

    /// <summary>What the settings were saved from the last time, to save again only when it changed.</summary>
    public SavedSources LastSaved { get; set; }

    /// <summary>Whether writing the settings is failing, and when to try again.</summary>
    public SaveRetry SaveRetry { get; set; } = new SaveRetry.Clear();
    public BookmarkList Bookmarks { get; } = new();

    /// <summary>The symbols being analyzed and what the analyses found.</summary>
    public AnalyzerTree Analyzer { get; } = new();

    public required ErrorLog Errors { get; init; }

    /// <summary>Where the work the app starts in the background is counted until it ends, so a harness waiting
    /// for the UI to be ready waits for it.</summary>
    public required BackgroundWork Work { get; init; }

    /// <summary>Starts the workflows the screen asks for, counted in <see cref="Work"/>, and cancels them as the
    /// app shuts down.</summary>
    public required WorkflowRunner Workflows { get; init; }

    /// <summary>Brings in what the tabs are pointed at: nodes decompiled, grids and pictures read.</summary>
    public required DecompileIntoTabHandler DecompileIntoTabHandler { get; init; }

    /// <summary>Follows references to what they lead to.</summary>
    public required NavigateHandler NavigateHandler { get; init; }

    /// <summary>Shows the metadata table rows that define what references point at.</summary>
    public required ShowInMetadataHandler ShowInMetadataHandler { get; init; }

    /// <summary>Opens the pages of the .NET API reference types and members have.</summary>
    public required ShowInApiReferenceHandler ShowInApiReferenceHandler { get; init; }

    /// <summary>Shows again the nodes the tabs reopened from the open list's saved documents wait for.</summary>
    public required ShowReopenedNodesHandler ShowReopenedNodesHandler { get; init; }

    /// <summary>Goes to the lines bookmarks mark.</summary>
    public required GoToBookmarkHandler GoToBookmarkHandler { get; init; }

    /// <summary>Imports bookmarks from files, this app's or the real ILSpy's.</summary>
    public required BookmarkImporting BookmarkImporting { get; init; }

    /// <summary>Exports the bookmarks to a file the real ILSpy reads.</summary>
    public required ExportBookmarksHandler ExportBookmarksHandler { get; init; }

    /// <summary>Grows diagrams by the cards of types: one shown with its neighbours, and those a card links to.</summary>
    public required DiagramGrowing DiagramGrowing { get; init; }

    /// <summary>Brings in the cards of the diagrams reopened from the open list's saved documents.</summary>
    public required ReopenDiagramHandler ReopenDiagramHandler { get; init; }

    /// <summary>Lays out diagrams as their cards are measured.</summary>
    public required LayOutDiagramHandler LayOutDiagramHandler { get; init; }

    /// <summary>Opens files and shows what an id names among them, as a command line asks.</summary>
    public required OpenAndShowIdHandler OpenAndShowIdHandler { get; init; }

    /// <summary>Goes back and forward through <see cref="History"/>.</summary>
    public required GoToHistoryEntryHandler GoToHistoryEntryHandler { get; init; }

    /// <summary>Opens assemblies into the list: files picked or dropped, and the assemblies one references.</summary>
    public required AssemblyOpening AssemblyOpening { get; init; }

    /// <summary>Reads the assemblies of the list again, one with a PDB or target framework picked, or all of them.</summary>
    public required AssemblyReloading AssemblyReloading { get; init; }

    /// <summary>Opens a list's files as a reload does, as switching lists or starting the app does.</summary>
    public required OpenAssembliesAsReloadHandler OpenAssembliesAsReloadHandler { get; init; }

    /// <summary>The newer version looked for, downloaded and offered, if any.</summary>
    public required AppUpdates Updates { get; set; }

    /// <summary>Whether the app looks for a newer version when it starts.</summary>
    public required bool IsUpdateCheckAutomatic { get; set; }
    /// <summary>The tabs reopened from the open list's saved documents that wait for what they showed.</summary>
    public ReopenedTabs Reopened { get; } = new();

    /// <summary>The declarations and documentation shown when hovering types and members in code.</summary>
    public required CodeTooltips Tooltips { get; init; }

    /// <summary>The themes the app can be drawn in, built-in and the user's.</summary>
    public required ThemeCatalog<ThemeFile, ThemeContents, AppTheme> Themes { get; init; }

    /// <summary>Reads the user's themes into <see cref="Themes"/>, makes their folder, writes a custom theme there
    /// and watches it.</summary>
    public required UserThemes<ThemeFile, ThemeContents, AppTheme> UserThemes { get; init; }

    /// <summary>The icon themes the app can be drawn with, built-in and the user's.</summary>
    public required ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> IconThemes { get; init; }

    /// <summary>Reads the user's icon themes into <see cref="IconThemes"/>, makes their folder, writes a custom
    /// icon theme there and watches it, the fonts and pictures in it too.</summary>
    public required UserThemes<IconThemeFile, IconThemeContents, IconTheme> UserIconThemes { get; init; }

    /// <summary>The id of the chosen theme, kept even while no theme has it.</summary>
    public required ThemeId<ThemeFile> Theme;

    /// <summary>The id of the chosen icon theme, kept even while no icon theme has it.</summary>
    public required ThemeId<IconThemeFile> IconTheme;
    public ApiVisibility ApiVisibility;

    /// <summary>Whether what is picked and followed shows in the preview tab rather than the active tab.</summary>
    public bool PreviewTabs = true;

    /// <summary>The interface language.</summary>
    public Locale Locale;

    public DecompilationLanguage Language { get; set; }

    /// <summary>How C# is decompiled.</summary>
    public required DecompilerOptions DecompilerOptions { get; set; }

    /// <summary>The commands the features queue for the view when none is on the stack, for the screen to run
    /// in order before it draws next.</summary>
    public required Queue<ViewCommand> ViewCommands { get; init; }

    /// <summary>Counts each time the screen hands over a change to what the settings keep of it.</summary>
    public int ViewSettingsChanges;

    /// <summary>True while a reference or a history entry is being found in the background.</summary>
    public bool IsResolving => Intent.IsResolving;

    /// <summary>True while <see cref="ILSight.AssemblyReloading.RefreshAssembliesAsync"/> reopens the assemblies.</summary>
    public bool IsReloading => Reload.IsRunning;

    /// <summary>True while a file of the list is read again, the list still holding it as it was.</summary>
    public bool IsReopening => Reopens.IsRunning;

    /// <summary>True while the nodes the tabs reopened from the saved documents showed are being found.</summary>
    public bool IsFindingDocuments => Reopened.IsFinding;

    /// <summary>True while analyzer roots wait for their symbols to be found.</summary>
    public bool IsFindingAnalyses => Analyzer.IsFinding;

    /// <summary>The bookmark imports finding their places or waiting for the user to say how to take them.</summary>
    public BookmarkImports Imports { get; } = new();

    /// <summary>True while imported bookmarks' places are being found in the background.</summary>
    public bool IsImporting => Imports.IsFinding;

    /// <summary>The code, projects, solutions and PDBs being written in the background, one at a time.</summary>
    public ExportQueue Export { get; } = new();

    /// <summary>The reports of the exports that finished, oldest first, until they are taken off the
    /// exports panel.</summary>
    public List<ExportReport> FinishedExports { get; } = new();

    /// <summary>True while <see cref="Export"/> runs.</summary>
    public bool IsExporting => Export.IsRunning;

    /// <summary>True while the diagram of an open tab reads cards or lays them out in the background.</summary>
    public bool IsDiagramming
    {
        get
        {
            foreach (var tab in Tabs.Open)
            {
                var diagram = tab.Diagram;
                if (diagram is not null && diagram.IsBusy)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>True while an open tab compares two assemblies in the background.</summary>
    public bool IsComparing
    {
        get
        {
            foreach (var tab in Tabs.Open)
            {
                var comparison = tab.Comparison;
                if (comparison is not null && comparison.IsComparing)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
