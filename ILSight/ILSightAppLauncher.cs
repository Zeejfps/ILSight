namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="commandLine"/> to the window already open with the same settings
    /// and leaves, unless it asks for a new window or none is open; else opens the app.</summary>
    public static void LaunchApp(CommandLine commandLine)
    {
        var address = CommandLineAddress.ForSettingsFile(commandLine.SettingsPath);
        using var pipe = new CommandLinePipe(address);
        if (SingleInstanceLock.TryAcquire(address, out var instanceLock))
        {
            using (instanceLock)
            {
                var app = new ILSightApp(commandLine);
                pipe.ForwardTo(app);
                app.Run();
            }
        }
        else if (commandLine.NewInstance)
        {
            var app = new ILSightApp(commandLine);
            app.Run();
        }
        else
        {
            var handedOver = pipe.TryHandOverToRunningApp(commandLine);
            if (!handedOver)
            {
                var app = new ILSightApp(commandLine);
                app.Run();
            }
        }
    }
}
