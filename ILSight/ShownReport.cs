using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The export report a tab's code view shows, in the interface's language, written again only
/// when the report or the language changes.</summary>
public sealed class ShownReport
{
    public ExportReport? Report { get; set; }

    public Strings? Strings { get; set; }

    public DecompiledDocument Document { get; set; } = DecompiledDocument.Empty;
}

public static class ShownReports
{
    /// <summary><paramref name="report"/> written out with <paramref name="t"/>.</summary>
    public static DecompiledDocument GetDocument(ShownReport shown, ExportReport report, Strings t)
    {
        if (ReferenceEquals(report, shown.Report) && t == shown.Strings)
            return shown.Document;
        shown.Report = report;
        shown.Strings = t;
        shown.Document = ExportReportText.Write(t, [report]);
        return shown.Document;
    }
}
