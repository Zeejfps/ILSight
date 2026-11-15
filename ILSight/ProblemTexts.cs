using System.Diagnostics;
using ILSight.Core;
using ILSight.BookmarkArea;
using ILSight.SettingArea;

namespace ILSight;

/// <summary>What went wrong in the words of the interface language.</summary>
public static class ProblemTexts
{
    private const int SummaryLength = 160;

    /// <summary>The title and the first line of the details on one line, joined as <paramref name="t"/>'s language
    /// joins them (<c>error.summary</c>).</summary>
    public static string Summarize(Strings t, string title, string details)
    {
        var end = details.AsSpan().IndexOfAny('\r', '\n');
        var first = (end < 0 ? details : details[..end]).Trim();
        var summary = first.Length == 0 ? title : t.ErrorSummary(title: title, details: first);
        return summary.Length > SummaryLength ? summary[..SummaryLength] + "…" : summary;
    }

    public static ProblemText Describe(Strings t, AppProblem problem)
    {
        var (title, details) = Words(t, problem);
        return ProblemText.FromWords(t, title, details.Trim());
    }

    private static (string Title, string Details) Words(Strings t, AppProblem problem)
    {
        return problem.Match<(string Title, string Details)>(
            openFailed: p => (t.ErrorOpenAssemblies, p.Error.ToString()),
            pdbUnusable: p => (t.ErrorSelectPdb(file: Path.GetFileName(p.PdbPath), name: p.Assembly),
                DescribePdbCheck(t, p.Check, p.Assembly) + Environment.NewLine + p.PdbPath),
            assemblyNotReloaded: p => (t.ErrorReloadAssembly(p.Assembly), p.Error.ToString()),
            assembliesNotReloaded: p => (t.ErrorReloadAssemblies, p.Error.ToString()),
            analysisFailed: p => (t.ErrorAnalyze(name: p.Subject, analysis: AnalyzerText.Header(t, p.Kind)), p.Error.ToString()),
            analysesNotRestored: p => (t.ErrorRestoreAnalyses, p.Error.ToString()),
            unresolved: p => (t.ErrorResolve(p.Reference.Name), p.Error.ToString()),
            unresolvedFileDenied: p => (t.ErrorResolve(p.Reference.Name), Advise(Denied(t, p.File), p.Error)),
            unresolvedFileDamaged: p => (t.ErrorResolve(p.Reference.Name), Advise(Damaged(t, p.File), p.Error)),
            placeLookupFailed: p => (t.ErrorLookUpPlace(p.Place.Label), AfterStep(t, p.Step, p.Error.ToString())),
            placeLookupFileDenied: p => (t.ErrorLookUpPlace(p.Place.Label),
                AfterStep(t, p.Step, Advise(Denied(t, p.File), p.Error))),
            placeLookupFileDamaged: p => (t.ErrorLookUpPlace(p.Place.Label),
                AfterStep(t, p.Step, Advise(Damaged(t, p.File), p.Error))),
            referenceFileDenied: p => (t.ErrorReadReference(name: p.Unread.Name, assembly: p.Unread.Module),
                Degraded(t, p.Unread, t.ErrorFileDenied(p.Unread.Error.FilePath.FileName))),
            referenceFileDamaged: p => (t.ErrorReadReference(name: p.Unread.Name, assembly: p.Unread.Module),
                Degraded(t, p.Unread, t.ErrorFileDamaged(p.Unread.Error.FilePath.FileName))),
            moduleUnreadable: p => (t.CauseModuleUnreadableTitle(p.Module), p.Why.ToString()),
            bookmarksNotExported: p => (t.ErrorExportBookmarks, p.Reason),
            bookmarksNotImported: p => (t.ErrorImportBookmarks(p.File), p.Reason),
            importedBookmarksUnreadable: p => (t.ErrorBookmarksUnreadable(p.File), DescribeEntryProblems(t, p.Problems)),
            importedBookmarksNotPlaced: p => (t.ErrorBookmarkPlacesFailed(p.File), DescribePlaceFailures(t, p.Failed)),
            comparisonFailed: p => (t.ErrorCompare(left: p.Left, right: p.Right), p.Error.ToString()),
            decompilationFailed: p => (t.ErrorDecompile(p.Node), p.Error.ToString()),
            typeNotDiagrammed: p => (t.ErrorShowInDiagram(p.Type.Name), p.Error.ToString()),
            diagramNotReopened: p => (t.ErrorShowInDiagram(p.Diagram), p.Error.ToString()),
            diagramNotLaidOut: p => (t.ErrorLayOutDiagram, p.Error.ToString()),
            resourceNotSaved: p => (t.ErrorSaveResource(p.Resource), p.Reason),
            packageFileNotExtracted: p => (t.ErrorExtractFile(p.File), p.Reason),
            settingsUnreadable: p => (SettingsTitle(t, p.Problem), $"{p.Path}{Environment.NewLine}{DescribeSettings(t, p.Problem)}"),
            settingsNotSaved: p => (t.ErrorSaveSettings, p.Reason),
            themeFileProblems: p => (ThemeFileTitle(t, p.Kind, Path.GetFileName(p.File)), DescribeThemeErrors(t, p.Errors)),
            themesFolderNotMade: p => (p.Kind == ThemeKind.Color ? t.ErrorThemesFolder : t.ErrorIconThemesFolder, p.Reason),
            customThemeNotWritten: p => (p.Kind == ThemeKind.Color ? t.ErrorCustomTheme : t.ErrorCustomIconTheme,
                $"{p.Path}{Environment.NewLine}{p.Reason}"),
            themesNotWatched: p => (p.Kind == ThemeKind.Color ? t.ErrorWatchThemes : t.ErrorWatchIconThemes, p.Reason),
            runtimeNotScanned: p => (t.ErrorScanRuntime, p.Error.ToString()),
            nuGetConfigUnreadable: p => (t.CauseNugetConfigUnreadableTitle, p.Error.ToString()),
            nuGetSearchFailed: p => (t.ErrorSearchNuget(p.Feed), p.Error.ToString()),
            packageNotDownloaded: p => (t.ErrorDownloadPackage(p.Package), p.Error.ToString()),
            unexpected: p => (t.ErrorUnexpected, p.Error.ToString()));
    }

