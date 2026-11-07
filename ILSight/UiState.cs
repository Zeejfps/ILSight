using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The screen, one per app: what every view is drawn with this frame (the theme, the editor's
/// and icons' colors, the icons, the strings of the chosen language), and the state that exists only because there
/// is a screen: the layout, each tab's view, the trees' and tables' state, the panels and dialogs with
/// what is selected in them, and UI flags. What the user does to it are static functions grouped by
/// feature, such as <see cref="DocumentPanels"/> and <see cref="ScreenLayout"/>. It and the app state never
/// see each other: the app hands it what it shows each frame and runs what it asks of the app state
/// through <see cref="AppCommands"/>.</summary>
public sealed class UiState : IUiState
{
    public UiTheme Theme { get; set; } = UiTheme.Dark;
    public CodeViewPalette CodeViewPalette { get; set; } = BuiltInThemes.Dark.Code;
    public NodeIconPalette NodeIconPalette { get; set; } = BuiltInThemes.Dark.Icons;

    /// <summary>What each icon key draws in the chosen icon theme.</summary>
    public AppIcons Icons { get; set; } = BuiltInIconThemes.Reference.Icons;

    /// <summary>The fonts <see cref="Icons"/> are drawn in, which must be registered for them to show.</summary>
    public IReadOnlyList<FontFace> IconFonts { get; set; } = BuiltInIconThemes.Reference.Fonts;

    /// <summary>The icons the screen was last drawn with.</summary>
    public (AppIcons Icons, IReadOnlyList<FontFace> Fonts)? ShownIcons;
    public Strings Strings { get; set; } = Strings.En;

    /// <summary>What the screen asked of the app state while it drew, for the app to run once the frame is drawn.</summary>
    public required Queue<AppCommand> AppCommands { get; init; }

    /// <summary>Where the views and the open documents are docked, all as tabs: side by side,
    /// stacked or in floating windows.</summary>
    public required MainLayout MainLayout { get; init; }

    public DockLayout Layout => MainLayout.Dock;

    /// <summary>How each open tab is shown, and the panel it is docked as.</summary>
    public DocumentViews Documents { get; } = new();

    /// <summary>The app state's active tab as the screen last followed it, which the views open beside.</summary>
    public DocumentTab? ActiveTab;

    /// <summary>When to try focusing another tab while none is active.</summary>
    public TabFocusTry LastTabFocusTry = new(-1, -1);

    /// <summary>The language and options the tabs in front were last brought up to date with.</summary>
    public (DecompilationLanguage Language, DecompilerOptions Options)? ShownDecompilation;

    /// <summary>Set while the user is asked whether to reset the layout.</summary>
    public bool ConfirmingLayoutReset;

    /// <summary>Set while the About dialog is open.</summary>
    public bool IsAboutOpen;

    /// <summary>The browse tree over the open assemblies, with the view's expansion and selection.</summary>
    public required BrowseTree Tree { get; init; }

    /// <summary>Which assembly list the tree last showed.</summary>
    public int ShownListGeneration;

    /// <summary>The node the tree's context menu was opened on.</summary>
    public BrowseNode? MenuTarget;

    /// <summary>The document whose tab's context menu was opened.</summary>
    public DocumentTab? TabMenuTarget;

    /// <summary>The web pages the features asked to open, which the app hands to the browser once the
    /// frame is drawn.</summary>
    public Queue<string> WebPagesToOpen { get; } = new();

    /// <summary>Whether the app is to close once the frame is drawn.</summary>
    public bool QuitAsked;

    /// <summary>Whether the window is to come to the front once the frame is drawn.</summary>
    public bool FrontAsked;

    /// <summary>The files the features asked to open, which the app hands to the OS once the frame is
    /// drawn.</summary>
    public Queue<string> FilesToOpen { get; } = new();

    /// <summary>The folders the features asked to open, which the app hands to the OS once the frame is
    /// drawn.</summary>
    public Queue<string> FoldersToOpen { get; } = new();

    /// <summary>The analyzer's tree as its view shows it.</summary>
    public required AnalyzerRows Analyzer { get; init; }

    /// <summary>The columns of the bookmarks panel: mark, name, location and module.</summary>
    public TableColumns BookmarkColumns { get; } = new(
        new TableColumn(Fixed: 28f, Controls: true),
        new TableColumn(2f, 80f),
        new TableColumn(3f, 80f),
        new TableColumn(2f, 60f));

    /// <summary>The columns of the export dialog's table: assembly, version and project name.</summary>
    public TableColumns ExportColumns { get; } = new(
        new TableColumn(3f, 120f),
        new TableColumn(1f, 64f),
        new TableColumn(3f, 120f));

    /// <summary>The columns of the search results: name, location and assembly.</summary>
    public TableColumns SearchColumns { get; } = new(
        new TableColumn(3f, 120f),
        new TableColumn(3f, 80f),
        new TableColumn(2f, 60f));

    /// <summary>The columns of the running processes: name, id, entry assembly and runtime.</summary>
    public TableColumns ProcessColumns { get; } = new(
        new TableColumn(2f, 80f),
        new TableColumn(1f, 48f),
        new TableColumn(2f, 80f),
        new TableColumn(1f, 56f));

    /// <summary>The columns of the assemblies a process loaded: name and folder.</summary>
    public TableColumns ProcessModuleColumns { get; } = new(
        new TableColumn(2f, 100f),
        new TableColumn(3f, 80f));

    /// <summary>The decompiler options panel's folded groups.</summary>
    public required FoldedGroups FoldedOptionGroups { get; init; }

    /// <summary>What the decompiler options panel is searched for; empty shows every option.</summary>
    public string DecompilerOptionsQuery = "";

    /// <summary>What the display options panel is searched for; empty shows every option.</summary>
    public string DisplayOptionsQuery = "";

    /// <summary>How the documents draw code: its size and the aids drawn around it.</summary>
    public required DisplayOptions Display;

    /// <summary>Set while the user is asked whether to put every display option back to its default.</summary>
    public bool ConfirmingDisplayReset;

    /// <summary>The fonts the display options offer to draw code in.</summary>
    public CodeFontChoices CodeFonts { get; } = new();

    /// <summary>The font code was last drawn in.</summary>
    public CodeFont? ShownCodeFont;

    /// <summary>What the settings last kept of the screen, by what it was made from, so that it is handed
    /// over again only once something in it changed.</summary>
    public ViewSources KeptView;

    public BookmarksPanel BookmarksPanel { get; } = new();

    /// <summary>The search panel's query, results and selection.</summary>
    public required Search Search { get; init; }

    /// <summary>The bar over each document: its path, or its search.</summary>
    public required Omnibar Omnibar { get; init; }

    /// <summary>The metadata grids' flags filters.</summary>
    public required MetadataFilters MetadataFilters { get; init; }

    /// <summary>The export selected in the exports panel, or null; one that is gone is as none.</summary>
    public ExportRun? SelectedExport;

    /// <summary>The export the exports panel's context menu was opened on, or null.</summary>
    public ExportRun? ExportMenuTarget;

    public ErrorDialog ErrorDialog { get; } = new();

    /// <summary>What the assembly lists dialog shows, and whether it is open.</summary>
    public AssemblyListsDialog ListsDialog { get; } = new();

    /// <summary>The "open from framework" dialog.</summary>
    public required FrameworkPicker Picker { get; init; }

    /// <summary>What this machine has installed.</summary>
    public required InstalledRuntime Runtime { get; init; }

    /// <summary>The dialog that picks the assembly to compare another with.</summary>
    public ComparisonPicker ComparisonPicker { get; } = new();

    /// <summary>The "open from NuGet" dialog.</summary>
    public required NuGetPicker NuGetPicker { get; init; }

    /// <summary>The "open from running process" dialog.</summary>
    public required ProcessPicker ProcessPicker { get; init; }

    /// <summary>What the export dialog asks for, and whether it is open.</summary>
    public ExportSetup ExportSetup { get; } = new();
}
