using System.Security.Cryptography;
using System.Text;

namespace ILSight;

/// <summary>Where the runs of the app with the same settings meet.</summary>
public sealed class CommandLineAddress
{
    private const string NamePrefix = "ILSight-";
    private const int HashBytes = 8;

    private CommandLineAddress(string name)
    {
        Name = name;
    }

    /// <summary>The name the runs meet under.</summary>
    public string Name { get; }

    /// <summary>Where the runs with the settings at <paramref name="settingsPath"/> meet: the same for
    /// every spelling of the path the file system takes as one.</summary>
    public static CommandLineAddress ForSettingsFile(string settingsPath)
    {
        var path = Path.GetFullPath(settingsPath);
        if (OperatingSystem.IsWindows())
            path = path.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return new CommandLineAddress(NamePrefix + Convert.ToHexString(hash, 0, HashBytes));
    }
}
