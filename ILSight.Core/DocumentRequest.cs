using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What <see cref="NodeDecompiler"/> writes a document of, titled <see cref="Title"/>; a
/// failure to write it is reported as one of <see cref="FullName"/>.</summary>
internal abstract record DocumentRequest
{
    private DocumentRequest()
    {
    }

    public required string Title { get; init; }

    public required string FullName { get; init; }

    /// <summary>A module's summary and attributes, not its types.</summary>
    public sealed record ModulePreview : DocumentRequest
    {
        public required LoadedModule Module { get; init; }
    }

    public sealed record Type : DocumentRequest
    {
        public required DefinedType Definition { get; init; }
    }

    public sealed record Member : DocumentRequest
    {
        public required DefinedMember Definition { get; init; }
    }

    /// <summary>What a node without code of its own lists, read from <see cref="Module"/>, or
    /// from none for a package's.</summary>
    public sealed record Summary : DocumentRequest
    {
        public required SummaryInfo Info { get; init; }

        public required LoadedModule? Module { get; init; }
    }

    /// <summary>A resource as its text, or a hex dump of its bytes, which <see cref="Read"/> reads.</summary>
    public sealed record Resource : DocumentRequest
    {
        public required LoadedModule? Module { get; init; }

        public required string Description { get; init; }

        public required Func<byte[]> Read { get; init; }
    }

    /// <summary>WPF's compiled XAML, as the XAML it was compiled from.</summary>
    public sealed record Baml : DocumentRequest
    {
        public required LoadedModule Module { get; init; }

        public required byte[] Data { get; init; }
    }

    /// <summary>Why a listed file could not be read.</summary>
    public sealed record Failure : DocumentRequest
    {
        public required FailedAssembly Assembly { get; init; }
    }

    /// <summary>What the handler of whichever case this is makes of it.</summary>
    public TResult Match<TResult>(Func<ModulePreview, TResult> modulePreview, Func<Type, TResult> type,
        Func<Member, TResult> member, Func<Summary, TResult> summary, Func<Resource, TResult> resource,
        Func<Baml, TResult> baml, Func<Failure, TResult> failure)
    {
        return this switch
        {
            ModulePreview c => modulePreview(c),
            Type c => type(c),
            Member c => member(c),
            Summary c => summary(c),
            Resource c => resource(c),
            Baml c => baml(c),
            Failure c => failure(c),
            _ => throw new UnreachableException($"DocumentRequest.Match does not handle {GetType().Name}."),
        };
    }

    /// <summary>Does what the handler of whichever case this is does with it and <paramref name="state"/>, which
    /// it is handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public void Switch<TState>(TState state, Action<TState, ModulePreview> modulePreview, Action<TState, Type> type,
        Action<TState, Member> member, Action<TState, Summary> summary, Action<TState, Resource> resource,
        Action<TState, Baml> baml, Action<TState, Failure> failure)
    {
        switch (this)
        {
            case ModulePreview c:
                modulePreview(state, c);
                return;
            case Type c:
                type(state, c);
                return;
            case Member c:
                member(state, c);
                return;
            case Summary c:
                summary(state, c);
                return;
            case Resource c:
                resource(state, c);
                return;
            case Baml c:
                baml(state, c);
                return;
            case Failure c:
                failure(state, c);
                return;
            default:
                throw new UnreachableException($"DocumentRequest.Switch does not handle {GetType().Name}.");
        }
    }
}
