using ILSight.ExportArea;

namespace ILSight.App.Tests;

/// <summary>Export jobs for tests that only need the queue to hold something by a name.</summary>
internal static class TestJobs
{
    public static ExportJob Named(string name)
    {
        return new ExportJob { Name = name, Kind = ExportKind.Project, Folder = Path.GetTempPath() };
    }
}
