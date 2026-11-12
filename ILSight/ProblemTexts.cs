using System.Diagnostics;
using ILSight.Core;
using ILSight.Localization;
using ILSight.Bookmarking;
using ILSight.SettingsKeeping;

namespace ILSight;

/// <summary>What went wrong in the words of the interface language.</summary>
public static class ProblemTexts
{
    private const int SummaryLength = 160;

    /// <summary>The title and the first line of the details on one line.</summary>
    public static string Summarize(string title, string details)
    {
        var end = details.AsSpan().IndexOfAny('\r', '\n');
        var first = (end < 0 ? details : details[..end]).Trim();
        var summary = first.Length == 0 ? title : $"{title} — {first}";
        return summary.Length > SummaryLength ? summary[..SummaryLength] + "…" : summary;
    }

    public static ProblemText Describe(Strings t, AppProblem problem)
    {
        var (title, details) = Words(t, problem);
        return new ProblemText(title, details.Trim());
    }

    private static (string Title, string Details) Words(Strings t, AppProblem problem)
    {
        return problem switch
        {
            AppProblem.OpenFailed p => (t.ErrorOpenAssemblies, p.Error.ToString()),
            AppProblem.PdbUnusable p => (t.ErrorSelectPdb(Path.GetFileName(p.PdbPath), p.Assembly),
                DescribePdbCheck(t, p.Check, p.Assembly) + Environment.NewLine + p.PdbPath),
            AppProblem.AssemblyNotReloaded p => (t.ErrorReloadAssembly(p.Assembly), p.Error.ToString()),
            AppProblem.AssembliesNotReloaded p => (t.ErrorReloadAssemblies, p.Error.ToString()),
            AppProblem.GoneAfterReload p => (t.ErrorFindAfterReload(p.Location.Label), p.Location.ToString()),
            AppProblem.AnalysisFailed p => (t.ErrorAnalyze(p.Subject, AnalyzerText.Header(t, p.Kind)), p.Error.ToString()),
            AppProblem.AnalysesNotRestored p => (t.ErrorRestoreAnalyses, p.Error.ToString()),
            AppProblem.IdNotFound p => (t.ErrorNavigateTo(p.Id), ""),
            AppProblem.ArgumentsIgnored p => (t.ErrorArgumentsIgnored(p.Arguments.Count), string.Join(Environment.NewLine, p.Arguments)),
            AppProblem.NoApiReferencePage p => (t.ErrorNoApiReferencePage(p.Name), t.ErrorNoApiReferencePageDetails),
            AppProblem.Unresolved p => (t.ErrorResolve(p.Reference.Name), Failure(p.Error, t.ErrorNoDefinition(p.Reference))),
            AppProblem.HistoryPlaceGone p => (t.ErrorGoBack(p.Place.Label), Failure(p.Error, t.ErrorNoLongerThere(p.Place))),
            AppProblem.BookmarkGone p => (t.ErrorGoToBookmark(p.Bookmark), Failure(p.Error, t.ErrorNoLongerThere(p.Place))),
            AppProblem.BookmarksNotExported p => (t.ErrorExportBookmarks, p.Reason),
            AppProblem.BookmarksNotImported p => (t.ErrorImportBookmarks(p.File), p.Reason),
            AppProblem.NotBookmarksFile p => (t.ErrorImportBookmarks(Path.GetFileName(p.Path)), $"{p.Path}{Environment.NewLine}{t.ErrorNotBookmarksFile}"),
            AppProblem.ImportedBookmarksUnreadable p => (t.ErrorBookmarksUnreadable(p.File), DescribeEntryProblems(t, p.Problems)),
            AppProblem.ImportedBookmarksLost p => (t.ErrorBookmarksNotFound(p.File), string.Join(Environment.NewLine, p.Lost)),
            AppProblem.BookmarksLeftOut p => (t.ErrorDuplicateBookmarks(p.Count), ""),
            AppProblem.ComparisonFailed p => (t.ErrorCompare(p.Left, p.Right), p.Error.ToString()),
            AppProblem.DecompilationFailed p => (t.ErrorDecompile(p.Node), p.Error.ToString()),
            AppProblem.TypeNotDiagrammed p => (t.ErrorShowInDiagram(p.Type.Name), Failure(p.Error, t.ErrorNoDefinition(p.Type))),
            AppProblem.DiagramNotReopened p => (t.ErrorShowInDiagram(p.Diagram), p.Error.ToString()),
            AppProblem.DiagramNotLaidOut p => (t.ErrorLayOutDiagram, p.Error.ToString()),
            AppProblem.ResourceNotSaved p => (t.ErrorSaveResource(p.Resource), p.Reason),
            AppProblem.PackageFileNotExtracted p => (t.ErrorExtractFile(p.File), p.Reason),
            AppProblem.SettingsUnreadable p => (SettingsTitle(t, p.Problem), $"{p.Path}{Environment.NewLine}{DescribeSettings(t, p.Problem)}"),
            AppProblem.SettingsNotSaved p => (t.ErrorSaveSettings, p.Reason),
            AppProblem.ThemeFileProblems p => (ThemeFileTitle(t, p.Kind, Path.GetFileName(p.File)), DescribeThemeErrors(t, p.Errors)),
            AppProblem.ThemesFolderNotMade p => (p.Kind == ThemeKind.Color ? t.ErrorThemesFolder : t.ErrorIconThemesFolder, p.Reason),
            AppProblem.CustomThemeNotWritten p => (p.Kind == ThemeKind.Color ? t.ErrorCustomTheme : t.ErrorCustomIconTheme,
                $"{p.Path}{Environment.NewLine}{p.Reason}"),
            AppProblem.ThemesNotWatched p => (p.Kind == ThemeKind.Color ? t.ErrorWatchThemes : t.ErrorWatchIconThemes, p.Reason),
            AppProblem.RuntimeNotScanned p => (t.ErrorScanRuntime, p.Error.ToString()),
            AppProblem.NuGetConfigUnreadable p => (t.ErrorReadNugetConfig, p.Error.ToString()),
            AppProblem.NuGetSearchFailed p => (t.ErrorSearchNuget(p.Feed), p.Error.ToString()),
            AppProblem.PackageNotDownloaded p => (t.ErrorDownloadPackage(p.Package), p.Error.ToString()),
            AppProblem.SearchFailed p => (t.ErrorSearch, p.Error.ToString()),
            AppProblem.Unexpected p => (t.ErrorUnexpected, p.Error.ToString()),
            _ => throw new UnreachableException(),
        };
    }

