using System.Diagnostics;

namespace ILSight.Core.ComparisonArea;

/// <summary>What an <see cref="ApiEntry"/> is: a namespace, in one assembly compared or both, or a type or member,
/// with how each assembly it is in has it.</summary>
public union ApiEntryKind(ApiEntryKind.Namespace, ApiEntryKind.Entity)
{
    public sealed record Namespace
    {
        public required ApiPresence Presence { get; init; }
    }

    public sealed record Entity
    {
        public required ApiSides Sides { get; init; }
    }

    /// <summary>How the entry differs from the left assembly to the right one, as the assemblies it is in say.</summary>
    public ApiChange Change => this switch
    {
        Namespace @namespace => @namespace.Presence switch
        {
            ApiPresence.LeftOnly => ApiChange.Removed,
            ApiPresence.RightOnly => ApiChange.Added,
            ApiPresence.Both => ApiChange.Unchanged,
            _ => throw new UnreachableException(),
        },
        Entity entity => entity.Sides.Change,
    };
}
