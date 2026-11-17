using ILSight.Core;

namespace ILSight.CodeViewArea;

/// <summary>What the user asked a code view to do, besides changing the size of its code.</summary>
public union CodeViewIntent(CodeViewIntent.Navigate, CodeViewIntent.ToggleBookmark, CodeViewIntent.Analyze,
    CodeViewIntent.ShowInMetadata, CodeViewIntent.ShowInApiReference, CodeViewIntent.ShowInDiagram,
    CodeViewIntent.ShowInNewDiagram)
{
    /// <summary>Go to <see cref="Target"/>, in a new tab when <see cref="InNewTab"/>.</summary>
    public sealed record Navigate
    {
        public required CodeReference.Navigable Target { get; init; }

        public required bool InNewTab { get; init; }
    }

    /// <summary>Marks or unmarks <see cref="Line"/>, which the document already holds.</summary>
    public sealed record ToggleBookmark(int Line);

    public sealed record Analyze(CodeReference.Entity Entity);

    public sealed record ShowInMetadata(CodeReference.Navigable Target);

    public sealed record ShowInApiReference(CodeReference.Entity Entity);

    public sealed record ShowInDiagram(CodeReference.Entity Type);

    public sealed record ShowInNewDiagram(CodeReference.Entity Type);

    /// <summary>Whether it can be done in the code of an assembly that was closed (<see cref="TabLoad.ShowClosed"/>),
    /// reading nothing of it: toggling a bookmark, the document already holding the line; not following, analyzing or
    /// showing a reference, which looks the assembly up.</summary>
    public bool CanRunWhenClosed => this switch
    {
        ToggleBookmark => true,
        Navigate or Analyze or ShowInMetadata or ShowInApiReference or ShowInDiagram or ShowInNewDiagram => false,
    };
}
