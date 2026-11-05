using ZGF.Gui.Immediate;

namespace ILSight;

/// <summary>How this run starts, which means nothing to an app already running: the settings it uses, how
/// it draws, and whether it opens a window of its own rather than hand its command to the one already open
/// with the same settings.</summary>
public sealed record LaunchOptions(string SettingsPath, RenderBackendKind RenderBackend, bool NewInstance);

/// <summary>The arguments this run was started with: those on how it starts, and the command the rest ask.</summary>
public sealed record LaunchArguments(LaunchOptions Options, ExternalCommand Command)
{
    private const string SettingsOption = "--settings=";
    private const string OpenGlFlag = "--gl";
    private const string NewInstanceFlag = "--newinstance";

    public static LaunchArguments Parse(string[] args)
    {
        var settingsPath = AppSettings.DefaultPath;
        var renderBackend = RenderBackendKind.Auto;
        var newInstance = false;
        var commandArgs = new List<string>();
        foreach (var arg in args)
        {
            var path = ReadSettingsPath(arg);
            if (path != null)
                settingsPath = path;
            else if (arg == OpenGlFlag)
                renderBackend = RenderBackendKind.OpenGl;
            else if (arg == NewInstanceFlag)
                newInstance = true;
            else
                commandArgs.Add(arg);
        }
        var options = new LaunchOptions(settingsPath, renderBackend, newInstance);
        return new LaunchArguments(options, ExternalCommand.Parse(commandArgs.ToArray()));
    }

    /// <summary>The path <paramref name="arg"/> names; null when it names none, and is left to the command
    /// to ignore.</summary>
    private static string? ReadSettingsPath(string arg)
    {
        if (!arg.StartsWith(SettingsOption, StringComparison.Ordinal))
            return null;
        var path = arg.Substring(SettingsOption.Length).Trim();
        if (path.Length == 0)
            return null;
        return path;
    }
}
