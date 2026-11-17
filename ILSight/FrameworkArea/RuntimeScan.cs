using ILSight.Core;

namespace ILSight.FrameworkArea;

/// <summary>How far scanning the runtime for its assemblies has got.</summary>
public union RuntimeScan(RuntimeScan.NotStarted, RuntimeScan.Scanning, RuntimeScan.Scanned, RuntimeScan.Failed)
{
    public sealed record NotStarted;

    public sealed record Scanning;

    public sealed record Scanned(IReadOnlyList<RuntimeAssembly> Assemblies);

    public sealed record Failed(string Error);
}
