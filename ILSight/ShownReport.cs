using ILSight.Core;
using ILSight.ExportArea;

namespace ILSight;

/// <summary>The export report a tab's code view shows, in the interface's language, written again only
/// when the report or the language changes.</summary>
public sealed class ShownReport
{
    // By reference: reports are records, and two equal ones are still written again.
    private Memo<(ByReference<ExportReport>, ByReference<Strings>), DecompiledDocument> _written;

    /// <summary><paramref name="report"/> written out with <paramref name="t"/>.</summary>
    public DecompiledDocument GetDocument(ExportReport report, Strings t)
    {
        var key = (new ByReference<ExportReport>(report), new ByReference<Strings>(t));
        if (_written.TryGet(key, out var document))
        {
            return document;
        }

        document = ExportReportText.Write(t, [report]);
        _written = new Memo<(ByReference<ExportReport>, ByReference<Strings>), DecompiledDocument>(key, document);
        return document;
    }
}
