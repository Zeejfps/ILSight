using ILSight.Core;

namespace ILSight.Tests;

/// <summary>The package ids and versions tests write as text.</summary>
internal static class Packages
{
    /// <summary>The package id <paramref name="text"/> writes; throws when it is not one.</summary>
    public static PackageId Id(string text)
    {
        return PackageId.Parse(text) ?? throw new ArgumentException($"Not a package id: '{text}'", nameof(text));
    }

    /// <summary>The package version <paramref name="text"/> writes; throws when it is not one.</summary>
    public static PackageVersion Version(string text)
    {
        return PackageVersion.Parse(text) ?? throw new ArgumentException($"Not a package version: '{text}'", nameof(text));
    }
}
