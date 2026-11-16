using System.Reflection;

namespace ILSight.Tests;

/// <summary>A library object every member of which throws as a corrupt file's lazy decode does.</summary>
public class UnreadableLibraryObject : DispatchProxy
{
    public static T Create<T>() where T : class
    {
        return Create<T, UnreadableLibraryObject>();
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        throw new BadImageFormatException($"The metadata behind {targetMethod?.Name} is corrupt.");
    }
}