    // The step that asked for the lookup on its own line, then the rest of the details.
    private static string AfterStep(Strings t, PlaceLookupStep step, string details)
    {
        var asked = step.Match(
            goingToBookmark: s => t.ErrorStepBookmark(s.Name),
            goingThroughHistory: _ => t.ErrorStepHistory,
            reopeningTabs: _ => t.ErrorStepReopenTabs,
            reloadingTab: _ => t.ErrorStepReloadTab);
        return asked + Environment.NewLine + Environment.NewLine + details;
    }

    // A file that couldn't be opened, as the cause of a lookup that reads it again when asked again: the cause in its
    // words and its recovery, each defined once (error.file_denied, error.recovery_make_readable) for every kind whose
    // cause it is.
    private static string Denied(Strings t, AssemblyPath file)
    {
        return t.ErrorCauseAndRecovery(cause: t.ErrorFileDenied(file.FileName), recovery: t.ErrorRecoveryMakeReadable);
    }

    // A file that isn't one that can be read, as the cause of a lookup, as Denied.
    private static string Damaged(Strings t, AssemblyPath file)
    {
        return t.ErrorCauseAndRecovery(cause: t.ErrorFileDamaged(file.FileName), recovery: t.ErrorRecoveryReplaceFile);
    }

    // What the code shows without the reference and the step that reads it again, the cause, then the error as it came.
    // The code is kept, so the cause's own "try again" would reuse it: the step named is the reload.
    private static string Degraded(Strings t, UnreadReference unread, string cause)
    {
        return t.ErrorReadReferenceDegraded(assembly: unread.Module, name: unread.Name) + Environment.NewLine + Environment.NewLine + Advise(cause, unread.Error);
    }

    // The cause and what the user can do about it first, then the error as it came.
    private static string Advise(string advice, Exception error)
    {
        return advice + Environment.NewLine + Environment.NewLine + error;
    }

    private static string DescribeEntryProblems(Strings t, IReadOnlyList<BookmarkEntryProblem> problems)
    {
        return string.Join(Environment.NewLine, problems.Select(p => DescribeEntryProblem(t, p)));
    }

