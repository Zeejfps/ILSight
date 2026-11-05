using ILSight;
using Velopack;

// First, because an install or update runs the app just to handle its hook, and this then exits.
VelopackApp.Build().Run();

var commandLine = CommandLine.Parse(args);
ILSightAppLauncher.LaunchApp(commandLine);
