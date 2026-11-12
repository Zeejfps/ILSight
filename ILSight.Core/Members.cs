using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

internal static class Members
{
    private static readonly DecompilerSettings Settings = Core.Settings.Create();

    /// <summary>Whether both name the same definition: the same token in the same file.</summary>
    public static bool IsSame(IEntity entity, IEntity other)
    {
        return entity.MetadataToken == other.MetadataToken && entity.ParentModule?.MetadataFile == other.ParentModule?.MetadataFile;
    }

    public static bool IsHidden(IEntity entity)
    {
        return IsMissing(entity) || IsCompilerGenerated(entity);
    }

    /// <summary>Whether <paramref name="entity"/> has no metadata of its own to show.</summary>
    public static bool IsMissing(IEntity entity)
    {
        return entity.MetadataToken.IsNil;
    }

    /// <summary>Whether the compiler made <paramref name="entity"/> up, or what holds it: the property or
    /// event of an accessor, and the types it is declared in.</summary>
    public static bool IsOrSitsInCompilerGenerated(IEntity entity)
    {
        for (var holder = entity; holder is not null; holder = HolderOf(holder))
        {
            if (IsCompilerGenerated(holder))
            {
                return true;
            }
        }
        return false;
    }

    private static IEntity? HolderOf(IEntity entity)
    {
        if (entity is IMethod method && method.IsAccessor && method.AccessorOwner is not null)
        {
            return method.AccessorOwner;
        }

        return entity.DeclaringTypeDefinition;
    }

    public static bool IsCompilerGenerated(IEntity entity)
    {
        var file = entity.ParentModule?.MetadataFile;
        return file is not null && CSharpDecompiler.MemberIsHidden(file, entity.MetadataToken, Settings);
    }

    /// <summary>The accessors of a property or event, in ILSpy's order; none for other entities.</summary>
    public static IEnumerable<IMethod> Accessors(IEntity entity)
    {
        IEnumerable<IMethod?> accessors = entity switch
        {
            IProperty property => [property.Getter, property.Setter],
            IEvent @event => [@event.AddAccessor, @event.RemoveAccessor, @event.InvokeAccessor],
            _ => [],
        };
        return accessors.OfType<IMethod>();
    }
}
