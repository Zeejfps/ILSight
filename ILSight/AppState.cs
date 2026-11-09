using ZGF.Gui.Immediate;
using ZGF.Desktop;
using ILSight.Core;
using ILSight.Localization;
using static ILSight.IconTheming;

namespace ILSight;

/// <summary>
/// The app's data, and which tab, list and history entry are current. What the user can do to it are
/// static functions grouped by feature, such as <see cref="Navigation"/> and <see cref="Docking"/>.
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
    public SavedAssemblyLists Lists { get; } = AssemblyLists.CreateLists();

    public NavigationHistory History { get; } = new();
    public UserIntent Intent { get; } = new();
    public Reload Reload { get; } = new();

    /// <summary>The files of the list being read again one at a time, as when another framework or
    /// PDB is picked for an assembly.</summary>
    public Reopens Reopens { get; } = new();
    public OpenRequests Opens { get; } = new();

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

    /// <summary>The newer version looked for, downloaded and offered, if any.</summary>
    public required AppUpdates Updates { get; set; }
    public DocumentTabs Tabs { get; } = new();

    /// <summary>The tabs reopened from the open list's saved documents that wait for what they showed.</summary>
    public ReopenedTabs Reopened { get; } = new();

    /// <summary>The declarations and documentation shown when hovering types and members in code.</summary>
    public required CodeTooltips Tooltips { get; init; }

    /// <summary>The themes the app can be drawn in, built-in and the user's.</summary>
    public ThemeCatalog<ThemeFile, ThemeContents, AppTheme> Themes { get; } = Theming.CreateCatalog(BuiltInThemes.Files);

    /// <summary>Watches the user's themes folder while the app runs.</summary>
    public ThemeFolderWatch ThemesWatch { get; } = Theming.CreateThemeWatch();

    /// <summary>The icon themes the app can be drawn with, built-in and the user's.</summary>
    public ThemeCatalog<IconThemeFile, IconThemeContents, IconTheme> IconThemes { get; } = IconTheming.CreateCatalog(BuiltInIconThemes.Files);

    /// <summary>Watches the user's icon themes folder, and the fonts and pictures in it, while the app runs.</summary>
    public ThemeFolderWatch IconThemesWatch { get; } = new ThemeFolderWatch { Filters = IconThemeFiles, IncludesSubfolders = true };

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
    public Queue<ViewCommand> ViewCommands { get; } = new();

    /// <summary>Counts each time the screen hands over a change to what the settings keep of it.</summary>
    public int ViewSettingsChanges;

    /// <summary>True while a reference or a history entry is being found in the background.</summary>
    public bool IsResolving => Intent.IsResolving;

    /// <summary>True while <see cref="ILSight.Assemblies.RefreshAssemblies"/> reopens the assemblies.</summary>
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
                if (diagram != null && diagram.IsBusy)
                    return true;
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
                if (comparison != null && comparison.IsComparing)
                    return true;
            }
            return false;
        }
    }
}
