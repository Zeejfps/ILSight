using ILSight.Core;
using ILSight.Localization;
using ILSight.AppSettingsFormat;
using ILSight.AssemblyListKeeping;
using ILSight.DisplayChoices;
using ILSight.NuGet;
using ILSight.Bookmarking;

namespace ILSight.SettingsKeeping;

/// <summary>The settings file as it is written: what <see cref="AppSettings"/> keeps, under the names
/// the file has it by. It is only ever written; <see cref="SettingsJson"/> reads a file one field at
/// a time, through the shapes below, which take what a hand-edited or older file can hold.</summary>
internal sealed record SettingsFile(
    List<AssemblyListJson> AssemblyLists,
    string? ActiveAssemblyList,
    string Theme,
    string IconTheme,
    DecompilationLanguage Language,
    DecompilerOptionsJson Decompiler,
    List<string>? FoldedOptionGroups,
    DisplayOptionsJson? Display,
    Locale? Locale,
    ApiVisibility ApiVisibility,
    bool CheckForUpdates,
    SearchKinds SearchKinds,
    DockLayoutJson? Layout,
    List<BookmarkJson> Bookmarks,
    List<float>? BookmarkColumns,
    List<float>? SearchColumns,
    List<float>? ExportColumns,
    Dictionary<string, SavedFilterStateJson?>? FlagsFilters,
    WindowBoundsJson? Window,
    WindowBoundsJson? ExportWindow,
    NuGetSearchJson? NuGet,
    WindowBoundsJson? NuGetWindow,
    WindowBoundsJson? ProcessWindow,
    List<float>? ProcessColumns,
    List<float>? ProcessModuleColumns,
    WindowBoundsJson? CloseWindow)
{
    /// <summary>The one list that settings files kept before lists had names.</summary>
    public const string OldAssemblies = "Assemblies";

    /// <summary>The symbols analyzed before each list kept its own.</summary>
    public const string OldAnalyzed = "Analyzed";

    /// <summary>The one switch settings files kept before <see cref="ApiVisibility"/> had three choices.</summary>
    public const string OldShowNonPublic = "ShowNonPublic";
}
