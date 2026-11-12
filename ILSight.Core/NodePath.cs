using System.Collections.Immutable;

namespace ILSight.Core;

/// <summary>
/// Where a <see cref="BrowseNode"/> sits: the listed file whose tree it is in, and the steps down from that
/// tree's root, naming each node by what it is. Unlike the node, it stays meaningful after the assembly is
/// closed or reloaded; <see cref="BrowseSessions.FindNode(BrowseSession, LoadedAssembly, NodePath)"/> finds the node it names again.
/// </summary>
public sealed class NodePath : IEquatable<NodePath>
{
    /// <summary>The absolute path of the listed file the path starts from: an assembly, or the package
    /// an assembly was read out of.</summary>
    public required AssemblyPath FilePath { get; init; }

    /// <summary>What each node down from the root is, and its label, the node itself last.</summary>
    public required ImmutableArray<NodePathStep> Steps { get; init; }

    /// <summary>The label of the node this names, as it showed when the path was taken; the file's name
    /// for the root.</summary>
    public string Label => Steps.Length == 0 ? FilePath.FileName : Steps[^1].Label;

    /// <summary>Equal when it starts from the same file and names the same node at each step, whatever
    /// the labels.</summary>
    public bool Equals(NodePath? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (other.Steps.Length != Steps.Length)
        {
            return false;
        }

        if (!FilePath.Equals(other.FilePath))
        {
            return false;
        }

        for (var i = Steps.Length - 1; i >= 0; i--)
        {
            if (Steps[i].Segment != other.Steps[i].Segment)
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is NodePath other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FilePath);
        foreach (var step in Steps)
        {
            hash.Add(step.Segment);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return NodeDefinitions.DescribePath(this);
    }

    /// <summary>The path to the node reached from the root of the file at <paramref name="filePath"/>
    /// through <paramref name="steps"/>, as <see cref="NodePath.Steps"/> gives them.</summary>
    public static NodePath From(AssemblyPath filePath, IEnumerable<NodePathStep> steps)
    {
        return new NodePath { FilePath = filePath, Steps = steps.ToImmutableArray() };
    }
}
