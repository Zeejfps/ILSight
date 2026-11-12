using ILSight.Core;
using ILSight.AppSettingArea;
using ILSight.AssemblyListArea;
using ILSight.NuGetArea;
using ILSight.BookmarkArea;

namespace ILSight.SettingArea;

/// <summary>The settings file as it is written: what <see cref="AppSettings"/> keeps, under the names
/// the file has it by. It is only ever written; <see cref="SettingsJson"/> reads a file one field at
/// a time, through the shapes below, which take what a hand-edited or older file can hold.</summary>
internal sealed record SettingsFile
{
    public required List<AssemblyListJson> AssemblyLists { get; init; }

    public required string? ActiveAssemblyList { get; init; }

    public required string Theme { get; init; }

    public required string IconTheme { get; init; }

    public required DecompilationLanguage Language { get; init; }

    public required DecompilerOptionsJson Decompiler { get; init; }

    public required List<string>? FoldedOptionGroups { get; init; }

    public required DisplayOptionsJson? Display { get; init; }

    public required Locale? Locale { get; init; }

    public required ApiVisibility ApiVisibility { get; init; }

    public required bool CheckForUpdates { get; init; }

    public required SearchKinds SearchKinds { get; init; }

    public required DockLayoutJson? Layout { get; init; }

    public required List<BookmarkJson> Bookmarks { get; init; }

    public required List<float>? BookmarkColumns { get; init; }

    public required List<float>? SearchColumns { get; init; }

    public required List<float>? ExportColumns { get; init; }

    public required Dictionary<string, SavedFilterStateJson?>? FlagsFilters { get; init; }

    public required WindowBoundsJson? Window { get; init; }

    public required WindowBoundsJson? ExportWindow { get; init; }

    public required NuGetSearchJson? NuGet { get; init; }

    public required WindowBoundsJson? NuGetWindow { get; init; }

    public required WindowBoundsJson? ProcessWindow { get; init; }

    public required List<float>? ProcessColumns { get; init; }

    public required List<float>? ProcessModuleColumns { get; init; }

    public required WindowBoundsJson? CloseWindow { get; init; }

    /// <summary>The one list that settings files kept before lists had names.</summary>
    public const string OldAssemblies = "Assemblies";

    /// <summary>The symbols analyzed before each list kept its own.</summary>
    public const string OldAnalyzed = "Analyzed";

    /// <summary>The one switch settings files kept before <see cref="ApiVisibility"/> had three choices.</summary>
    public const string OldShowNonPublic = "ShowNonPublic";
}
