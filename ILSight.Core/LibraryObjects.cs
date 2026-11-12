using System.Runtime.CompilerServices;

namespace ILSight.Core;

/// <summary>The library's objects the I/O side keeps against the handles it hands out (a node's subject, a namespace),
/// so that each lives exactly as long as its handle and no handle holds one. Read only by the I/O side.</summary>
internal static class LibraryObjects<THandle, TLibrary>
    where THandle : class
    where TLibrary : class
{
    private static readonly ConditionalWeakTable<THandle, TLibrary> Kept = new();

    /// <summary><paramref name="handle"/>, with <paramref name="library"/> kept against it.</summary>
    public static THandle Keep(THandle handle, TLibrary library)
    {
        Kept.Add(handle, library);
        return handle;
    }

    /// <summary>What is kept against <paramref name="handle"/>; throws for a handle made without it.</summary>
    public static TLibrary Get(THandle handle)
    {
        if (!Kept.TryGetValue(handle, out var library))
        {
            throw new InvalidOperationException($"No {typeof(TLibrary).Name} was kept against this {typeof(THandle).Name}.");
        }

        return library;
    }
}
