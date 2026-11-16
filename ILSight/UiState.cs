using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.CodeViewArea;
using ILSight.ExportArea;
using ILSight.BookmarkArea;
using ILSight.BuiltInArea;
using ILSight.ComparisonPickerArea;
using ILSight.OmnibarArea;
using ILSight.DocumentViewArea;
using ILSight.ProcessArea;
using ILSight.FrameworkArea;
using ILSight.AssemblyListArea;
using ILSight.NuGetArea;
using ILSight.SearchArea;
using ILSight.MainArea;
using ILSight.AssemblyArea;
using ILSight.MetadataArea;
using ILSight.MetadataGridArea;

namespace ILSight;

/// <summary>The screen, one per app: what every view is drawn with this frame (the theme, the editor's
/// and icons' colors, the icons, the strings of the chosen language), and the state that exists only because there
/// is a screen: the layout, each tab's view, the trees' and tables' state, the panels and dialogs with
/// what is selected in them, and UI flags. What the user does to it are its parts' methods, such as
/// <see cref="Panels"/>', or static functions grouped by feature, such as <see cref="ScreenLayout"/>. It and the app state never
/// see each other: the app hands it what it shows each frame and runs what it asks of the app state
/// through <see cref="AppCommands"/>.</summary>
public sealed class UiState : IUiState
{
    public Appearance Appearance { get; } = new();

    UiTheme IUiState.Theme => Appearance.Theme;

    /// <summary>What the screen last showed: the icons, the decompilation, the list, the code font.</summary>
    internal ShownChanges Shown { get; } = new();

    /// <summary>What the screen asked of the app state while it drew, for the app to run once the frame is drawn.</summary>
    public required AppCommandQueue AppCommands { get; init; }

    /// <summary>How each open tab is shown, and the panel it is docked as.</summary>
    public required DocumentViews Documents { get; init; }

    public required TabFocus TabFocus { get; init; }

    /// <summary>The layout, where the views and the open documents are docked, all as tabs: side by side, stacked or in
    /// floating windows; it opens the views beside the active document, docks, brings to the front and closes the
    /// tabs' panels, and puts the layout back.</summary>
    public required Panels Panels { get; init; }

    /// <summary>Whether to reset the layout, asked of the user.</summary>
    public Confirmation LayoutReset { get; } = new();

    public AboutDialog About { get; } = new();

    /// <summary>The browse tree over the open assemblies, with the view's expansion and selection.</summary>
    public required BrowseTree Tree { get; init; }

    /// <summary>The context menu of a document's tab.</summary>
    public TabMenu TabMenu { get; } = new();

    /// <summary>What the features asked of the shell (web pages, files and folders to open, the app to quit), which the
    /// frame edge hands to the OS once the frame is drawn.</summary>
    public ShellRequests Shell { get; } = new();

    /// <summary>The analyzer's tree as its view shows it.</summary>
    public required AnalyzerRows Analyzer { get; init; }

    /// <summary>The decompiler options panel's search and folded groups.</summary>
    public required DecompilerOptionsPanel DecompilerOptionsPanel { get; init; }

    /// <summary>The display options panel's search.</summary>
    public DisplayOptionsPanel DisplayOptionsPanel { get; } = new();

    /// <summary>How the documents draw code: its size and the aids drawn around it, and their reset asked of the user.</summary>
    public required ScreenDisplay Display { get; init; }

    /// <summary>The fonts the display options offer to draw code in.</summary>
    public CodeFontChoices CodeFonts { get; } = new();

    public KeptViewSources KeptView { get; } = new();

    public BookmarksPanel BookmarksPanel { get; } = new();

    /// <summary>The search panel's query, results and selection.</summary>
    public required Search Search { get; init; }

    /// <summary>The search panel's results table.</summary>
    public SearchPanel SearchPanel { get; } = new();

    /// <summary>The bar over each document: its path, or its search.</summary>
    public required Omnibar Omnibar { get; init; }

    /// <summary>The metadata grids' flags filters, and the sorts, filters and pages they asked for while the screen drew.</summary>
    public required MetadataGridScreenState Grids { get; init; }

    /// <summary>Sorts and filters the rows <see cref="MetadataGridScreenState.Views"/> asks for, off the screen's thread.</summary>
    public required ArrangeMetadataGridHandler ArrangeMetadataGridHandler { get; init; }

    /// <summary>Reads the pages <see cref="MetadataGridScreenState.Pages"/> asks for, off the screen's thread.</summary>
    public required ReadMetadataPageHandler ReadMetadataPageHandler { get; init; }

    /// <summary>The exports panel's selection and context menu.</summary>
    public ExportsPanel ExportsPanel { get; } = new();

    public ErrorDialog ErrorDialog { get; } = new();

    /// <summary>The refusal note's details, opened from the note.</summary>
    public RefusalDetails RefusalDetails { get; } = new();

    /// <summary>What the assembly lists dialog shows, and whether it is open.</summary>
    public AssemblyListsDialog ListsDialog { get; } = new();

    /// <summary>The "open from framework" dialog.</summary>
    public required FrameworkPicker Picker { get; init; }

    /// <summary>What this machine has installed.</summary>
    /// <summary>The ready-made lists this machine offers, looked for the first time the lists dialog's menu
    /// opens.</summary>
    public PresetScan Presets { get; } = new();

    /// <summary>The folder of the licenses the app ships beside it, which the about dialog opens; null when it
    /// ships none there.</summary>
    public required string? ShippedLicenses { get; init; }

    /// <summary>The dialog that picks the assembly to compare another with.</summary>
    public ComparisonPicker ComparisonPicker { get; } = new();

    /// <summary>The "open from NuGet" dialog.</summary>
    public required NuGetPicker NuGetPicker { get; init; }

    /// <summary>Reads the user's NuGet.Config for <see cref="NuGetPicker"/> the first time it opens.</summary>
    public required ReadNuGetSetupHandler ReadNuGetSetupHandler { get; init; }


    /// <summary>Fetches the package <see cref="NuGetPicker"/> opens into the package cache.</summary>
    public required DownloadNuGetPackageHandler DownloadNuGetPackageHandler { get; init; }

    /// <summary>Starts the work <see cref="NuGetPicker"/> asks for: its searches, and the icons and details it shows.</summary>
    public required NuGetPickerFetcher NuGetFetcher { get; init; }

    /// <summary>The "close assemblies" dialog.</summary>
    public AssemblyCloser Closer { get; } = new();

    /// <summary>The "open from running process" dialog.</summary>
    public required ProcessPicker ProcessPicker { get; init; }

    /// <summary>What the export dialog asks for, and whether it is open.</summary>
    public ExportSetup ExportSetup { get; } = new();
}
