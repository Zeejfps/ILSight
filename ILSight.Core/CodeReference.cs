using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core;

/// <summary>
/// An immutable pointer from code or the tree to an assembly, entity, local or label. References to
/// the same target compare equal, so every occurrence of a symbol in a document can be matched.
/// <see cref="Navigable"/> references can be resolved to a tree node with
/// <see cref="NodeLookup.FindAsync(AssemblyList, BrowseSession, CodeReference.Navigable, CancellationToken)"/>.
/// </summary>
public abstract class CodeReference : IEquatable<CodeReference>
{
    private CodeReference()
    {
    }

    /// <summary>The target's name as written where it was referenced.</summary>
    public required string Name { get; init; }

    /// <summary>An assembly or entity, found by a handle in the file at <see cref="FilePath"/>.</summary>
    public abstract class Navigable : CodeReference
    {
        private protected Navigable()
        {
        }

        public required AssemblyPath FilePath { get; init; }

        /// <summary>What it is found by in the file.</summary>
        public abstract EntityHandle Handle { get; }

        public override bool Equals(CodeReference? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other is not Navigable navigable)
            {
                return false;
            }

            return Handle == navigable.Handle && FilePath.Equals(navigable.FilePath);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FilePath, MetadataTokens.GetToken(Handle));
        }
    }

    /// <summary>The assembly in the file at <see cref="Navigable.FilePath"/> itself, or one that file
    /// references by <see cref="Referenced"/>.</summary>
    public sealed class Assembly : Navigable
    {
        /// <summary>The reference to the assembly from the file; null for the file's own assembly.</summary>
        public required AssemblyReferenceHandle? Referenced { get; init; }

        /// <summary>The reference in the file, or the file's own assembly, which <see cref="Referenced"/>
        /// says, so that the two can't disagree.</summary>
        public override EntityHandle Handle
        {
            get
            {
                if (Referenced is null)
                {
                    return EntityHandle.AssemblyDefinition;
                }

                return Referenced.Value;
            }
        }

        public override string ToString()
        {
            return $"Assembly {Name}";
        }
    }

    /// <summary>A type or member, defined or referenced by its handle in the file.</summary>
    public sealed class Entity : Navigable
    {
        public required EntityKind Kind { get; init; }

        /// <summary>The type or member, as the file names it.</summary>
        public required EntityHandle Target { get; init; }

        public override EntityHandle Handle => Target;

        public override string ToString()
        {
            return $"{Kind} {Name}";
        }
    }

    /// <summary>A local variable or parameter, numbered by <see cref="Key"/> within <see cref="Scope"/>.</summary>
    public sealed class Local : CodeReference
    {
        public required int Key { get; init; }
        public required Entity? Scope { get; init; }

        public override bool Equals(CodeReference? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is Local local && Key == local.Key && Equals(Scope, local.Scope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(nameof(Local), Key, Scope);
        }

        public override string ToString()
        {
            return $"Local {Name}";
        }
    }

    /// <summary>An IL label, numbered by <see cref="Key"/> within <see cref="Scope"/>.</summary>
    public sealed class Label : CodeReference
    {
        public required int Key { get; init; }
        public required Entity? Scope { get; init; }

        public override bool Equals(CodeReference? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other is Label label && Key == label.Key && Equals(Scope, label.Scope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(nameof(Label), Key, Scope);
        }

        public override string ToString()
        {
            return $"Label {Name}";
        }
    }

    public abstract bool Equals(CodeReference? other);

    public sealed override bool Equals(object? obj)
    {
        return obj is CodeReference other && Equals(other);
    }

    public abstract override int GetHashCode();

    public static bool operator ==(CodeReference? left, CodeReference? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(CodeReference? left, CodeReference? right)
    {
        return !Equals(left, right);
    }
}
