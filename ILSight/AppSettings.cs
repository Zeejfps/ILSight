using System.Text.Json;
using ZGF.Desktop;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.Localization;

namespace ILSight;

/// <summary>What persists between runs, in the user's app-data folder, as read from its file by
/// <see cref="SettingsJson.LoadSettings"/>: whatever the file held, every value here is one the app can use.</summary>
public sealed class AppSettings
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILSight", "settings.json");

    /// <summary>Where <see cref="SettingsJson.SaveSettings"/> writes; null keeps the settings in memory only.</summary>
    public string? FilePath { get; set; }

    /// <summary>The named assembly lists, the open one's as it was left.</summary>
    public List<AssemblyListRecord> AssemblyLists { get; set; } = new();

    /// <summary>The name of the list that was open; null opens the first.</summary>
    public string? ActiveAssemblyList { get; set; }

    /// <summary>The id of the chosen theme, the name of its file without the extension.</summary>
    public ThemeId<ThemeFile> Theme { get; set; } = Theming.DarkId;

    /// <summary>The id of the chosen icon theme, the name of its file without the extension.</summary>
    public ThemeId<IconThemeFile> IconTheme { get; set; } = IconTheming.ReferenceId;
    public DecompilationLanguage Language { get; set; } = DecompilationLanguage.CSharp;
    public DecompilerOptions Decompiler { get; set; } = DecompilerOptions.Default;

    /// <summary>The decompiler options panel's folded groups.</summary>
    public List<string>? FoldedOptionGroups { get; set; }

    /// <summary>How the documents draw code; null for the defaults.</summary>
    public DisplayOptions? Display { get; set; }

    /// <summary>The interface language; null follows the system's.</summary>
    public Locale? Locale { get; set; }
    public ApiVisibility ApiVisibility { get; set; } = ApiVisibility.AllButCompilerGenerated;

    /// <summary>Whether the app looks for a newer version when it starts.</summary>
    public bool CheckForUpdates { get; set; } = true;
    public SearchKinds SearchKinds { get; set; } = SearchKinds.TypesAndMembers;
    /// <summary>Where the views and the open list's documents were docked; null for where they start out.</summary>
    public DockLayout? Layout { get; set; }

    public List<BookmarkRecord> Bookmarks { get; set; } = new();

    /// <summary>The bookmarks panel's column weights as last dragged; null for the defaults.</summary>
    public List<float>? BookmarkColumns { get; set; }

    /// <summary>The search results' column weights as last dragged; null for the defaults.</summary>
    public List<float>? SearchColumns { get; set; }

    /// <summary>The export dialog's table column weights as last dragged; null for the defaults.</summary>
    public List<float>? ExportColumns { get; set; }

    /// <summary>The metadata grids' flags filters, by kind of row and column.</summary>
    public Dictionary<string, SavedFilterState>? FlagsFilters { get; set; }

    /// <summary>The main window as it was left; null opens it at the default size, centered.</summary>
    public WindowBounds? Window { get; set; }

    /// <summary>The export dialog's window as it was left; null opens it at its default size, centered.</summary>
    public WindowBounds? ExportWindow { get; set; }

    /// <summary>The feed the "open from NuGet" dialog last searched; null searches the NuGet.Config's first.</summary>
    public SavedNuGetSearch? NuGet { get; set; }

    /// <summary>The "open from NuGet" dialog's window as it was left; null opens it at its default size, centered.</summary>
    public WindowBounds? NuGetWindow { get; set; }

    public const int MinWindowWidth = 480;
    public const int MinWindowHeight = 320;
    public const int MaxWindowSize = 16384;
}
