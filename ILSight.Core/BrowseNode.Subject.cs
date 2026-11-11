using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

public static partial class BrowseNodes
{
    private static LoadedAssembly GetAssembly(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.TypeDefinition type => type.Definition.Module,
            NodeSubject.MemberDefinition member => member.Definition.Module,
            NodeSubject.References references => references.Owner,
            NodeSubject.AssemblyReference reference => reference.Value.Module,
            NodeSubject.ReferencedTypes types => types.Reference.Module,
            NodeSubject.TypeReference type => type.Value.Module,
            NodeSubject.MemberReference member => member.Value.Module,
            NodeSubject.ModuleReference module => module.Value.Module,
            NodeSubject.ImportedMethod import => import.Value.Shown.Module,
            NodeSubject.ForwardedType forwarded => forwarded.Value.Module,
            NodeSubject.BaseTypes baseTypes => baseTypes.Type.Module,
            NodeSubject.BaseType baseType => baseType.Value.Module,
            NodeSubject.DerivedTypes derivedTypes => derivedTypes.Type.Module,
            NodeSubject.DerivedType derived => derived.Value.Module,
            NodeSubject.Resources resources => resources.Owner,
            NodeSubject.ResourceContainer container => container.Value.Module,
            NodeSubject.ResourceEntry entry => entry.Value.Module,
            NodeSubject.Metadata metadata => metadata.Value.Module,
            NodeSubject.MetadataTables tables => tables.Folder.Module,
            NodeSubject.MetadataTable table => table.Value.Module,
            NodeSubject.Package package => package.Value.Package,
            NodeSubject.PackageFile file => file.Value.Package,
            NodeSubject.Module module => module.Value,
            NodeSubject.Namespace ns => ns.Value.Module,
            NodeSubject.FailedAssembly failed => failed.Value,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The bytes of the resource the node stands for; null for a node of anything else.</summary>
    internal static ResourceData? FindResourceData(BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.ResourceContainer container => ResourceNodes.CreateData(container.Value),
            NodeSubject.ResourceEntry entry => ResourceNodes.CreateData(entry.Value),
            NodeSubject.PackageFile file => ResourceNodes.CreateData(file.Value),
            NodeSubject.TypeDefinition or NodeSubject.MemberDefinition or NodeSubject.References or NodeSubject.AssemblyReference
                or NodeSubject.ReferencedTypes or NodeSubject.TypeReference or NodeSubject.MemberReference or NodeSubject.ModuleReference
                or NodeSubject.ImportedMethod or NodeSubject.ForwardedType or NodeSubject.BaseTypes or NodeSubject.BaseType
                or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources or NodeSubject.Metadata
                or NodeSubject.MetadataTables or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly => null,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>A qualified name for copying, e.g. <c>System.Collections.Generic.List&lt;T&gt;.Add(T)</c>.</summary>
    public static string GetFullName(BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => EntityText.FullName(type.Definition.Symbol),
            NodeSubject.MemberDefinition member => EntityText.FullName(member.Definition.Symbol),
            NodeSubject.AssemblyReference reference => reference.Value.Reference.FullName,
            NodeSubject.TypeReference type => type.Value.FullName,
            NodeSubject.MemberReference member => member.Value.FullName,
            NodeSubject.ImportedMethod import => $"{import.Value.DeclaringTypeName}.{import.Value.Label}",
            NodeSubject.ForwardedType forwarded => forwarded.Value.FullName,
            NodeSubject.Package package => package.Value.FullName,
            NodeSubject.PackageFile file => file.Value.File.FullName,
            NodeSubject.Module module => module.Value.FullName,
            NodeSubject.Namespace ns => ns.Value.Name,
            NodeSubject.FailedAssembly failed => failed.Value.FilePath.Text,
            NodeSubject.References or NodeSubject.ReferencedTypes or NodeSubject.ModuleReference or NodeSubject.BaseTypes
                or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources
                or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables
                or NodeSubject.MetadataTable => node.Text,
            _ => throw new UnreachableException(),
        };
    }

    private static string GetText(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.TypeDefinition type => EntityText.Label(type.Definition.Symbol),
            NodeSubject.MemberDefinition member => EntityText.Label(member.Definition.Symbol),
            NodeSubject.References => "References",
            NodeSubject.AssemblyReference reference => reference.Value.Reference.Name,
            NodeSubject.ReferencedTypes => "Referenced types",
            NodeSubject.TypeReference type => type.Value.Label,
            NodeSubject.MemberReference member => EntityText.Label(member.Value.Declared),
            NodeSubject.ModuleReference module => module.Value.Name,
            NodeSubject.ImportedMethod import => import.Value.Label,
            NodeSubject.ForwardedType forwarded => forwarded.Value.Label,
            NodeSubject.BaseTypes => "Base Types",
            NodeSubject.BaseType baseType => EntityText.TypeName(baseType.Value.Type),
            NodeSubject.DerivedTypes => "Derived Types",
            NodeSubject.DerivedType derived => EntityText.TypeName(derived.Value.Symbol),
            NodeSubject.Resources => "Resources",
            NodeSubject.ResourceContainer container => container.Value.Resource.Name,
            NodeSubject.ResourceEntry entry => entry.Value.Name,
            NodeSubject.Metadata metadata => metadata.Value.Title,
            NodeSubject.MetadataTables => "Tables",
            NodeSubject.MetadataTable table => table.Value.Title,
            NodeSubject.Package package => package.Value.Label,
            NodeSubject.PackageFile file => file.Value.File.Name,
            NodeSubject.Module module => GetModuleLabel(module.Value),
            NodeSubject.Namespace ns => ns.Value.Label,
            NodeSubject.FailedAssembly failed => failed.Value.FilePath.FileName,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Whether this node may have children, guessed without building them.</summary>
    internal static bool CanHaveChildren(BrowseNode node)
    {
        return node.Subject switch
        {
            NodeSubject.TypeDefinition type => CanListMembers(type.Definition),
            NodeSubject.MemberDefinition member => member.Definition.Symbol is IProperty or IEvent,
            NodeSubject.References references => HasReferences(references.Owner),
            NodeSubject.AssemblyReference => true,
            NodeSubject.ReferencedTypes types => ForwardedTypes.UsesReference(LoadedAssemblies.EnsureReferenceUsage(types.Reference.Module), types.Reference.Reference.Handle),
            NodeSubject.TypeReference type => UsesAnythingOf(type.Value),
            NodeSubject.ModuleReference module => module.Value.Imports.Count > 0,
            NodeSubject.BaseTypes or NodeSubject.DerivedTypes or NodeSubject.Resources or NodeSubject.ResourceContainer
                or NodeSubject.Metadata or NodeSubject.MetadataTables or NodeSubject.Package or NodeSubject.Module => true,
            NodeSubject.Namespace ns => !ns.Value.IsEnclosingOnly,
            NodeSubject.MetadataTable table => table.Value.Part is MetadataPart.DebugDirectory,
            NodeSubject.DerivedType derived => !derived.Value.Symbol.IsSealed,
            NodeSubject.MemberReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType or NodeSubject.BaseType
                or NodeSubject.ResourceEntry or NodeSubject.PackageFile or NodeSubject.FailedAssembly => false,
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Builds the children in ILSpy's order; the <see cref="BrowseSession"/> calls it once.</summary>
    internal static BrowseNode[] CreateChildren(BrowseNode parent)
    {
        return parent.Subject switch
        {
            NodeSubject.TypeDefinition type => ListMembers(parent, type.Definition),
            NodeSubject.MemberDefinition member => ListAccessors(parent, member.Definition),
            NodeSubject.References references => ListAssemblyReferences(references.Owner, parent).Concat(ListModuleReferences(references.Owner, parent)).ToArray(),
            NodeSubject.AssemblyReference reference => ListReferenceContents(parent, reference.Value),
            NodeSubject.ReferencedTypes types => ListUsedTypes(parent, types.Reference),
            NodeSubject.TypeReference type => ListUsedOf(parent, type.Value),
            NodeSubject.ModuleReference module => ListImports(parent, module.Value),
            NodeSubject.BaseTypes baseTypes => ListBaseTypes(parent, baseTypes.Type),
            NodeSubject.DerivedTypes derivedTypes => FindDerivedTypes(derivedTypes.Type.Module, parent, derivedTypes.Type.Symbol),
            NodeSubject.DerivedType derived => FindDerivedTypes(derived.Value.Module, parent, derived.Value.Symbol),
            NodeSubject.Resources resources => ListResources(parent, resources.Owner),
            NodeSubject.ResourceContainer container => ListEntries(parent, container.Value),
            NodeSubject.Metadata metadata => ListMetadata(parent, metadata.Value),
            NodeSubject.MetadataTables tables => ListTables(parent, tables.Folder),
            NodeSubject.MetadataTable table => table.Value.Part is MetadataPart.DebugDirectory directory ? ListDebugEntries(parent, table.Value.Module, directory.File) : [],
            NodeSubject.Package package => ListPackageFolder(parent, package.Value),
            NodeSubject.Module module => ListModule(parent, module.Value),
            NodeSubject.Namespace ns => ListNamespace(parent, ns.Value),
            NodeSubject.MemberReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType or NodeSubject.BaseType
                or NodeSubject.ResourceEntry or NodeSubject.PackageFile or NodeSubject.FailedAssembly => [],
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>The node of <paramref name="subject"/>, under <paramref name="parent"/>, which for a namespace
    /// may be one extending the name of <paramref name="enclosingNamespace"/> by a segment.</summary>
    internal static BrowseNode Create(BrowseNode? parent, NodeSubject subject, BrowseNode? enclosingNamespace = null)
    {
        return new BrowseNode
        {
            Assembly = GetAssembly(subject),
            Parent = parent,
            Text = GetText(subject),
            Subject = subject,
            EnclosingNamespace = enclosingNamespace,
        };
    }

    /// <summary>Where <paramref name="node"/> sits, for finding it again after its assembly is reloaded,
    /// worked out the first time. Two threads doing that at once work out the same path, of which one stays.</summary>
    public static NodePath GetPath(BrowseNode node)
    {
        return node.PathMemo ??= NodeDefinitions.GetPath(node);
    }

    /// <summary>What <paramref name="node"/> is among its siblings, as a path names it; null for the root of
    /// a tree. Worked out the first time, as <see cref="GetPath"/> is.</summary>
    internal static NodePathSegment? FindSegment(BrowseNode node)
    {
        return node.SegmentMemo ??= NodeDefinitions.FindPathSegment(node.Subject);
    }

    /// <summary>The bytes of the resource <paramref name="node"/> stands for, as a saved copy holds them: for
    /// BAML, the XAML it was compiled from.</summary>
    public static byte[] ReadResource(BrowseNode node)
    {
        return RequireResource(node).ReadData();
    }

    /// <summary>The name a saved copy of the resource <paramref name="node"/> stands for is offered under.</summary>
    public static string GetResourceFileName(BrowseNode node)
    {
        return RequireResource(node).FileName;
    }

    private static ResourceData RequireResource(BrowseNode node)
    {
        return FindResourceData(node) ?? throw new InvalidOperationException($"{node.Text} is no resource.");
    }

    /// <summary>The node of <paramref name="type"/>, under <paramref name="parent"/>; null for a type with
    /// no metadata token, which the tree does not list.</summary>
    internal static BrowseNode? CreateForType(LoadedModule module, BrowseNode parent, ITypeDefinition type)
    {
        var definition = NodeDefinitions.FindType(module, type);
        if (definition is null)
        {
            return null;
        }

        return Create(parent, new NodeSubject.TypeDefinition { Definition = definition });
    }

    /// <summary>The node of <paramref name="member"/>, under <paramref name="parent"/>; null for a member
    /// with no metadata token, which the tree does not list.</summary>
    internal static BrowseNode? CreateForMember(LoadedModule module, BrowseNode parent, IMember member)
    {
        var definition = NodeDefinitions.FindMember(module, member);
        if (definition is null)
        {
            return null;
        }

        return Create(parent, new NodeSubject.MemberDefinition { Definition = definition });
    }

    /// <summary>The type or member <paramref name="node"/> defines; null for a node of anything else.</summary>
    internal static IEntity? FindEntity(BrowseNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return node.Entity;
    }

    /// <summary>Whether <paramref name="node"/> defines a member, under which only its accessors sit.</summary>
    internal static bool IsMember(BrowseNode? node)
    {
        if (node is null)
        {
            return false;
        }

        return node.Subject is NodeSubject.MemberDefinition;
    }

    private static void AddIfListed(List<BrowseNode> nodes, BrowseNode? node)
    {
        if (node is not null)
        {
            nodes.Add(node);
        }
    }
}
