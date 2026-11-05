using ILSight;
using Velopack;

// First, because an install or update runs the app just to handle its hook, and this then exits.
VelopackApp.Build().Run();

var arguments = LaunchArguments.Parse(args);
ILSightAppLauncher.LaunchApp(arguments.Options, arguments.Command);
