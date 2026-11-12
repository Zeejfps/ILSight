using System.Diagnostics;

namespace ILSight.Core;

/// <summary>What <see cref="NodeDecompiler"/> writes a document of, titled <see cref="Title"/>; a
/// failure to write it is reported as one of <see cref="FullName"/>.</summary>
internal abstract record DocumentRequest
{
    private DocumentRequest(string title, string fullName)
    {
        Title = title;
        FullName = fullName;
    }

    public string Title { get; }

    public string FullName { get; }

    /// <summary>A module's summary and attributes, not its types.</summary>
    public sealed record ModulePreview(LoadedModule Module, string Title, string FullName) : DocumentRequest(Title, FullName);

    public sealed record Type(DefinedType Definition, string Title, string FullName) : DocumentRequest(Title, FullName);

    public sealed record Member(DefinedMember Definition, string Title, string FullName) : DocumentRequest(Title, FullName);

    /// <summary>What a node without code of its own lists, read from <paramref name="Module"/>, or
    /// from none for a package's.</summary>
    public sealed record Summary(SummaryInfo Info, LoadedModule? Module, string Title, string FullName) : DocumentRequest(Title, FullName);

    /// <summary>A resource as its text, or a hex dump of its bytes, which <paramref name="Read"/> reads.</summary>
    public sealed record Resource(LoadedModule? Module, string Description, Func<byte[]> Read, string Title, string FullName)
        : DocumentRequest(Title, FullName);

    /// <summary>WPF's compiled XAML, as the XAML it was compiled from.</summary>
    public sealed record Baml(LoadedModule Module, byte[] Data, string Title, string FullName) : DocumentRequest(Title, FullName);

    /// <summary>Why a listed file could not be read.</summary>
    public sealed record Failure(FailedAssembly Assembly, string Title, string FullName) : DocumentRequest(Title, FullName);

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
