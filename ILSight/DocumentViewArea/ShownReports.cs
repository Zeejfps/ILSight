using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight.DocumentViewArea;

/// <summary>The export reports documents show, written out once per report and language.</summary>
internal static class ShownReports
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
