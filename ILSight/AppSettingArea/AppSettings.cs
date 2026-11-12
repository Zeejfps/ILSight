using ZGF.Desktop;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Metadata;
using ILSight.BookmarkArea;
using ILSight.AssemblyListArea;
using ILSight.DisplayOptionArea;
using ILSight.SettingsKeeping;

namespace ILSight.AppSettingArea;

/// <summary>What persists between runs, in the user's app-data folder, as read from its file by
/// <see cref="SettingsReading"/>: whatever the file held, every value here is one the app can use.</summary>
public sealed class AppSettings
{
    /// <summary>The settings file in the user's app-data folder; under this process's folder when the
    /// system names no app-data folder.</summary>
    public static FullFilePath DefaultPath { get; } = FullFilePath.Of(Path.Combine(Environment.CurrentDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ILSight", "settings.json"));

    /// <summary>The settings file, with the user's themes and icon themes beside it; null keeps the settings
    /// in memory only.</summary>
    public FullFilePath? FilePath { get; set; }

    /// <summary>Whether <see cref="SettingsSaving.WriteSettings"/> writes to <see cref="FilePath"/>; false when the
    /// file there could be neither read whole nor copied aside, so writing over it would lose what it holds.</summary>
    public bool CanSaveOverFile { get; set; } = true;

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

    /// <summary>The "open from running process" dialog's window as it was left; null opens it at its default size, centered.</summary>
    public WindowBounds? ProcessWindow { get; set; }

    /// <summary>The widths of the running processes' columns, as dragged.</summary>
    public List<float>? ProcessColumns { get; set; }

    /// <summary>The widths of the columns of a process's assemblies, as dragged.</summary>
    public List<float>? ProcessModuleColumns { get; set; }

    /// <summary>The "close assemblies" dialog's window as it was left; null opens it at its default size, centered.</summary>
    public WindowBounds? CloseWindow { get; set; }

    public const int MinWindowWidth = 480;
    public const int MinWindowHeight = 320;
    public const int MaxWindowSize = 16384;
}
