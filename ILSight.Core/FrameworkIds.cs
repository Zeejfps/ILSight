namespace ILSight.Core;

/// <summary>The target frameworks ILSpy offers to resolve an assembly's references against.</summary>
public static class FrameworkIds
{
    /// <summary>The frameworks offered to resolve an assembly's references against, newest first in
    /// each family.</summary>
    public static IReadOnlyList<TargetFrameworkId> Common { get; } =
    [
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "11.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "10.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "9.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "8.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "7.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "6.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "5.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "3.1"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "3.0"),
        TargetFrameworkId.Of(TargetFrameworkId.CoreApp, "2.1"),
        TargetFrameworkId.Of(TargetFrameworkId.Standard, "2.1"),
        TargetFrameworkId.Of(TargetFrameworkId.Standard, "2.0"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.8.1"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.8"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.7.2"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.7.1"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.7"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.6.2"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.6.1"),
        TargetFrameworkId.Of(TargetFrameworkId.Framework, "4.6"),
    ];

    /// <summary>Whether an assembly built for <paramref name="id"/> runs on .NET Core, as the running
    /// runtime is: one built for .NET Core or .NET Standard, or for a framework not known.</summary>
    internal static bool RunsOnCore(TargetFrameworkId? id)
    {
        return id is null || id.IsCoreOrStandard;
    }
}
