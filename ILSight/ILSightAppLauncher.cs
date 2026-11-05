namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="commandLine"/> to the window already open with the same settings
    /// and leaves, unless it asks for a new window or none is open; else opens the app.</summary>
    public static void LaunchApp(CommandLine commandLine)
    {
        using var instanceLock = SingleInstanceLock.Request(commandLine.SettingsPath);
        if (instanceLock.IsGranted)
        {
            var app = new ILSightApp(commandLine);
            using var pipe = CommandLinePipe.Listen(instanceLock.Name, app.TryHandle);
            app.Run();
        }
        else if (commandLine.NewInstance)
        {
            var app = new ILSightApp(commandLine);
            app.Run();
        }
        else
        {
            var handedOver = CommandLinePipe.TryHandOver(instanceLock.Name, commandLine);
            if (!handedOver)
            {
                var app = new ILSightApp(commandLine);
                app.Run();
            }
        }
    }
}
