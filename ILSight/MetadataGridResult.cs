using ILSight.Core;
using ILSight.Core.Metadata;

namespace ILSight;

/// <summary>What the user asked of a <see cref="MetadataGridWidget.RenderMetadataGrid"/> this frame.</summary>
/// <param name="Target">The table row a token cell leads to.</param>
/// <param name="Definition">What a row stands for in the code, to decompile.</param>
/// <param name="InNewTab">Whether to show either in a new tab.</param>
public readonly record struct MetadataGridResult(MetadataAddress? Target, CodeReference.Navigable? Definition, bool InNewTab);