    private static string Failure(Exception? error, string fallback)
    {
        if (error is null)
        {
            return fallback;
        }

        return error.ToString();
    }

    private static string DescribeEntryProblems(Strings t, IReadOnlyList<BookmarkEntryProblem> problems)
    {
        return string.Join(Environment.NewLine, problems.Select(p => DescribeEntryProblem(t, p)));
    }

    private static string DescribeEntryProblem(Strings t, BookmarkEntryProblem problem)
    {
        return problem.Match(
            placeUnreadable: p => t.ErrorBookmarkPlaceUnreadable(p.Entry),
            notABookmark: n => $"#{n.Entry}: {n.Detail}");
    }

    private static string DescribePdbCheck(Strings t, PdbCheck check, string name)
    {
        return check switch
        {
            PdbCheck.OtherBuild => t.ErrorPdbOtherBuild(name),
            PdbCheck.WindowsPdb => t.ErrorPdbWindows,
            PdbCheck.Unreadable => t.ErrorPdbUnreadable,
            _ => throw new UnreachableException(),
        };
    }

    private static string SettingsTitle(Strings t, SettingsProblem problem)
    {
        return problem switch
        {
            SettingsProblem.NotRead or SettingsProblem.NotJson or SettingsProblem.Empty => t.ErrorReadSettings,
            SettingsProblem.ValuesLeftOut => t.ErrorReadSomeSettings,
            SettingsProblem.LayoutPartsMissing or SettingsProblem.LayoutPartsRepeated => t.ErrorReadLayout,
            _ => throw new UnreachableException(),
        };
    }

    private static string DescribeSettings(Strings t, SettingsProblem problem)
    {
        return problem switch
        {
            SettingsProblem.NotRead notRead => notRead.Detail + Environment.NewLine + DescribeKept(t, notRead.Kept),
            SettingsProblem.NotJson notJson => t.ErrorSettingsNotJson(notJson.Detail) + Environment.NewLine + DescribeKept(t, notJson.Kept),
            SettingsProblem.ValuesLeftOut leftOut => t.ErrorSettingsValuesLeftOut(string.Join(", ", leftOut.Fields)) + Environment.NewLine + DescribeKept(t, leftOut.Kept),
            SettingsProblem.Empty => t.ErrorSettingsEmpty,
            SettingsProblem.LayoutPartsMissing => t.ErrorLayoutPartsMissing,
            SettingsProblem.LayoutPartsRepeated => t.ErrorLayoutPartsRepeated,
            _ => throw new UnreachableException(),
        };
    }

