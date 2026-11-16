using System.Diagnostics;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>Where a method's code is, read out of the library for the analyzers: its module and file, its token, the
/// token of the type declaring it, and whether it has a body.</summary>
internal sealed class MethodCode
{
    public required MetadataModule Module { get; init; }

    public required MetadataFile File { get; init; }

    public required EntityHandle Token { get; init; }

    /// <summary>The token of the type declaring the method; null when none does.</summary>
    public required EntityHandle? DeclaringType { get; init; }

    public required bool HasBody { get; init; }

    /// <summary>The token of the type declaring the method, which every member has.</summary>
    public EntityHandle Declaring => DeclaringType ?? throw new UnreachableException("The method has no type declaring it, though every member has.");

    /// <summary>The method's row in the MethodDef table, which every method of a loaded assembly has.</summary>
    public MethodDefinitionHandle Handle => (MethodDefinitionHandle)Token;
}
