namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="command"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="options"/> ask for a new window or none is open; else opens
    /// the app.</summary>
    public static void LaunchApp(LaunchOptions options, ExternalCommand command)
    {
        var address = ExternalCommandAddress.ForSettingsFile(options.SettingsPath);
        using var pipe = new ExternalCommandPipe(address);
        if (SingleInstanceLock.TryAcquire(address, out var instanceLock))
        {
            using (instanceLock)
            {
                var app = new ILSightApp();
                pipe.ForwardTo(app);
                app.Run(options, command);
            }
        }
        else if (options.NewInstance)
        {
            var app = new ILSightApp();
            app.Run(options, command);
        }
        else
        {
            var handedOver = pipe.TryHandOverToRunningApp(command);
            if (!handedOver)
            {
                var app = new ILSightApp();
                app.Run(options, command);
            }
        }
    }
}
