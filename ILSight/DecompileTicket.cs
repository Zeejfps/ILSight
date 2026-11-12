using ILSight.Core;

namespace ILSight;

/// <summary>A load <see cref="Decompilation.StartDecompiling"/> started in a tab, for
/// <see cref="DecompileIntoTabHandler"/> to bring in: the tab and the node it shows loading, the language and
/// options to show it in, and the work of the tab it runs as, which newer work on the tab or closing it
/// cancels.</summary>
public sealed class DecompileTicket
{
    public required DocumentTab Tab { get; init; }

    public required BrowseNode Node { get; init; }

    public required DecompilationLanguage Language { get; init; }

    public required DecompilerOptions Options { get; init; }

    public required WorkTicket Work { get; init; }
}
