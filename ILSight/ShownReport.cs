using ILSight.Core;
using ILSight.Localization;

namespace ILSight;

/// <summary>The export report a tab's code view shows, in the interface's language, written again only
/// when the report or the language changes.</summary>
public sealed class ShownReport
{
    /// <summary>The report written out, by the report and language. Reports are records, and two equal ones
    /// are still written again.</summary>
    public Memo<(ByReference<ExportReport>, ByReference<Strings>), DecompiledDocument> Written;
}

public static class ShownReports
{
    /// <summary><paramref name="report"/> written out with <paramref name="t"/>.</summary>
    public static DecompiledDocument GetDocument(ShownReport shown, ExportReport report, Strings t)
    {
        if (shown.Written.TryGet((new ByReference<ExportReport>(report), new ByReference<Strings>(t)), out var document))
        {
            return document;
        }

        document = ExportReportText.Write(t, [report]);
        shown.Written = new Memo<(ByReference<ExportReport>, ByReference<Strings>), DecompiledDocument>((new ByReference<ExportReport>(report), new ByReference<Strings>(t)), document);
        return document;
    }
}
