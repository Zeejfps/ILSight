using ZGF.AppUtils;

namespace ILSight;

// Velopack's AppImage has no install step, so nothing else puts ILSight in the Linux application menu.
// On each launch from an AppImage the framework writes the entry, or repoints it if the AppImage has
// moved. install.sh writes the same entry and must stay byte for byte identical to it, or every launch
// rewrites it.
internal static class LinuxMenu
{
    public static void EnsureEntry()
    {
        if (!OperatingSystem.IsLinux()) return;

        // The AppImage runtime exports where it mounted the image; a dev build has none, and must not
        // register itself. The icon is the one the image carries, which install.sh uses too.
        var appDir = Environment.GetEnvironmentVariable("APPDIR");
        if (string.IsNullOrEmpty(appDir) || !Path.IsPathRooted(appDir)) return;

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
