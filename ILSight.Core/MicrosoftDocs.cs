using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>The pages of Microsoft's .NET API reference.</summary>
public static class MicrosoftDocs
{
    private const string ApiReference = "https://learn.microsoft.com/dotnet/api/";

    /// <summary>The reference page of what <paramref name="node"/> stands for, as the reference names
    /// its pages, e.g. <c>system.collections.generic.list-1.add</c>: a namespace, or a type or member
    /// visible outside its assembly. Null for any other node.</summary>
    public static string? FindPage(BrowseNode node)
    {
        if (node.Subject is NodeSubject.Namespace ns)
        {
            return FindNamespacePage(ns.Value);
        }

        var entity = BrowseNodes.FindEntity(node);
        if (entity is not null)
        {
            return FindPage(entity);
        }

        return null;
    }

    /// <summary>The reference page of <paramref name="entity"/>, a type or member visible outside its
    /// assembly. Null for any other entity.</summary>
    internal static string? FindPage(IEntity entity)
    {
        var name = ReadPageName(entity);
        if (name is null)
        {
            return null;
        }

        return PageAt(name);
    }

    /// <summary>What names the reference page of <paramref name="entity"/>, read from the library's objects: the
    /// reflection name of the type, or of the member's declaring type, and the member's name. Null for an entity not
    /// visible outside its assembly, or a member no type declares. <see cref="PageAt(ApiPageName)"/> makes the page of
    /// it, apart from the read.</summary>
    internal static ApiPageName? ReadPageName(IEntity entity)
    {
        if (!EntityText.IsPublicApi(entity))
        {
            return null;
        }

        if (entity is ITypeDefinition type)
        {
            return new ApiPageName { Type = type.ReflectionName, Member = null };
        }

        if (entity is IMember member)
        {
            var declaring = member.DeclaringTypeDefinition;
            if (declaring is null)
            {
                return null;
            }

            return new ApiPageName { Type = declaring.ReflectionName, Member = member.Name };
        }

        return null;
    }

    /// <summary>The page <paramref name="name"/> names, as the reference names its pages.</summary>
    internal static string PageAt(ApiPageName name)
    {
        var type = TypePageName(name.Type);
        if (name.Member is null)
        {
            return PageAt(type);
        }

        // The docs name a constructor's page -ctor.
        return PageAt(type + "." + name.Member.Replace('.', '-'));
    }

    private static string? FindNamespacePage(NamespaceInfo ns)
    {
        if (!ns.IsPublicApi)
        {
            return null;
        }

        if (ns.Name.Length == 0)
        {
            return null;
        }

        return PageAt(ns.Name);
    }

    private static string PageAt(string name)
    {
        return ApiReference + name.ToLowerInvariant();
    }

    // The docs name a generic nested type's page Outer-1.Inner.
    private static string TypePageName(string reflectionName)
    {
        return reflectionName
            .Replace('`', '-')
            .Replace('+', '.');
    }
}
