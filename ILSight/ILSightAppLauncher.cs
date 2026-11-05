namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="command"/> to the window already open with the same settings
    /// and leaves, unless <paramref name="config"/> asks for a new window or none is open; else opens
    /// the app.</summary>
    public static void LaunchApp(LaunchConfig config, ExternalCommand command)
    {
        var address = ExternalCommandAddress.ForSettingsFile(config.SettingsPath);
        using var pipe = new ExternalCommandPipe(address);
        if (SingleInstanceLock.TryAcquire(address, out var instanceLock))
        {
            using (instanceLock)
            {
                var app = new ILSightApp();
                pipe.ForwardTo(app);
                app.Run(config, command);
            }
        }
        else if (config.NewInstance)
        {
            var app = new ILSightApp();
            app.Run(config, command);
        }
        else
        {
            var handedOver = pipe.TryHandOverToRunningApp(command);
            if (!handedOver)
            {
                var app = new ILSightApp();
                app.Run(config, command);
            }
        }
    }
}
