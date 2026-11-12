using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What the summary of a node without code of its own lists.</summary>
internal abstract record SummaryInfo
{
    private SummaryInfo()
    {
    }

    /// <summary>A comment a line; an empty line is left blank. Read as they are written.</summary>
    public sealed record Comments(IEnumerable<string> Lines) : SummaryInfo;

    public sealed record Namespace(string Name, IReadOnlyList<NamespaceEntry> Entries) : SummaryInfo;

    public sealed record ForwardedType(string FullName, CodeReference? Target, string? Destination) : SummaryInfo;

    /// <summary>A references folder: its assemblies, then its modules.</summary>
    public sealed record References(IReadOnlyList<SummaryLink> Assemblies, IReadOnlyList<string> Modules) : SummaryInfo;

    public sealed record ModuleReference(string Name, bool ContainsMetadata, IReadOnlyList<SummaryLink> Imports) : SummaryInfo;

    public sealed record ImportedMethod(SummaryLink Method, string EntryPoint, string ModuleName) : SummaryInfo;

    /// <summary>A reference of <paramref name="Module"/>, with where it resolves from.</summary>
    public sealed record AssemblyReference(LoadedModule Module, ICSharpCode.Decompiler.Metadata.AssemblyReference Reference) : SummaryInfo;

    /// <summary>What an assembly uses from a reference, each type with what is used of it under it.</summary>
    public sealed record Used(IReadOnlyList<UsedEntry> Entries) : SummaryInfo;

    /// <summary>A line per type, going to it, as base and derived types are listed.</summary>
    public sealed record Types(IReadOnlyList<SummaryLink> Entries) : SummaryInfo;

    public sealed record Error(Exception Exception) : SummaryInfo;

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, Comments> comments, Action<TState, Namespace> @namespace,
        Action<TState, ForwardedType> forwardedType, Action<TState, References> references,
        Action<TState, ModuleReference> moduleReference, Action<TState, ImportedMethod> importedMethod,
        Action<TState, AssemblyReference> assemblyReference, Action<TState, Used> used, Action<TState, Types> types,
        Action<TState, Error> error)
    {
        switch (this)
        {
            case Comments c:
                comments(state, c);
                return;
            case Namespace c:
                @namespace(state, c);
                return;
            case ForwardedType c:
                forwardedType(state, c);
                return;
            case References c:
                references(state, c);
                return;
            case ModuleReference c:
                moduleReference(state, c);
                return;
            case ImportedMethod c:
                importedMethod(state, c);
                return;
            case AssemblyReference c:
                assemblyReference(state, c);
                return;
            case Used c:
                used(state, c);
                return;
            case Types c:
                types(state, c);
                return;
            case Error c:
                error(state, c);
                return;
            default:
                throw new UnreachableException($"SummaryInfo.Switch does not handle {GetType().Name}.");
        }
    }
}
