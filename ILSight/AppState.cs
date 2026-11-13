using ZGF.Gui.Immediate;
using ZGF.Desktop;
using ILSight.Core;
using ILSight.ExportArea;
using ILSight.BookmarkArea;
using ILSight.CodeTooltipArea;
using ILSight.UserArea;
using ILSight.SearchArea;
using ILSight.DiagramArea;
using ILSight.AssemblyArea;
using ILSight.SettingArea;
using ILSight.ILSightAppArea;
using ILSight.MetadataArea;

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

    /// <summary>How many rows a grid made for a tab reads with its content: what its first show needs, the rows it
    /// measures its columns over or the window it opens to, the larger (<see cref="MetadataGridArea.MetadataGridFrame.CalculateFirstReadRows"/>),
    /// as the frame edge last worked it out from the screen's layout; from the window the settings kept until then.</summary>
    public int FirstGridRows;

    /// <summary>What the open list holds while it is open, which switching lists changes together.</summary>
    public required OpenListSession ListSession { get; init; }

    public AssemblyList Assemblies => ListSession.Assemblies;

    /// <summary>The browse tree of the assemblies: their roots, the children built so far and the
    /// nodes of their types and members.</summary>
    public required BrowseSession Session { get; init; }

    /// <summary>The named assembly lists, one of which <see cref="Assemblies"/> holds open.</summary>
    public SavedAssemblyLists Lists { get; } = new();

    /// <summary>The open tabs, the history of what they showed and the newest thing asked for, which change
    /// together: showing a node is one of its methods.</summary>
    public Navigation Navigation => ListSession.Navigation;

    public Reload Reload => ListSession.Reload;

    /// <summary>The files of the list being read again one at a time, as when another framework or
    /// PDB is picked for an assembly.</summary>
    public Reopens Reopens { get; } = new();
    public OpenRequests Opens => ListSession.Opens;

    /// <summary>What the settings were saved from the last time, to save again only when it changed.</summary>
    public SavedSources LastSaved { get; set; }

    /// <summary>Whether writing the settings is failing, and when to try again.</summary>
    public SaveRetry SaveRetry { get; set; } = new SaveRetry.Clear();

    /// <summary>Whether the window closes as the user asks it to, from what saving the settings then did.</summary>
    public WindowClosing Closing { get; } = new();

    public BookmarkList Bookmarks { get; } = new();

    /// <summary>The symbols being analyzed and what the analyses found.</summary>
    public AnalyzerTree Analyzer => ListSession.Analyzer;

    public required ErrorLog Errors { get; init; }

    /// <summary>Whether a later run's arguments asked for the window to come to the front, which it does once the
    /// frame is drawn.</summary>
    public bool FrontAsked { get; set; }

    /// <summary>The last request refused, said in the status bar.</summary>
    public required RefusalLine Refusals { get; init; }

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

    /// <summary>Runs the search panel's and the omnibar's searches.</summary>
    public required RunSearchHandler RunSearchHandler { get; init; }

    /// <summary>Looks up the tooltip of a reference hovered in code.</summary>
    public required LookUpTooltipHandler LookUpTooltipHandler { get; init; }

    /// <summary>Builds the children of a node the tree showed loading.</summary>
    public required BuildChildrenHandler BuildChildrenHandler { get; init; }

    /// <summary>Finishes expanding an assembly and the namespaces in it.</summary>
    public required ExpandNamespacesHandler ExpandNamespacesHandler { get; init; }

    /// <summary>Saves the settings.</summary>
    public required SettingsSaving SettingsSaving { get; init; }

    /// <summary>Writes an export the exports panel queued, once its turn comes.</summary>
    public required ExportWriting ExportWriting { get; init; }

    /// <summary>Saves the bytes of a resource to a file straight away.</summary>
    public required SaveResourceHandler SaveResourceHandler { get; init; }

    /// <summary>Writes a file of a package to a file straight away.</summary>
    public required ExtractPackageFileHandler ExtractPackageFileHandler { get; init; }

    /// <summary>Looks at what the export dialog names on the disk.</summary>
    public required CheckExportFilesHandler CheckExportFilesHandler { get; init; }

    /// <summary>Exports the bookmarks to a file the real ILSpy reads.</summary>
    public required ExportBookmarksHandler ExportBookmarksHandler { get; init; }

    /// <summary>Grows diagrams by the cards of types: one shown with its neighbours, and those a card links to.</summary>
    public required DiagramGrowing DiagramGrowing { get; init; }

    /// <summary>Brings in the cards of the diagrams reopened from the open list's saved documents.</summary>
    public required ReopenDiagramHandler ReopenDiagramHandler { get; init; }

    /// <summary>Lays out diagrams as their cards are measured.</summary>
    public required LayOutDiagramHandler LayOutDiagramHandler { get; init; }

    /// <summary>Runs the comparisons of assemblies the tabs show.</summary>
    public required CompareAssembliesHandler CompareAssembliesHandler { get; init; }

    /// <summary>Runs the analyses the analyzer shows.</summary>
    public required RunAnalysisHandler RunAnalysisHandler { get; init; }

    /// <summary>Analyzes what a reference in the code points at.</summary>
    public required AnalyzeReferenceHandler AnalyzeReferenceHandler { get; init; }

    /// <summary>Analyzes again what was analyzed in the last run.</summary>
    public required RestoreAnalysesHandler RestoreAnalysesHandler { get; init; }

    /// <summary>Opens files and shows what an id names among them, as a command line asks.</summary>
    public required OpenAndShowIdHandler OpenAndShowIdHandler { get; init; }

    /// <summary>Goes back and forward through <see cref="Navigation"/>'s history.</summary>
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
    public ReopenedTabs Reopened => ListSession.Reopened;

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

    /// <summary>The interface language.</summary>
    public Locale Locale;

    /// <summary>The language code is shown in and how C# is decompiled, and the setup the views read and pass on.</summary>
    public required DecompilerChoice Decompiler { get; init; }

    /// <summary>The commands the features queue for the view when none is on the stack, for the screen to run
    /// in order before it draws next.</summary>
    public required Queue<ViewCommand> ViewCommands { get; init; }

    /// <summary>Counts each time the screen hands over a change to what the settings keep of it.</summary>
    public int ViewSettingsChanges;

    /// <summary>True while a reference or a history entry is being found in the background.</summary>
    public bool IsResolving => Navigation.IsResolving;

    /// <summary>True while <see cref="ILSight.AssemblyArea.AssemblyReloading.RefreshAssembliesAsync"/> reopens the assemblies.</summary>
    public bool IsReloading => Reload.IsRunning;

    /// <summary>True while a file of the list is read again, the list still holding it as it was.</summary>
    public bool IsReopening => Reopens.IsRunning;

    /// <summary>The bookmark imports waiting for the user to say how to take them.</summary>
    public BookmarkImports Imports { get; } = new();

    /// <summary>The code, projects, solutions and PDBs being written in the background, one at a time.</summary>
    public ExportQueue Export { get; } = new();

    /// <summary>The reports of the exports that finished, oldest first, until they are taken off the
    /// exports panel.</summary>
    public List<ExportReport> FinishedExports { get; } = new();
}
