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
        if (!EntityText.IsPublicApi(entity))
        {
            return null;
        }

        var name = FindPageName(entity);
        if (name is null)
        {
            return null;
        }

        return PageAt(name);
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

    private static string? FindPageName(IEntity entity)
    {
        if (entity is ITypeDefinition type)
        {
            return TypePageName(type);
        }

        if (entity is IMember member)
        {
            return MemberPageName(member);
        }

        return null;
    }

    // The docs name a generic nested type's page Outer-1.Inner.
    private static string TypePageName(ITypeDefinition type)
    {
        return type.ReflectionName
            .Replace('`', '-')
            .Replace('+', '.');
    }

    private static string? MemberPageName(IMember member)
    {
        var declaring = member.DeclaringTypeDefinition;
        if (declaring is null)
        {
            return null;
        }

        // The docs name a constructor's page -ctor.
        var name = member.Name.Replace('.', '-');
        return TypePageName(declaring) + "." + name;
    }
}
