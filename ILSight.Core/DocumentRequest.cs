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
}
