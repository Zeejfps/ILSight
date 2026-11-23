using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ILSight.Core;

/// <summary>An assembly or entity, found by a handle in the file at <see cref="FilePath"/>: what a reference holds
/// but a local and a label. It compares as the reference it is.</summary>
public union Navigable(CodeReference.Assembly, CodeReference.Entity) : IEquatable<Navigable>
{
    /// <summary>The target's name as written where it was referenced.</summary>
    public string Name => this switch
    {
        CodeReference.Assembly assembly => assembly.Name,
        CodeReference.Entity entity => entity.Name,
    };

    public AssemblyPath FilePath => this switch
    {
        CodeReference.Assembly assembly => assembly.FilePath,
        CodeReference.Entity entity => entity.FilePath,
    };

    /// <summary>What it is found by in the file.</summary>
    public EntityHandle Handle => this switch
    {
        CodeReference.Assembly assembly => assembly.Handle,
        CodeReference.Entity entity => entity.Handle,
    };

    /// <summary><paramref name="entity"/> as a navigable; null for none. A null entity converted to a navigable
    /// would be one holding no case.</summary>
    public static Navigable? FromNullable(CodeReference.Entity? entity)
    {
        if (entity is null)
        {
            return null;
        }

        return entity;
    }

    /// <summary>The navigable as the reference it is.</summary>
    public static implicit operator CodeReference(Navigable navigable)
    {
        return navigable switch
        {
            CodeReference.Assembly assembly => assembly,
            CodeReference.Entity entity => entity,
        };
    }

    public bool Equals(Navigable other)
    {
        return ((CodeReference)this).Equals(other);
    }

    public override bool Equals(object? obj)
    {
        return ((CodeReference)this).Equals(obj);
    }

    public override int GetHashCode()
    {
        return ((CodeReference)this).GetHashCode();
    }

    /// <summary>The target as a person reads it: every kind of reference says what it points to.</summary>
    public override string ToString()
    {
        return ((CodeReference)this).ToString();
    }

    public static bool operator ==(Navigable left, Navigable right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Navigable left, Navigable right)
    {
        return !left.Equals(right);
    }
}
