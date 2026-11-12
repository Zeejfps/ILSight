using System.Diagnostics;
using System.Globalization;
using System.Text;
using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>Exports as text: what kind each is and how it ended, in a few words for the exports
/// panel, and whole reports, which a tab shows.</summary>
internal static class ExportReportText
{
    private const string Indent = "    ";

    public static string KindOf(Strings t, ExportKind kind)
    {
        return kind switch
        {
            ExportKind.CSharpFile => t.ExportKindCsharpFile,
            ExportKind.ILFile => t.ExportKindIlFile,
            ExportKind.Project => t.ExportKindProject,
            ExportKind.Solution => t.ExportKindSolution,
            ExportKind.Pdb => t.ExportKindPdb,
            ExportKind.Pdbs => t.ExportKindPdbs,
            ExportKind.Diagram => t.ExportKindDiagram,
            ExportKind.Files => t.ExportKindFiles,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>How <paramref name="report"/>'s export ended, in a few words: whether it finished,
    /// with how many problems, and how long it took.</summary>
    public static string DescribeOutcome(Strings t, ExportReport report)
    {
        return report.End.Match((t, report.Elapsed),
            static (s, finished) => DescribeFinished(s.t, finished, s.Elapsed),
            static (s, _) => s.t.ExportsCancelled,
            static (s, _) => s.t.ExportsFailed);
    }

    private static string DescribeFinished(Strings t, ExportEnd.Finished finished, TimeSpan elapsed)
    {
        var duration = FormatDuration(t, elapsed);
        if (!finished.HasProblems)
        {
            return t.ExportsFinishedIn(duration);
        }

        var problems = t.ExportProblemCount(finished.Problems.Count);
        return $"{t.ExportsFinishedWith(problems)} · {duration}";
    }

    /// <summary><paramref name="elapsed"/> as people say it: seconds with a tenth under a minute,
    /// then minutes and seconds, then hours and minutes.</summary>
    public static string FormatDuration(Strings t, TimeSpan elapsed)
    {
        if (elapsed.TotalMinutes < 1)
        {
            return t.DurationSeconds(elapsed.TotalSeconds.ToString("F1", CultureInfo.CurrentCulture));
        }

        if (elapsed.TotalHours < 1)
        {
            return t.DurationMinutes((int)elapsed.TotalMinutes, elapsed.Seconds);
        }

        return t.DurationHours((int)elapsed.TotalHours, elapsed.Minutes);
    }

    private static string Summarize(Strings t, ExportReport report)
    {
        return report.End.Match((t, report.Name),
            static (s, finished) => SummarizeFinished(s.t, s.Name, finished),
            static (s, _) => s.t.ExportSummaryCancelled(s.Name),
            static (s, _) => s.t.ExportSummaryFailed(s.Name));
    }

    private static string SummarizeFinished(Strings t, string name, ExportEnd.Finished finished)
    {
        var summary = t.ExportSummaryFinished(name);
        if (!finished.HasProblems)
        {
            return summary;
        }

        return $"{summary} · {t.ExportProblemCount(finished.Problems.Count)}";
    }

    /// <summary>Each of <paramref name="reports"/> in full: its summary, kind, folder and time, then
    /// the files it wrote and its problems with their errors.</summary>
    public static DecompiledDocument Write(Strings t, IReadOnlyList<ExportReport> reports)
    {
        var text = new StringBuilder();
        foreach (var report in reports)
        {
            WriteReport(text, t, report);
        }

        return Documents.CreateFromText(t.ExportReportTitle, text.ToString());
    }

    private static void WriteReport(StringBuilder text, Strings t, ExportReport report)
    {
        text.AppendLine(Summarize(t, report));
        text.AppendLine(KindOf(t, report.Job.Kind));
        text.AppendLine(report.Folder);
        text.AppendLine(t.ExportTook(FormatDuration(t, report.Elapsed)));
        text.AppendLine();
        report.End.Switch((text, t, kind: report.Job.Kind),
            static (s, finished) => WriteFinished(s.text, s.t, s.kind, finished),
            static (_, _) =>
            {
            },
            static (s, failed) => WriteFailure(s.text, s.t, s.kind, failed));
    }

    private static void WriteFinished(StringBuilder text, Strings t, ExportKind kind, ExportEnd.Finished finished)
    {
        WriteWritten(text, t, finished.Written);
        WriteProblems(text, t, kind, finished.Problems);
    }

    /// <summary>Why the export failed, under the problems' heading, as a problem of a finished one would be.</summary>
    private static void WriteFailure(StringBuilder text, Strings t, ExportKind kind, ExportEnd.Failed failed)
    {
        text.AppendLine(t.ExportProblemsHeading);
        text.Append(Indent).AppendLine(Headline(t, kind, failed));
        WriteDetails(text, DetailsOf(failed));
        text.AppendLine();
    }

    private static void WriteWritten(StringBuilder text, Strings t, IReadOnlyList<string> written)
    {
        if (written.Count == 0)
        {
            return;
        }

        text.AppendLine(t.ExportWritten);
        foreach (var path in written)
        {
            text.Append(Indent).AppendLine(path);
        }

        text.AppendLine();
    }

    private static void WriteProblems(StringBuilder text, Strings t, ExportKind kind, IReadOnlyList<ExportProblem> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        text.AppendLine(t.ExportProblemsHeading);
        foreach (var problem in problems)
        {
            WriteProblem(text, t, kind, problem);
        }
    }

    private static void WriteProblem(StringBuilder text, Strings t, ExportKind kind, ExportProblem problem)
    {
        text.Append(Indent).AppendLine(Headline(t, kind, problem));
        var details = DetailsOf(problem);
        if (details is not null)
        {
            WriteDetails(text, details);
        }

        text.AppendLine();
    }

    /// <summary><paramref name="problem"/> of an export of <paramref name="kind"/> in a line.</summary>
    public static string Headline(Strings t, ExportKind kind, ExportProblem problem)
    {
        return problem.Match(
            notDecompiled: static part => part.Error.Message,
            noPdbEntry: pdb => t.ErrorNoPdbEntry(pdb.Assembly),
            pdbNotWritten: static pdb => $"{pdb.Assembly}: {pdb.Error.Message}",
            outsideFolder: outside => t.ExportOutsideFolder(outside.Path),
            fileNotWritten: static file => $"{file.Path}: {file.Error.Message}");
    }

    /// <summary>The full error under <paramref name="problem"/>'s headline, when there is one.</summary>
    public static string? DetailsOf(ExportProblem problem)
    {
        return problem.Match<string?>(
            notDecompiled: static part => part.Error.Details,
            noPdbEntry: static _ => null,
            pdbNotWritten: static pdb => pdb.Error.Details,
            outsideFolder: static _ => null,
            fileNotWritten: static file => file.Error.Details);
    }

    /// <summary>The failure <paramref name="failed"/> of an export of <paramref name="kind"/> in a line.</summary>
    public static string Headline(Strings t, ExportKind kind, ExportEnd.Failed failed)
    {
        return FailureOf(t, kind, failed.Subject);
    }

    /// <summary>The full error under <paramref name="failed"/>'s headline.</summary>
    public static string DetailsOf(ExportEnd.Failed failed)
    {
        return failed.Error.ToString();
    }

    private static string FailureOf(Strings t, ExportKind kind, string subject)
    {
        return kind switch
        {
            ExportKind.CSharpFile or ExportKind.ILFile => t.ErrorSaveCode(subject),
            ExportKind.Project => t.ErrorExportProject(subject),
            ExportKind.Solution => t.ErrorExportSolution,
            ExportKind.Pdb => t.ErrorGeneratePdb(subject),
            ExportKind.Pdbs => t.ErrorGeneratePdbs,
            ExportKind.Files => t.ErrorExtractFiles(subject),
            ExportKind.Diagram => t.ErrorCreateDiagram(subject),
            _ => throw new UnreachableException(),
        };
    }

    private static void WriteDetails(StringBuilder text, string details)
    {
        var lines = details.Split('\n');
        foreach (var line in lines)
        {
            text.Append(Indent).Append(Indent).AppendLine(line.TrimEnd('\r'));
        }
    }
}
