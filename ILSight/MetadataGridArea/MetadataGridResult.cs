using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight.MetadataGridArea;

/// <summary>What the user asked of a <see cref="MetadataGridWidget.RenderMetadataGrid"/> this frame.</summary>
public readonly record struct MetadataGridResult
{
    /// <summary>The table row a token cell leads to.</summary>
    public required MetadataAddress? Target { get; init; }

    /// <summary>What a row stands for in the code, to decompile.</summary>
    public required CodeReference.Navigable? Definition { get; init; }

    /// <summary>Whether to show either in a new tab.</summary>
    public required bool InNewTab { get; init; }
}
