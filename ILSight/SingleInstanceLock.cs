using System.Security.Cryptography;
using System.Text;

namespace ILSight;

/// <summary>Tells the runs of the app with the same settings apart: the first is granted the lock, and
/// holds it until it exits. A run that is not granted it is free to go on regardless; the lock only
/// says which run is the first.</summary>
public sealed class SingleInstanceLock : IDisposable
{
    private const string NamePrefix = "ILSight-";
    private const int HashBytes = 8;

    private readonly Mutex _mutex;

    private SingleInstanceLock(string name)
    {
        _mutex = new Mutex(false, name, out var createdNew);
        IsGranted = createdNew;
        Name = name;
    }

    /// <summary>The name the runs with these settings meet under.</summary>
    public string Name { get; }

    /// <summary>Whether no other run with these settings is open.</summary>
    public bool IsGranted { get; }

    /// <summary>Requests the lock of the runs with the settings at <paramref name="settingsPath"/>.</summary>
    public static SingleInstanceLock Request(string settingsPath)
    {
        return new SingleInstanceLock(NameFor(settingsPath));
    }

    /// <summary>The name the runs with the settings at <paramref name="settingsPath"/> meet under: the
    /// same for every spelling of the path the file system takes as one.</summary>
    private static string NameFor(string settingsPath)
    {
        var path = Path.GetFullPath(settingsPath);
        if (OperatingSystem.IsWindows())
            path = path.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return NamePrefix + Convert.ToHexString(hash, 0, HashBytes);
    }

    public void Dispose()
    {
        _mutex.Dispose();
    }
}