    private static string DescribePlaceFailures(Strings t, IReadOnlyList<(LostBookmark Bookmark, Exception Error)> failed)
    {
        return string.Join(Environment.NewLine, failed.Select(f => $"{f.Bookmark.Words(t)}: {f.Error}"));
    }

    private static string DescribeEntryProblem(Strings t, BookmarkEntryProblem problem)
    {
        return problem.Match(
            placeUnreadable: p => t.ErrorBookmarkPlaceUnreadable(p.Entry),
            notABookmark: n => $"#{n.Entry}: {n.Detail}");
    }

    internal static string DescribePdbCheck(Strings t, PdbCheck check, string name)
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
        return problem.Match(
            none: static _ => throw new UnreachableException("SettingsProblem.None is no problem to tell of."),
            notRead: _ => t.ErrorReadSettings,
            notJson: _ => t.ErrorReadSettings,
            valuesLeftOut: _ => t.ErrorReadSomeSettings,
            empty: _ => t.ErrorReadSettings,
            layoutPartsMissing: _ => t.ErrorReadLayout,
            layoutPartsRepeated: _ => t.ErrorReadLayout);
    }

    private static string DescribeSettings(Strings t, SettingsProblem problem)
    {
        return problem.Match(
            none: static _ => throw new UnreachableException("SettingsProblem.None is no problem to tell of."),
            notRead: notRead => notRead.Detail + Environment.NewLine + DescribeKept(t, notRead.Kept),
            notJson: notJson => t.ErrorSettingsNotJson(notJson.Detail) + Environment.NewLine + DescribeKept(t, notJson.Kept),
            valuesLeftOut: leftOut => t.ErrorSettingsValuesLeftOut(string.Join(", ", leftOut.Fields)) + Environment.NewLine + DescribeKept(t, leftOut.Kept),
            empty: _ => t.ErrorSettingsEmpty,
            layoutPartsMissing: _ => t.ErrorLayoutPartsMissing,
            layoutPartsRepeated: _ => t.ErrorLayoutPartsRepeated);
    }

    private static string DescribeKept(Strings t, SettingsKept kept)
    {
        return kept.Match(
            copied => t.ErrorSettingsKeptAs(copied.Path),
            _ => t.ErrorSettingsNotSaved);
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

    // A notation, the same in every language, as compilers write a diagnostic, so that an editor's jump to the line
    // reads it: the file, the line when there is one, a colon, then the problem, the only part translated.
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
            ThemeProblem.NotAColor => t.ThemeErrorNotAColor(key: error.Subject, value: error.Detail),
            ThemeProblem.TransparentBlack => t.ThemeErrorTransparentBlack(error.Subject),
            ThemeProblem.UnknownPaletteName => t.ThemeErrorUnknownPaletteName(key: error.Subject, name: error.Detail),
            ThemeProblem.NoType => t.ThemeErrorNoType,
            ThemeProblem.UnknownParent => t.ThemeErrorUnknownParent(error.Subject),
            ThemeProblem.ExtendsCycle => t.ThemeErrorExtendsCycle(error.Detail),
            ThemeProblem.MissingColors => t.ThemeErrorMissingColors(error.Detail),
            ThemeProblem.IdTaken => t.ThemeErrorIdTaken(error.Subject),
            ThemeProblem.NotACodepoint => t.ThemeErrorNotACodepoint(key: error.Subject, glyph: error.Detail),
            ThemeProblem.InvalidIcon => t.ThemeErrorInvalidIcon(error.Subject),
            ThemeProblem.UnknownFont => t.ThemeErrorUnknownFont(key: error.Subject, font: error.Detail),
            ThemeProblem.AssetOutsideFolder => t.ThemeErrorAssetOutsideFolder(key: error.Subject, path: error.Detail),
            ThemeProblem.AssetUnreadable => t.ThemeErrorAssetUnreadable(key: error.Subject, file: error.Detail),
            ThemeProblem.NotAFont => t.ThemeErrorNotAFont(key: error.Subject, path: error.Detail),
            ThemeProblem.InvalidSvg => t.ThemeErrorInvalidSvg(key: error.Subject, reason: error.Detail),
            ThemeProblem.MissingGlyph => t.ThemeErrorMissingGlyph(key: error.Subject, glyph: error.Detail),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Problem, null),
        };
    }
}
