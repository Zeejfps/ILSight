using Velopack;

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

        var commandLine = CommandLines.Parse(args);
        ILSightAppLauncher.LaunchApp(commandLine.Launch, commandLine.Arguments);
    }
}
