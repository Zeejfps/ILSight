using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core;

/// <summary>
/// An immutable pointer from code or the tree to an assembly, entity, local or label. References to
/// the same target compare equal, so every occurrence of a symbol in a document can be matched.
/// <see cref="Navigable"/> references can be resolved to a tree node with
/// <see cref="NodeLookup.FindAsync(AssemblyList, BrowseSession, Navigable, CancellationToken)"/>.
/// </summary>
public union CodeReference(CodeReference.Assembly, CodeReference.Entity, CodeReference.Local, CodeReference.Label) : IEquatable<CodeReference>
{
    /// <summary>The assembly in the file at <see cref="FilePath"/> itself, or one that file
    /// references by <see cref="Referenced"/>.</summary>
    public sealed class Assembly
    {
        /// <summary>The target's name as written where it was referenced.</summary>
        public required string Name { get; init; }

        public required AssemblyPath FilePath { get; init; }

        /// <summary>The reference to the assembly from the file; null for the file's own assembly.</summary>
        public required AssemblyReferenceHandle? Referenced { get; init; }

        /// <summary>The reference in the file, or the file's own assembly, which <see cref="Referenced"/>
        /// says, so that the two can't disagree.</summary>
        public EntityHandle Handle
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

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || IsSameNavigable(Handle, FilePath, obj);
        }

        public override int GetHashCode()
        {
            return HashNavigable(Handle, FilePath);
        }

        public override string ToString()
        {
            return $"Assembly {Name}";
        }

        public static bool operator ==(Assembly? left, Assembly? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Assembly? left, Assembly? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>A type or member, defined or referenced by its handle in the file.</summary>
    public sealed class Entity
    {
        /// <summary>The target's name as written where it was referenced.</summary>
        public required string Name { get; init; }

        public required AssemblyPath FilePath { get; init; }

        public required EntityKind Kind { get; init; }

        /// <summary>The type or member, as the file names it.</summary>
        public required EntityHandle Target { get; init; }

        /// <summary>What it is found by in the file.</summary>
        public EntityHandle Handle => Target;

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || IsSameNavigable(Handle, FilePath, obj);
        }

        public override int GetHashCode()
        {
            return HashNavigable(Handle, FilePath);
        }

        public override string ToString()
        {
            return $"{Kind} {Name}";
        }

        public static bool operator ==(Entity? left, Entity? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Entity? left, Entity? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>A local variable or parameter, numbered by <see cref="Key"/> within <see cref="Scope"/>.</summary>
    public sealed class Local
    {
        /// <summary>The target's name as written where it was referenced.</summary>
        public required string Name { get; init; }

        public required int Key { get; init; }
        public required Entity? Scope { get; init; }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Local local && Key == local.Key && Equals(Scope, local.Scope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(nameof(Local), Key, Scope);
        }

        public override string ToString()
        {
            return $"Local {Name}";
        }

        public static bool operator ==(Local? left, Local? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Local? left, Local? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>An IL label, numbered by <see cref="Key"/> within <see cref="Scope"/>.</summary>
    public sealed class Label
    {
        /// <summary>The target's name as written where it was referenced.</summary>
        public required string Name { get; init; }

        public required int Key { get; init; }
        public required Entity? Scope { get; init; }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Label label && Key == label.Key && Equals(Scope, label.Scope);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(nameof(Label), Key, Scope);
        }

        public override string ToString()
        {
            return $"Label {Name}";
        }

        public static bool operator ==(Label? left, Label? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Label? left, Label? right)
        {
            return !Equals(left, right);
        }
    }

    /// <summary>The target's name as written where it was referenced.</summary>
    public string Name => this switch
    {
        Assembly assembly => assembly.Name,
        Entity entity => entity.Name,
        Local local => local.Name,
        Label label => label.Name,
    };

    /// <summary><paramref name="entity"/> as a reference; null for none. A null entity converted to a reference would
    /// be one holding no case.</summary>
    public static CodeReference? FromNullable(Entity? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return entity;
    }

    /// <summary>The reference as a navigable one: an assembly's or an entity's; null for a local's or a label's.</summary>
    public Navigable? AsNavigable => this switch
    {
        Assembly assembly => assembly,
        Entity entity => entity,
        Local or Label => (Navigable?)null,
    };

    // The case held, as the object whose equality the reference's is.
    private object Held => this switch
    {
        Assembly assembly => assembly,
        Entity entity => entity,
        Local local => local,
        Label label => label,
    };

    public bool Equals(CodeReference other)
    {
        return Held.Equals(other.Held);
    }

    // A navigable compares as the reference it is, as it did when it was one.
    public override bool Equals(object? obj)
    {
        if (obj is CodeReference reference)
        {
            return Equals(reference);
        }

        return obj is Navigable navigable && Equals((CodeReference)navigable);
    }

    public override int GetHashCode()
    {
        return Held.GetHashCode();
    }

    /// <summary>The target as a person reads it: every kind of reference says what it points to.</summary>
    public override string ToString()
    {
        return this switch
        {
            Assembly assembly => assembly.ToString(),
            Entity entity => entity.ToString(),
            Local local => local.ToString(),
            Label label => label.ToString(),
        };
    }

    public static bool operator ==(CodeReference left, CodeReference right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CodeReference left, CodeReference right)
    {
        return !left.Equals(right);
    }

    // Navigables compare by the handle and the file, whichever case holds them, as they did when both were one type.
    private static bool IsSameNavigable(EntityHandle handle, AssemblyPath filePath, object? other)
    {
        if (other is Assembly assembly)
        {
            return handle == assembly.Handle && filePath.Equals(assembly.FilePath);
        }

        return other is Entity entity && handle == entity.Handle && filePath.Equals(entity.FilePath);
    }

    private static int HashNavigable(EntityHandle handle, AssemblyPath filePath)
    {
        return HashCode.Combine(filePath, MetadataTokens.GetToken(handle));
    }
}
