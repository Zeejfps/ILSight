using ILSight.Core;
using ILSight.Core.AnalysisArea;
using ILSight.BookmarkArea;
using ILSight.SettingArea;

namespace ILSight;

/// <summary>Something that went wrong, worth telling the user, as what happened rather than words, for
/// the screen to tell in the chosen language. A <c>Reason</c> is as the system said it.</summary>
public union AppProblem(AppProblem.OpenFailed, AppProblem.PdbUnusable, AppProblem.AssemblyNotReloaded,
    AppProblem.AssembliesNotReloaded, AppProblem.AnalysisFailed, AppProblem.AnalysesNotRestored, AppProblem.Unresolved,
    AppProblem.UnresolvedFileDenied, AppProblem.UnresolvedFileDamaged, AppProblem.PlaceLookupFailed,
    AppProblem.PlaceLookupFileDenied, AppProblem.PlaceLookupFileDamaged, AppProblem.ReferenceFileDenied,
    AppProblem.ReferenceFileDamaged, AppProblem.ModuleUnreadable, AppProblem.BookmarksNotExported,
    AppProblem.BookmarksNotImported, AppProblem.ImportedBookmarksUnreadable, AppProblem.ImportedBookmarksNotPlaced,
    AppProblem.ComparisonFailed, AppProblem.DecompilationFailed, AppProblem.TypeNotDiagrammed,
    AppProblem.DiagramNotReopened, AppProblem.DiagramNotLaidOut, AppProblem.ResourceNotSaved,
    AppProblem.PackageFileNotExtracted, AppProblem.SettingsUnreadable, AppProblem.SettingsNotSaved,
    AppProblem.ThemeFileProblems, AppProblem.ThemesFolderNotMade, AppProblem.CustomThemeNotWritten,
    AppProblem.ThemesNotWatched, AppProblem.RuntimeNotScanned, AppProblem.NuGetConfigUnreadable,
    AppProblem.NuGetSearchFailed, AppProblem.PackageNotDownloaded, AppProblem.Unexpected)
{
    public sealed record OpenFailed(Exception Error);

    /// <summary>The PDB at <see cref="PdbPath"/> could not be read for the assembly <see cref="Assembly"/> (it is no PDB, or
    /// reading it failed); one read that doesn't match the assembly is a refusal (<see cref="Refusal.PdbUnusable"/>).</summary>
    public sealed record PdbUnusable
    {
        public required string PdbPath { get; init; }

        public required string Assembly { get; init; }

        public required PdbCheck Check { get; init; }
    }

    public sealed record AssemblyNotReloaded
    {
        public required string Assembly { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record AssembliesNotReloaded(Exception Error);

    public sealed record AnalysisFailed
    {
        public required string Subject { get; init; }

        public required AnalysisKind Kind { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record AnalysesNotRestored(Exception Error);

    /// <summary>Finding what <see cref="Reference"/> points at failed with <see cref="Error"/>; not finding it is a
    /// refusal (<see cref="Refusal.Unresolved"/>).</summary>
    public sealed record Unresolved
    {
        public required CodeReference Reference { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Finding what <see cref="Reference"/> points at needed <see cref="File"/>, which is there but could not be
    /// opened (access denied, or another program holds it): readable again, the lookup can be asked again.</summary>
    public sealed record UnresolvedFileDenied
    {
        public required CodeReference Reference { get; init; }

        public required AssemblyPath File { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Finding what <see cref="Reference"/> points at needed <see cref="File"/>, which is there but is no .NET
    /// assembly or package that can be read: asking again fails the same way until the file is replaced.</summary>
    public sealed record UnresolvedFileDamaged
    {
        public required CodeReference Reference { get; init; }

        public required AssemblyPath File { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Looking up the place <see cref="Place"/> failed with <see cref="Error"/>: one kind wherever the lookup was
    /// asked for, a history entry's, a bookmark's, a reopened or reloaded tab's. Not finding it is a refusal where the
    /// user asked (<see cref="Refusal.HistoryPlaceGone"/>, <see cref="Refusal.BookmarkGone"/>).</summary>
    public sealed record PlaceLookupFailed
    {
        public required NodePath Place { get; init; }

        /// <summary>The step that asked, said beside the failure; not part of its kind.</summary>
        public required PlaceLookupStep Step { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Looking up the place <see cref="Place"/> needed <see cref="File"/>, which is there but could not be opened
    /// (access denied, or another program holds it): readable again, the place can be looked up again.</summary>
    public sealed record PlaceLookupFileDenied
    {
        public required NodePath Place { get; init; }

        /// <summary>The step that asked, said beside the failure; not part of its kind.</summary>
        public required PlaceLookupStep Step { get; init; }

        public required AssemblyPath File { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Looking up the place <see cref="Place"/> needed <see cref="File"/>, which is there but is no .NET assembly
    /// or package that can be read: looking again fails the same way until the file is replaced.</summary>
    public sealed record PlaceLookupFileDamaged
    {
        public required NodePath Place { get; init; }

        /// <summary>The step that asked, said beside the failure; not part of its kind.</summary>
        public required PlaceLookupStep Step { get; init; }

        public required AssemblyPath File { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Decompiling <see cref="Unread"/>'s module found a file for that reference which is there but could not be
    /// opened (access denied, or another program holds it): the module's code reads the reference by name only until
    /// the module is read again with the file readable. Reported once, by the first tab that showed such code.</summary>
    public sealed record ReferenceFileDenied
    {
        public required UnreadReference Unread { get; init; }
    }

    /// <summary>Decompiling <see cref="Unread"/>'s module found a file for that reference which is there but is no .NET
    /// assembly that can be read: the module's code reads the reference by name only until the file is replaced and
    /// the module read again. Reported once, as above.</summary>
    public sealed record ReferenceFileDamaged
    {
        public required UnreadReference Unread { get; init; }
    }

    /// <summary>What was asked for is in <see cref="Module"/>, a file whose type system the library could not read,
    /// with <see cref="Why"/>, so it was not looked for.</summary>
    public sealed record ModuleUnreadable
    {
        /// <summary>The module's file name.</summary>
        public required string Module { get; init; }

        public required Exception Why { get; init; }

        /// <summary>The problem of <paramref name="unreadable"/>, which a lookup could not read.</summary>
        public static ModuleUnreadable FromModule(UnreadableModule unreadable)
        {
            return new ModuleUnreadable { Module = unreadable.Module.FilePath.FileName, Why = unreadable.Why };
        }
    }

    public sealed record BookmarksNotExported(string Reason);

    public sealed record BookmarksNotImported
    {
        public required string File { get; init; }

        public required string Reason { get; init; }
    }

    /// <summary>The entries of <see cref="File"/> that <see cref="Problems"/> tells of are not bookmarks
    /// at all, so they were left out, or their place in this app's tree could not be read.</summary>
    public sealed record ImportedBookmarksUnreadable
    {
        public required string File { get; init; }

        public required IReadOnlyList<BookmarkEntryProblem> Problems { get; init; }
    }

    /// <summary>Looking up where some bookmarks imported from <see cref="File"/> go failed, each with why: they were left
    /// out, and the others settled as their own lookups came out.</summary>
    public sealed record ImportedBookmarksNotPlaced
    {
        public required string File { get; init; }

        /// <summary>Each bookmark left out, as the refusal line describes one lost, and why its lookup failed.</summary>
        public required IReadOnlyList<(LostBookmark Bookmark, Exception Error)> Failed { get; init; }
    }

    public sealed record ComparisonFailed
    {
        public required string Left { get; init; }

        public required string Right { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Decompiling <see cref="Node"/> failed with <see cref="Error"/>.</summary>
    public sealed record DecompilationFailed
    {
        public required string Node { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Reading the type <see cref="Type"/> for a diagram failed with <see cref="Error"/>; not finding it is a
    /// refusal (<see cref="Refusal.TypeNotDiagrammed"/>).</summary>
    public sealed record TypeNotDiagrammed
    {
        public required CodeReference.Entity Type { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record DiagramNotReopened
    {
        public required TabTitle Diagram { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record DiagramNotLaidOut(Exception Error);

    public sealed record ResourceNotSaved
    {
        public required string Resource { get; init; }

        /// <summary>What failed, said from its cause (<see cref="FailureTexts"/>).</summary>
        public required Exception Error { get; init; }
    }

    public sealed record PackageFileNotExtracted
    {
        public required string File { get; init; }

        /// <summary>What failed, said from its cause (<see cref="FailureTexts"/>).</summary>
        public required Exception Error { get; init; }
    }

    /// <summary>The settings file at <see cref="Path"/> was read as <see cref="Problem"/> tells.</summary>
    public sealed record SettingsUnreadable
    {
        public required string? Path { get; init; }

        public required SettingsProblem Problem { get; init; }
    }

    public sealed record SettingsNotSaved(string Reason);

    /// <summary>What is wrong in the theme file <see cref="File"/>, or in the themes it extends.</summary>
    public sealed record ThemeFileProblems
    {
        public required ThemeKind Kind { get; init; }

        public required string File { get; init; }

        public required IReadOnlyList<ThemeError> Errors { get; init; }
    }

    public sealed record ThemesFolderNotMade
    {
        public required ThemeKind Kind { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record CustomThemeNotWritten
    {
        public required ThemeKind Kind { get; init; }

        public required string Path { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record ThemesNotWatched
    {
        public required ThemeKind Kind { get; init; }

        public required string Reason { get; init; }
    }

    public sealed record RuntimeNotScanned(Exception Error);

    public sealed record NuGetConfigUnreadable(Exception Error);

    public sealed record NuGetSearchFailed
    {
        public required string Feed { get; init; }

        public required Exception Error { get; init; }
    }

    public sealed record PackageNotDownloaded
    {
        public required string Package { get; init; }

        public required Exception Error { get; init; }
    }

    /// <summary>Work started on the UI thread failed where it does not expect to fail.</summary>
    public sealed record Unexpected(Exception Error);
}
