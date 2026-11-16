using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Core;

/// <summary>What an assembly reference row says, read out of the library's reference once, under the read's catch, so
/// that the app's resolving after it reads no metadata: a reference row's properties read the user's data each time
/// they are asked.</summary>
internal sealed class AssemblyReferenceFacts : IAssemblyReference
{
    public required string Name { get; init; }

    public required string FullName { get; init; }

    public required Version? Version { get; init; }

    public required string? Culture { get; init; }

    public required byte[]? PublicKeyToken { get; init; }

    public required bool IsWindowsRuntime { get; init; }

    public required bool IsRetargetable { get; init; }

    /// <summary>The facts of the reference row <paramref name="handle"/> names in <paramref name="file"/>, each read now:
    /// what the library throws reading them goes on to the caller's catch.</summary>
    public static AssemblyReferenceFacts Read(MetadataFile file, AssemblyReferenceHandle handle)
    {
        IAssemblyReference reference = new ICSharpCode.Decompiler.Metadata.AssemblyReference(file, handle);
        return new AssemblyReferenceFacts
        {
            Name = reference.Name,
            FullName = reference.FullName,
            Version = reference.Version,
            Culture = reference.Culture,
            PublicKeyToken = reference.PublicKeyToken,
            IsWindowsRuntime = reference.IsWindowsRuntime,
            IsRetargetable = reference.IsRetargetable,
        };
    }
}
