using ILSight.Core;

namespace ILSight.Tests;

/// <summary>Every report, in the order they came, from whichever thread sent them.</summary>
internal sealed class RecordedProgress : IProgress<ExportProgress>
{
    private readonly List<ExportProgress> _reports = new();

    public List<ExportProgress> Reports
    {
        get
        {
            lock (_reports)
            {
                return _reports.ToList();
            }
        }
    }

    public void Report(ExportProgress value)
    {
        lock (_reports)
        {
            _reports.Add(value);
        }
    }
}
