using System.Security.Cryptography;
using System.Text;
using ILSight.Core;

namespace ILSight.ILSightAppArea;

/// <summary>Where runs meet to hand their arguments over (<see cref="RunRouting"/> decides where they go).</summary>
public static class RunArgumentsRouting
{
    private const string NamePrefix = "ILSight-";
    private const int HashBytes = 8;

    /// <summary>Where the runs with the settings at <paramref name="settingsPath"/> meet: the same for
    /// every spelling of the path the file system takes as one.</summary>
    public static RunArgumentsAddress GetAddress(FullFilePath settingsPath)
    {
        var path = settingsPath.Text;
        if (OperatingSystem.IsWindows())
        {
            path = path.ToUpperInvariant();
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return new RunArgumentsAddress(NamePrefix + Convert.ToHexString(hash, 0, HashBytes));
    }
}
