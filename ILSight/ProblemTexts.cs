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
    /// joins them (<c>error.summary</c>): a value in that line, a message's among them, keeps it
    /// (<see cref="OneLineText.Message"/>), as each value in the title did (<see cref="Words"/>).</summary>
    public static string Summarize(Strings t, string title, string details)
    {
        var end = details.AsSpan().IndexOfAny('\r', '\n');
        var first = OneLineText.Message((end < 0 ? details : details[..end]).Trim());
        var summary = first.Length == 0 ? title : t.ErrorSummary(title: title, details: first);
        return summary.Length > SummaryLength ? summary[..SummaryLength] + "…" : summary;
    }

    public static ProblemText Describe(Strings t, AppProblem problem)
    {
        var (title, details, diagnostic) = Words(t, problem);
        return ProblemText.FromWords(t, title, details.Trim(), diagnostic?.ToString() ?? "");
    }

    // The title and the details in the language's words, and the exception behind them, if any: its message is the
    // cause said in the details, and the whole of it (type, inner exceptions, stack trace) the technical details,
    // never the details themselves. Each value in the title, a line in the error list, is on its one line
    // (OneLineText.Name).
    private static (string Title, string Details, Exception? Diagnostic) Words(Strings t, AppProblem problem)
    {
        return problem switch
        {
            AppProblem.OpenFailed p => AskAgainOrReport(t, t.ErrorOpenAssemblies, p.Error),
            AppProblem.PdbUnusable p => (t.ErrorSelectPdb(file: OneLineText.Name(Path.GetFileName(p.PdbPath)), name: OneLineText.Name(p.Assembly)),
                DescribePdbCheck(t, p.Check, p.Assembly) + Environment.NewLine + p.PdbPath, null),
            AppProblem.AssemblyNotReloaded p => AskAgain(t, t.ErrorReloadAssembly(OneLineText.Name(p.Assembly)), p.Error),
            AppProblem.AssembliesNotReloaded p => AskAgainOrReport(t, t.ErrorReloadAssemblies, p.Error),
            AppProblem.AnalysisFailed p => Reportable(t, t.ErrorAnalyze(name: OneLineText.Name(p.Subject), analysis: AnalyzerText.Header(t, p.Kind)), p.Error),
            AppProblem.AnalysesNotRestored p => Reportable(t, t.ErrorRestoreAnalyses, p.Error),
            AppProblem.Unresolved p => AskAgainOrReport(t, t.ErrorResolve(OneLineText.Name(p.Reference.Name)), p.Error),
            AppProblem.UnresolvedFileDenied p => (t.ErrorResolve(OneLineText.Name(p.Reference.Name)), Denied(t, p.File), p.Error),
            AppProblem.UnresolvedFileDamaged p => (t.ErrorResolve(OneLineText.Name(p.Reference.Name)), Damaged(t, p.File), p.Error),
            AppProblem.PlaceLookupFailed p => (t.ErrorLookUpPlace(OneLineText.Name(p.Place.Label)), AfterStep(t, p.Step, BuildCauseAskingForReport(t, FailureTexts.DescribeCause(t, p.Error))), p.Error),
            AppProblem.PlaceLookupFileDenied p => (t.ErrorLookUpPlace(OneLineText.Name(p.Place.Label)),
                AfterStep(t, p.Step, Denied(t, p.File)), p.Error),
            AppProblem.PlaceLookupFileDamaged p => (t.ErrorLookUpPlace(OneLineText.Name(p.Place.Label)),
                AfterStep(t, p.Step, Damaged(t, p.File)), p.Error),
            AppProblem.ReferenceFileDenied p => (t.ErrorReadReference(name: OneLineText.Name(p.Unread.Name), assembly: OneLineText.Name(p.Unread.Module)),
                Degraded(t, p.Unread, t.ErrorFileDenied(p.Unread.Error.FilePath.FileName)), p.Unread.Error),
            AppProblem.ReferenceFileDamaged p => (t.ErrorReadReference(name: OneLineText.Name(p.Unread.Name), assembly: OneLineText.Name(p.Unread.Module)),
                Degraded(t, p.Unread, t.ErrorFileDamaged(p.Unread.Error.FilePath.FileName)), p.Unread.Error),
            AppProblem.ModuleUnreadable p => Thrown(t, t.CauseModuleUnreadableTitle(OneLineText.Name(p.Module)), p.Why),
            AppProblem.BookmarksNotExported p => (t.ErrorExportBookmarks, p.Reason, null),
            AppProblem.BookmarksNotImported p => (t.ErrorImportBookmarks(OneLineText.Name(p.File)), p.Reason, null),
            AppProblem.ImportedBookmarksUnreadable p => (t.ErrorBookmarksUnreadable(OneLineText.Name(p.File)), DescribeEntryProblems(t, p.Problems), null),
            AppProblem.ImportedBookmarksNotPlaced p => (t.ErrorBookmarkPlacesFailed(OneLineText.Name(p.File)), DescribePlaceFailures(t, p.Failed), null),
            AppProblem.ComparisonFailed p => Reportable(t, t.ErrorCompare(left: OneLineText.Name(p.Left), right: OneLineText.Name(p.Right)), p.Error),
            AppProblem.DecompilationFailed p => Reportable(t, t.ErrorDecompile(OneLineText.Name(p.Node)), p.Error),
            AppProblem.TypeNotDiagrammed p => Reportable(t, t.ErrorShowInDiagram(OneLineText.Name(p.Type.Name)), p.Error),
            AppProblem.DiagramNotReopened p => Reportable(t, t.ErrorShowInDiagram(p.Diagram.Words(t)), p.Error),
            AppProblem.DiagramNotLaidOut p => Reportable(t, t.ErrorLayOutDiagram, p.Error),
            AppProblem.ResourceNotSaved p => (t.ErrorSaveResource(OneLineText.Name(p.Resource)), FailureTexts.DescribeCause(t, p.Error), null),
            AppProblem.PackageFileNotExtracted p => (t.ErrorExtractFile(OneLineText.Name(p.File)), FailureTexts.DescribeCause(t, p.Error), null),
            AppProblem.SettingsUnreadable p => (SettingsTitle(t, p.Problem), $"{p.Path}{Environment.NewLine}{DescribeSettings(t, p.Problem)}", null),
            AppProblem.SettingsNotSaved p => (t.ErrorSaveSettings, p.Reason, null),
            AppProblem.ThemeFileProblems p => (ThemeFileTitle(t, p.Kind, OneLineText.Name(Path.GetFileName(p.File))), DescribeThemeErrors(t, p.Errors), null),
            AppProblem.ThemesFolderNotMade p => (p.Kind == ThemeKind.Color ? t.ErrorThemesFolder : t.ErrorIconThemesFolder, p.Reason, null),
            AppProblem.CustomThemeNotWritten p => (p.Kind == ThemeKind.Color ? t.ErrorCustomTheme : t.ErrorCustomIconTheme,
                $"{p.Path}{Environment.NewLine}{p.Reason}", null),
            AppProblem.ThemesNotWatched p => (p.Kind == ThemeKind.Color ? t.ErrorWatchThemes : t.ErrorWatchIconThemes, p.Reason, null),
            AppProblem.RuntimeNotScanned p => AskAgain(t, t.ErrorScanRuntime, p.Error),
            AppProblem.NuGetConfigUnreadable p => FixNuGetConfigOrReport(t, p.Error),
            AppProblem.NuGetSearchFailed p => AskAgain(t, t.ErrorSearchNuget(OneLineText.Name(p.Feed)), p.Error),
            AppProblem.PackageNotDownloaded p => AskAgain(t, t.ErrorDownloadPackage(OneLineText.Name(p.Package)), p.Error),
            AppProblem.Unexpected p => Reportable(t, t.ErrorUnexpected, p.Error),
        };
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

    // What the code shows without the reference and the step that reads it again, then the cause.
    // The code is kept, so the cause's own "try again" would reuse it: the step named is the reload.
    private static string Degraded(Strings t, UnreadReference unread, string cause)
    {
        return t.ErrorReadReferenceDegraded(assembly: unread.Module, name: unread.Name) + Environment.NewLine + Environment.NewLine + cause;
    }

    // A failure whose cause the app knows only by the exception, of an operation with no recovery that holds whatever
    // the cause (what it reads isn't read again when asked, and it isn't the app's to report): the cause said by its
    // message as written, the exception itself the technical details, and no step invented.
    private static (string Title, string Details, Exception? Diagnostic) Thrown(Strings t, string title, Exception error)
    {
        return (title, FailureTexts.DescribeCause(t, error), error);
    }

    // A failure whose cause the app knows only by the exception, of an operation that reads what can change between
    // asks (a file, a folder, a feed, a download) and fails only by what the read expects of it (a file that can't be
    // read, a feed that can't be reached; the app's own bugs go on to the edge, reported as unexpected): asking again
    // holds whatever the cause.
    private static (string Title, string Details, Exception? Diagnostic) AskAgain(Strings t, string title, Exception error)
    {
        return (title, t.ErrorCauseAndRecovery(cause: FailureTexts.DescribeCause(t, error), recovery: t.ErrorRecoveryTryAgain), error);
    }

    // The user's NuGet.Config failing to read unexpectedly: the setup is read once a session and kept, so asking again
    // would reuse it; what reads it again is a restart (ReadNuGetSetupHandler), after fixing the file. The catch takes
    // any failure, the app's own among them, which ILSight takes reports of: the report beside it.
    private static (string Title, string Details, Exception? Diagnostic) FixNuGetConfigOrReport(Strings t, Exception error)
    {
        return (t.CauseNugetConfigUnreadableTitle,
            t.ErrorCauseAndRecovery(cause: FailureTexts.DescribeCause(t, error), recovery: t.ErrorRecoveryFixNugetConfigOrReport), error);
    }

    // A failure whose cause the app knows only by the exception, of an operation whose contract holds both: asking
    // again is safe and reads what can change (a file, the file system), and the catch takes any failure, the app's
    // own among them, which ILSight takes reports of: the retry first, then the report if it fails again.
    private static (string Title, string Details, Exception? Diagnostic) AskAgainOrReport(Strings t, string title, Exception error)
    {
        return (title, t.ErrorCauseAndRecovery(cause: FailureTexts.DescribeCause(t, error), recovery: t.ErrorRecoveryTryAgainOrReport), error);
    }

    // A failure in the app's own code or the decompiler's, or of an operation that fails on input that hasn't changed
    // (what is open, as it was read), a bug to report: the cause by its message, and the recovery asks for a report,
    // which the copied error's technical details make.
    private static (string Title, string Details, Exception? Diagnostic) Reportable(Strings t, string title, Exception error)
    {
        return (title, BuildCauseAskingForReport(t, FailureTexts.DescribeCause(t, error)), error);
    }

    // The cause, then the recovery that asks for a report: defined once for every failure that is the app's to report.
    private static string BuildCauseAskingForReport(Strings t, string cause)
    {
        return t.ErrorCauseAndRecovery(cause: cause, recovery: t.ErrorRecoveryReportBug);
    }

    // One problem a line: the lines are the layout, each line's words the language's.
    private static string DescribeEntryProblems(Strings t, IReadOnlyList<BookmarkEntryProblem> problems)
    {
        return string.Join(Environment.NewLine, problems.Select(p => DescribeEntryProblem(t, p)));
    }

    // Each bookmark and why its lookup failed, by its cause's message; the exception stays on the problem logged.
    private static string DescribePlaceFailures(Strings t, IReadOnlyList<(LostBookmark Bookmark, Exception Error)> failed)
    {
        return string.Join(Environment.NewLine, failed.Select(f => t.BookmarkPlaceFailed(bookmark: f.Bookmark.Words(t), error: FailureTexts.DescribeCause(t, f.Error))));
    }

    private static string DescribeEntryProblem(Strings t, BookmarkEntryProblem problem)
    {
        return problem switch
        {
            BookmarkEntryProblem.PlaceUnreadable p => t.ErrorBookmarkPlaceUnreadable(p.Entry),
            BookmarkEntryProblem.NotABookmark n => t.BookmarkEntryProblem(entry: n.Entry, detail: n.Detail),
        };
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
