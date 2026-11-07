// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;

namespace ILSight.Core.Analysis;

/// <summary>
/// Where the symbols related to an analyzed entity can be: only inside its type when it is
/// private, its assembly and the friends it shows internals to when it is internal, and otherwise
/// every open assembly that references its type.
/// </summary>
internal sealed class AnalyzerScope
{
    public required AssemblyList List { get; init; }

    public required IReadOnlyList<LoadedModule> Assemblies { get; init; }

    public required Accessibility EffectiveAccessibility { get; init; }

    /// <summary>Whether the analyzed symbol is reachable only from inside <see cref="TypeScope"/>.</summary>
    public required bool IsLocal { get; init; }

    public required ITypeDefinition TypeScope { get; init; }
}
