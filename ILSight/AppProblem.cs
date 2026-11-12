using System.Diagnostics;
using ILSight.Core;
using ILSight.Core.Analysis;
using ILSight.BookmarkArea;
using ILSight.SettingArea;

namespace ILSight;

/// <summary>Something that went wrong, worth telling the user, as what happened rather than words, for
/// the screen to tell in the chosen language. A <c>Reason</c> is as the system said it.</summary>
public abstract record AppProblem
{
    private AppProblem()
    {
    }

    public sealed record OpenFailed(Exception Error) : AppProblem;

    /// <summary>The PDB at <see cref="PdbPath"/> cannot be used for the assembly <see cref="Assembly"/>.</summary>
    public sealed record PdbUnusable : AppProblem
    {
        public required string PdbPath { get; init; }

        public required string Assembly { get; init; }

        public required PdbCheck Check { get; init; }
    }

    public sealed record AssemblyNotReloaded : AppProblem
    {
        public required string Assembly { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record AssembliesNotReloaded(Exception Error) : AppProblem;

    /// <summary>What a tab showed is gone from the assemblies read again.</summary>
    public sealed record GoneAfterReload(NodePath Location) : AppProblem;

    public sealed record AnalysisFailed : AppProblem
    {
        public required string Subject { get; init; }

        public required AnalysisKind Kind { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record AnalysesNotRestored(Exception Error) : AppProblem;

    /// <summary>No open assembly has what the documentation id <see cref="Id"/> names.</summary>
    public sealed record IdNotFound(string Id) : AppProblem;

    /// <summary>The command line's <see cref="Arguments"/> asked for nothing the app can do.</summary>
    public sealed record ArgumentsIgnored(IReadOnlyList<string> Arguments) : AppProblem;

    public sealed record NoApiReferencePage(string Name) : AppProblem;

    /// <summary>What <see cref="Reference"/> points at was not found, or finding it failed with <see cref="Error"/>.</summary>
    public sealed record Unresolved : AppProblem
    {
        public required CodeReference Reference { get; init; }

        public required Exception? Error { get; init; }
    }

    /// <summary>The place a history entry shows was not found, or finding it failed with <see cref="Error"/>.</summary>
    public sealed record HistoryPlaceGone : AppProblem
    {
        public required NodePath Place { get; init; }

        public required Exception? Error { get; init; }
    }

    /// <summary>The place a bookmark marks was not found, or finding it failed with <see cref="Error"/>.</summary>
    public sealed record BookmarkGone : AppProblem
    {
        public required string Bookmark { get; init; }

        public required NodePath Place { get; init; }

        public required Exception? Error { get; init; }
    }

    public sealed record BookmarksNotExported(string Reason) : AppProblem;

    public sealed record BookmarksNotImported : AppProblem
    {
        public required string File { get; init; }

        public required string Reason { get; init; }
    }

    /// <summary>The file at <see cref="Path"/>, given to import bookmarks from, holds no bookmarks list.</summary>
    public sealed record NotBookmarksFile(string Path) : AppProblem;

    /// <summary>The entries of <see cref="File"/> that <see cref="Problems"/> tells of are not bookmarks
    /// at all, so they were left out, or their place in this app's tree could not be read.</summary>
    public sealed record ImportedBookmarksUnreadable : AppProblem
    {
        public required string File { get; init; }

        public required IReadOnlyList<BookmarkEntryProblem> Problems { get; init; }
    }

    /// <summary>The places of the bookmarks <see cref="Lost"/> describes, read from <see cref="File"/>, were not found.</summary>
    public sealed record ImportedBookmarksLost : AppProblem
    {
        public required string File { get; init; }

        public required IReadOnlyList<string> Lost { get; init; }
    }

    /// <summary><see cref="Count"/> imported bookmarks were left out because others mark their lines.</summary>
    public sealed record BookmarksLeftOut(int Count) : AppProblem;

    public sealed record ComparisonFailed : AppProblem
    {
        public required string Left { get; init; }

        public required string Right { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Decompiling <see cref="Node"/> failed with <see cref="Error"/>.</summary>
    public sealed record DecompilationFailed : AppProblem
    {
        public required string Node { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>The type <see cref="Type"/> was not found for a diagram, or reading it failed with <see cref="Error"/>.</summary>
    public sealed record TypeNotDiagrammed : AppProblem
    {
        public required CodeReference.Entity Type { get; init; }

        public required Exception? Error { get; init; }
    }

    public sealed record DiagramNotReopened : AppProblem
    {
        public required string Diagram { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record DiagramNotLaidOut(Exception Error) : AppProblem;

    public sealed record ResourceNotSaved : AppProblem
    {
        public required string Resource { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record PackageFileNotExtracted : AppProblem
    {
        public required string File { get; init; }

        public required string Reason { get; init; }
    }

    /// <summary>The settings file at <see cref="Path"/> was read as <see cref="Problem"/> tells.</summary>
    public sealed record SettingsUnreadable : AppProblem
    {
        public required string? Path { get; init; }

        public required SettingsProblem Problem { get; init; }
    }

    public sealed record SettingsNotSaved(string Reason) : AppProblem;

    /// <summary>What is wrong in the theme file <see cref="File"/>, or in the themes it extends.</summary>
    public sealed record ThemeFileProblems : AppProblem
    {
        public required ThemeKind Kind { get; init; }

        public required string File { get; init; }

        public required IReadOnlyList<ThemeError> Errors { get; init; }
    }

    public sealed record ThemesFolderNotMade : AppProblem
    {
        public required ThemeKind Kind { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record CustomThemeNotWritten : AppProblem
    {
        public required ThemeKind Kind { get; init; }

        public required string Path { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record ThemesNotWatched : AppProblem
    {
        public required ThemeKind Kind { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record RuntimeNotScanned(Exception Error) : AppProblem;

    public sealed record NuGetConfigUnreadable(Exception Error) : AppProblem;

    public sealed record NuGetSearchFailed : AppProblem
    {
        public required string Feed { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record PackageNotDownloaded : AppProblem
    {
        public required string Package { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record SearchFailed(Exception Error) : AppProblem;

    /// <summary>Work started on the UI thread failed where it does not expect to fail.</summary>
    public sealed record Unexpected(Exception Error) : AppProblem;

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<OpenFailed, TResult> openFailed, Func<PdbUnusable, TResult> pdbUnusable,
        Func<AssemblyNotReloaded, TResult> assemblyNotReloaded,
        Func<AssembliesNotReloaded, TResult> assembliesNotReloaded, Func<GoneAfterReload, TResult> goneAfterReload,
        Func<AnalysisFailed, TResult> analysisFailed, Func<AnalysesNotRestored, TResult> analysesNotRestored,
        Func<IdNotFound, TResult> idNotFound, Func<ArgumentsIgnored, TResult> argumentsIgnored,
        Func<NoApiReferencePage, TResult> noApiReferencePage, Func<Unresolved, TResult> unresolved,
        Func<HistoryPlaceGone, TResult> historyPlaceGone, Func<BookmarkGone, TResult> bookmarkGone,
        Func<BookmarksNotExported, TResult> bookmarksNotExported,
        Func<BookmarksNotImported, TResult> bookmarksNotImported, Func<NotBookmarksFile, TResult> notBookmarksFile,
        Func<ImportedBookmarksUnreadable, TResult> importedBookmarksUnreadable,
        Func<ImportedBookmarksLost, TResult> importedBookmarksLost, Func<BookmarksLeftOut, TResult> bookmarksLeftOut,
        Func<ComparisonFailed, TResult> comparisonFailed, Func<DecompilationFailed, TResult> decompilationFailed,
        Func<TypeNotDiagrammed, TResult> typeNotDiagrammed, Func<DiagramNotReopened, TResult> diagramNotReopened,
        Func<DiagramNotLaidOut, TResult> diagramNotLaidOut, Func<ResourceNotSaved, TResult> resourceNotSaved,
        Func<PackageFileNotExtracted, TResult> packageFileNotExtracted,
        Func<SettingsUnreadable, TResult> settingsUnreadable, Func<SettingsNotSaved, TResult> settingsNotSaved,
        Func<ThemeFileProblems, TResult> themeFileProblems, Func<ThemesFolderNotMade, TResult> themesFolderNotMade,
        Func<CustomThemeNotWritten, TResult> customThemeNotWritten, Func<ThemesNotWatched, TResult> themesNotWatched,
        Func<RuntimeNotScanned, TResult> runtimeNotScanned, Func<NuGetConfigUnreadable, TResult> nuGetConfigUnreadable,
        Func<NuGetSearchFailed, TResult> nuGetSearchFailed, Func<PackageNotDownloaded, TResult> packageNotDownloaded,
        Func<SearchFailed, TResult> searchFailed, Func<Unexpected, TResult> unexpected)
    {
        return this switch
        {
            OpenFailed c => openFailed(c),
            PdbUnusable c => pdbUnusable(c),
            AssemblyNotReloaded c => assemblyNotReloaded(c),
            AssembliesNotReloaded c => assembliesNotReloaded(c),
            GoneAfterReload c => goneAfterReload(c),
            AnalysisFailed c => analysisFailed(c),
            AnalysesNotRestored c => analysesNotRestored(c),
            IdNotFound c => idNotFound(c),
            ArgumentsIgnored c => argumentsIgnored(c),
            NoApiReferencePage c => noApiReferencePage(c),
            Unresolved c => unresolved(c),
            HistoryPlaceGone c => historyPlaceGone(c),
            BookmarkGone c => bookmarkGone(c),
            BookmarksNotExported c => bookmarksNotExported(c),
            BookmarksNotImported c => bookmarksNotImported(c),
            NotBookmarksFile c => notBookmarksFile(c),
            ImportedBookmarksUnreadable c => importedBookmarksUnreadable(c),
            ImportedBookmarksLost c => importedBookmarksLost(c),
            BookmarksLeftOut c => bookmarksLeftOut(c),
            ComparisonFailed c => comparisonFailed(c),
            DecompilationFailed c => decompilationFailed(c),
            TypeNotDiagrammed c => typeNotDiagrammed(c),
            DiagramNotReopened c => diagramNotReopened(c),
            DiagramNotLaidOut c => diagramNotLaidOut(c),
            ResourceNotSaved c => resourceNotSaved(c),
            PackageFileNotExtracted c => packageFileNotExtracted(c),
            SettingsUnreadable c => settingsUnreadable(c),
            SettingsNotSaved c => settingsNotSaved(c),
            ThemeFileProblems c => themeFileProblems(c),
            ThemesFolderNotMade c => themesFolderNotMade(c),
            CustomThemeNotWritten c => customThemeNotWritten(c),
            ThemesNotWatched c => themesNotWatched(c),
            RuntimeNotScanned c => runtimeNotScanned(c),
            NuGetConfigUnreadable c => nuGetConfigUnreadable(c),
            NuGetSearchFailed c => nuGetSearchFailed(c),
            PackageNotDownloaded c => packageNotDownloaded(c),
            SearchFailed c => searchFailed(c),
            Unexpected c => unexpected(c),
            _ => throw new UnreachableException($"AppProblem.Match does not handle {GetType().Name}."),
        };
    }
}
