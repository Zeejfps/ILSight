using ILSight.Core;

namespace ILSight.CodeView;

/// <summary>What the user asked a code view to do, besides changing the size of its code.</summary>
public abstract record CodeViewIntent
{
    private CodeViewIntent()
    {
    }

    /// <summary>Go to <see cref="Target"/>, in a new tab when <see cref="InNewTab"/>.</summary>
    public sealed record Navigate(CodeReference.Navigable Target, bool InNewTab) : CodeViewIntent;

    public sealed record ToggleBookmark(int Line) : CodeViewIntent;

    public sealed record Analyze(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInMetadata(CodeReference.Navigable Target) : CodeViewIntent;

    public sealed record ShowInApiReference(CodeReference.Entity Entity) : CodeViewIntent;

    public sealed record ShowInDiagram(CodeReference.Entity Type) : CodeViewIntent;

    public sealed record ShowInNewDiagram(CodeReference.Entity Type) : CodeViewIntent;
}