    private static string DescribeKept(Strings t, SettingsKept kept)
    {
        return kept switch
        {
            SettingsKept.CopiedTo copied => t.ErrorSettingsKeptAs(copied.Path),
            SettingsKept.NotSaved => t.ErrorSettingsNotSaved,
            _ => throw new UnreachableException(),
        };
    }

    private static string ThemeFileTitle(Strings t, ThemeKind kind, string file)
    {
        return kind switch
        {
            ThemeKind.Color => t.ErrorTheme(file),
            ThemeKind.Icon => t.ErrorIconTheme(file),
            _ => throw new UnreachableException(),
        };
    }

    private static string DescribeThemeErrors(Strings t, IReadOnlyList<ThemeError> errors)
    {
        return string.Join(Environment.NewLine, errors.Select(e => DescribeThemeError(t, e)));
    }

    /// <summary><paramref name="error"/> as a line of a report: the file, the line when known, and what is wrong.</summary>
    private static string DescribeThemeError(Strings t, ThemeError error)
    {
        var problem = DescribeThemeProblem(t, error);
        if (error.Line == 0)
        {
            return $"{error.File}: {problem}";
        }

        return $"{error.File}({error.Line}): {problem}";
    }

    private static string DescribeThemeProblem(Strings t, ThemeError error)
    {
        return error.Problem switch
        {
            ThemeProblem.Unreadable => t.ThemeErrorUnreadable(error.Detail),
            ThemeProblem.InvalidJson => t.ThemeErrorInvalidJson(error.Detail),
            ThemeProblem.RootNotAnObject => t.ThemeErrorRootNotAnObject,
            ThemeProblem.NotAnObject => t.ThemeErrorNotAnObject(error.Subject),
            ThemeProblem.NotAString => t.ThemeErrorNotAString(error.Subject),
            ThemeProblem.InvalidName => t.ThemeErrorInvalidName,
            ThemeProblem.DuplicateLanguage => t.ThemeErrorDuplicateLanguage(error.Subject),
            ThemeProblem.UnknownKey => t.ThemeErrorUnknownKey(error.Subject),
            ThemeProblem.UnknownType => t.ThemeErrorUnknownType(error.Subject),
            ThemeProblem.NotAColor => t.ThemeErrorNotAColor(error.Subject, error.Detail),
            ThemeProblem.TransparentBlack => t.ThemeErrorTransparentBlack(error.Subject),
            ThemeProblem.UnknownPaletteName => t.ThemeErrorUnknownPaletteName(error.Subject, error.Detail),
            ThemeProblem.NoType => t.ThemeErrorNoType,
            ThemeProblem.UnknownParent => t.ThemeErrorUnknownParent(error.Subject),
            ThemeProblem.ExtendsCycle => t.ThemeErrorExtendsCycle(error.Detail),
            ThemeProblem.MissingColors => t.ThemeErrorMissingColors(error.Detail),
            ThemeProblem.IdTaken => t.ThemeErrorIdTaken(error.Subject),
            ThemeProblem.NotACodepoint => t.ThemeErrorNotACodepoint(error.Subject, error.Detail),
            ThemeProblem.InvalidIcon => t.ThemeErrorInvalidIcon(error.Subject),
            ThemeProblem.UnknownFont => t.ThemeErrorUnknownFont(error.Subject, error.Detail),
            ThemeProblem.AssetOutsideFolder => t.ThemeErrorAssetOutsideFolder(error.Subject, error.Detail),
            ThemeProblem.AssetUnreadable => t.ThemeErrorAssetUnreadable(error.Subject, error.Detail),
            ThemeProblem.NotAFont => t.ThemeErrorNotAFont(error.Subject, error.Detail),
            ThemeProblem.InvalidSvg => t.ThemeErrorInvalidSvg(error.Subject, error.Detail),
            ThemeProblem.MissingGlyph => t.ThemeErrorMissingGlyph(error.Subject, error.Detail),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Problem, null),
        };
    }
}
