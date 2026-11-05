namespace ILSight;

public static class ILSightAppLauncher
{
    /// <summary>Hands <paramref name="command"/> to the window already open with the same settings
    /// and leaves, unless it asks for a new window or none is open; else opens the app.</summary>
    public static void LaunchApp(ExternalCommand command)
    {
        var address = ExternalCommandAddress.ForSettingsFile(command.SettingsPath);
        using var pipe = new ExternalCommandPipe(address);
        if (SingleInstanceLock.TryAcquire(address, out var instanceLock))
        {
            using (instanceLock)
            {
                var app = new ILSightApp(command);
                pipe.ForwardTo(app);
                app.Run();
            }
        }
        else if (command.NewInstance)
        {
            var app = new ILSightApp(command);
            app.Run();
        }
        else
        {
            var handedOver = pipe.TryHandOverToRunningApp(command);
            if (!handedOver)
            {
                var app = new ILSightApp(command);
                app.Run();
            }
        }
    }
}
