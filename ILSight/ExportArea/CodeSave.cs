using ILSight.Core;

namespace ILSight.ExportArea;

/// <summary>What saving a node to a file the user picked writes: a resource's bytes as they are, straight away, or the
/// node's code as one file, an export the exports panel queues.</summary>
public union CodeSave(CodeSave.ResourceBytes, CodeSave.Code)
{
    /// <summary>The bytes of the resource <see cref="Node"/> stands for, written to <see cref="File"/> as they are.</summary>
    public sealed record ResourceBytes
    {
        public required BrowseNode Node { get; init; }

        public required string File { get; init; }
    }

    /// <summary>The node's code, written as <see cref="Work"/> says.</summary>
    public sealed record Code
    {
        public required ExportWork.CodeFile Work { get; init; }
    }
}
