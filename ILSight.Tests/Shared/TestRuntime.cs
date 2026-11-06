using ILSight.Core;

namespace ILSight.Tests;

/// <summary>What this machine has installed, read once for all the tests.</summary>
internal static class TestRuntime
{
    public static readonly InstalledRuntime Installed = new();
}
