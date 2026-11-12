using ILSight.Core;
using ILSight.Exporting;

namespace ILSight;

/// <summary>The export report a tab's code view shows, in the interface's language, written again only
/// when the report or the language changes.</summary>
public sealed class ShownReport
{
    /// <summary>The report written out, by the report and language. Reports are records, and two equal ones
    /// are still written again.</summary>
    public Memo<(ByReference<ExportReport>, ByReference<Strings>), DecompiledDocument> Written;
}
