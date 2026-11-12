using System.Reflection;
using System.Runtime.InteropServices;
using ICSharpCode.Decompiler.CSharp;

namespace ILSight.MainScreen;

/// <summary>The versions of the app, the decompiler it is built on, the runtime and the system it runs
/// on, as the About dialog shows them.</summary>
internal static class AppVersions
{
    private const string DecompilerName = "ICSharpCode.Decompiler";
    private const char BuildSeparator = '+';

    public static string App { get; } = GetVersion(typeof(AppVersions).Assembly);

    public static string Decompiler { get; } = $"{DecompilerName} {GetVersion(typeof(CSharpDecompiler).Assembly)}";

    public static string Runtime { get; } = RuntimeInformation.FrameworkDescription;

    public static string System { get; } = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})";

    /// <summary>The version <paramref name="assembly"/> was released as, without its commit, else
    /// its assembly version.</summary>
    private static string GetVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (informational is not null)
        {
            return WithoutCommit(informational.InformationalVersion);
        }

        var version = assembly.GetName().Version;
        if (version is null)
        {
            return "";
        }

        return version.ToString();
    }

    /// <summary>The version without the commit after its <c>+</c>.</summary>
    private static string WithoutCommit(string version)
    {
        var plus = version.IndexOf(BuildSeparator);
        if (plus < 0)
        {
            return version;
        }

        return version.Substring(0, plus);
    }
}
