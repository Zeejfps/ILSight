using System.Security.Cryptography;
using System.Text;

namespace ILSight;

/// <summary>Where a run's arguments go.</summary>
public static class RunArgumentsRouting
{
    private const string NamePrefix = "ILSight-";
    private const int HashBytes = 8;

    /// <summary>Where the runs with the settings at <paramref name="settingsPath"/> meet: the same for
    /// every spelling of the path the file system takes as one.</summary>
    public static RunArgumentsAddress FindAddress(string settingsPath)
    {
        var path = Path.GetFullPath(settingsPath);
        if (OperatingSystem.IsWindows())
            path = path.ToUpperInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(path));
        return new RunArgumentsAddress(NamePrefix + Convert.ToHexString(hash, 0, HashBytes));
    }

    /// <summary>Null when the app already running at <paramref name="address"/> took
    /// <paramref name="arguments"/>, and this run has nothing left to do. Otherwise this run opens a
    /// window, with the receiver it gets: one listening already if none was, or one standing by to take
    /// over, whether <paramref name="newInstance"/> asked for that or the running app could not be reached.</summary>
    public static RunArgumentsReceiver? ForwardOrReceive(RunArgumentsAddress address, RunArguments arguments, bool newInstance)
    {
        var receiver = RunArgumentsReceiver.TryClaim(address);
        if (receiver != null)
            return receiver;
        if (newInstance)
            return RunArgumentsReceiver.StandBy(address);
        var handedOver = RunArgumentsPipe.TrySend(address, arguments);
        if (handedOver)
            return null;
        return RunArgumentsReceiver.StandBy(address);
    }
}
