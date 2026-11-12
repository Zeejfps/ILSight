using Velopack;
using ZGF.AppUtils;
using ILSight.Commanding;

namespace ILSight;

internal static class Program
{
    // ZGF needs the main thread in a single-threaded apartment on Windows, where the native file
    // dialogs are COM objects shown on the UI thread. The attribute does nothing on other platforms.
    [STAThread]
    public static void Main(string[] args)
    {
        // First, because an install or update runs the app just to handle its hook, and this then exits.
        VelopackApp.Build().Run();
        EnsureLinuxMenuEntry();

        var commandLine = CommandLines.Parse(args);
        ILSightAppLauncher.LaunchApp(commandLine.Launch, commandLine.Arguments);
    }

    // Velopack's AppImage has no install step, so nothing else puts ILSight in the Linux application menu.
    // On each launch from an AppImage the framework writes the entry, or repoints it if the AppImage has
    // moved. install.sh writes the same entry and must stay byte for byte identical to it, or every launch
    // rewrites it.
    private static void EnsureLinuxMenuEntry()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        // The AppImage runtime exports where it mounted the image; a dev build has none, and must not
        // register itself. The icon is the one the image carries, which install.sh uses too.
        var appDir = Environment.GetEnvironmentVariable("APPDIR");
        if (string.IsNullOrEmpty(appDir) || !Path.IsPathRooted(appDir))
        {
            return;
        }

        var entry = new LinuxDesktopEntry(
            fileStem: "ILSight",
            name: "ILSight",
            comment: "A .NET assembly explorer",
            startupWmClass: "ILSight",
            categories: "Development;",
            iconSource: Path.Combine(appDir, ".DirIcon"));
        entry.EnsureRegistered();
    }
}
