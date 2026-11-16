using System.Diagnostics;
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
    /// with how many problems, and how long it took; or that it was refused, and why.</summary>
    public static string DescribeOutcome(Strings t, ExportReport report)
    {
        return report.End.Match((t, report.Elapsed, Name: OneLineText.Name(report.Job.Name)),
            static (s, finished) => DescribeFinished(s.t, finished, s.Elapsed),
            static (s, _) => s.t.ExportsCancelled,
            static (s, closed) => s.t.ExportSummaryAssemblyClosed(name: s.Name, assembly: OneLineText.Name(closed.Assembly)),
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
            return t.DurationSeconds(elapsed.TotalSeconds.ToString("F1", t.Culture));
        }

        if (elapsed.TotalHours < 1)
        {
            return t.DurationMinutes(minutes: (int)elapsed.TotalMinutes, seconds: elapsed.Seconds);
        }

        return t.DurationHours(hours: (int)elapsed.TotalHours, minutes: elapsed.Minutes);
    }

    private static string Summarize(Strings t, ExportReport report)
    {
        return report.End.Match((t, Name: OneLineText.Name(report.Job.Name)),
            static (s, finished) => SummarizeFinished(s.t, s.Name, finished),
            static (s, _) => s.t.ExportSummaryCancelled(s.Name),
            static (s, closed) => s.t.ExportSummaryAssemblyClosed(name: s.Name, assembly: OneLineText.Name(closed.Assembly)),
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
        text.AppendLine(OneLineText.Name(report.Job.Folder));
        text.AppendLine(t.ExportTook(FormatDuration(t, report.Elapsed)));
        text.AppendLine();
        report.End.Switch((text, t, kind: report.Job.Kind),
            static (s, finished) => WriteFinished(s.text, s.t, s.kind, finished),
            static (_, _) =>
            {
            },
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

    private static void WriteFailure(StringBuilder text, Strings t, ExportKind kind, ExportEnd.Failed failed)
    {
        text.AppendLine(t.ExportProblemsHeading);
        text.Append(Indent).AppendLine(Headline(t, kind, failed));
        text.Append(Indent).Append(Indent).AppendLine(CauseOf(failed));
        WriteTechnicalDetails(text, t, TechnicalDetailsOf(failed));
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
            text.Append(Indent).AppendLine(OneLineText.Name(path));
        }

        text.AppendLine();
    }

    // Every problem's headline, one a line, then the technical details of those with an exception in a labelled section
    // after them, each under its problem's headline again: no stack trace stands between one problem and the next.
    private static void WriteProblems(StringBuilder text, Strings t, ExportKind kind, IReadOnlyList<ExportProblem> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        text.AppendLine(t.ExportProblemsHeading);
        foreach (var problem in problems)
        {
            text.Append(Indent).AppendLine(Headline(t, kind, problem));
        }

        text.AppendLine();
        var labelled = false;
        foreach (var problem in problems)
        {
            var details = TechnicalDetailsOf(problem);
            if (details is null)
            {
                continue;
            }

            if (!labelled)
            {
                text.AppendLine(t.ErrorsTechnicalDetails);
                labelled = true;
            }

            text.Append(Indent).AppendLine(Headline(t, kind, problem));
            WriteTrace(text, details, Indent + Indent);
            text.AppendLine();
        }
    }

    /// <summary><paramref name="problem"/> of an export of <paramref name="kind"/> in a line.</summary>
    public static string Headline(Strings t, ExportKind kind, ExportProblem problem)
    {
        return problem.Match(
            notDecompiled: static part => OneLineText.Message(part.Error.Message),
            noPdbEntry: pdb => t.ErrorNoPdbEntry(OneLineText.Name(pdb.Assembly)),
            pdbNotWritten: static pdb => $"{OneLineText.Name(pdb.Assembly)}: {OneLineText.Message(pdb.Error.Message)}",
            outsideFolder: outside => t.ExportOutsideFolder(OneLineText.Name(outside.Path)),
            fileNotWritten: static file => $"{OneLineText.Name(file.Path)}: {OneLineText.Message(file.Error.Message)}");
    }

    /// <summary>The exception behind <paramref name="problem"/> as the runtime wrote it (its type and stack trace), when
    /// there is one, under its headline in the technical details after every problem; the headline says it by its
    /// message.</summary>
    public static string? TechnicalDetailsOf(ExportProblem problem)
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

    /// <summary>What made <paramref name="failed"/> fail, by its exception's message, under its headline.</summary>
    public static string CauseOf(ExportEnd.Failed failed)
    {
        return OneLineText.Message(failed.Error.Message);
    }

    /// <summary>The exception behind <paramref name="failed"/> as the runtime wrote it (its type and stack trace),
    /// under the technical details label after its cause.</summary>
    public static string TechnicalDetailsOf(ExportEnd.Failed failed)
    {
        return failed.Error.ToString();
    }

    private static string FailureOf(Strings t, ExportKind kind, string named)
    {
        var subject = OneLineText.Name(named);
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

    // The diagnostic apart from what the user reads, under its label, as the runtime wrote it.
    private static void WriteTechnicalDetails(StringBuilder text, Strings t, string details)
    {
        text.Append(Indent).Append(Indent).AppendLine(t.ErrorsTechnicalDetails);
        WriteTrace(text, details, Indent + Indent + Indent);
    }

    private static void WriteTrace(StringBuilder text, string details, string indent)
    {
        foreach (var line in details.Split('\n'))
        {
            text.Append(indent).AppendLine(line.TrimEnd('\r'));
        }
    }
}
