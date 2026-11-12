using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Metadata;
using ILSight.Core.Resources;

namespace ILSight.Core;

public static class BrowseNodes
{
    private static LoadedAssembly GetAssembly(NodeSubject subject)
    {
        return subject.Match<LoadedAssembly>(
            module: static module => module.Value,
            @namespace: static ns => ns.Value.Module,
            failedAssembly: static failed => failed.Value,
            typeDefinition: static type => type.Definition.Module,
            memberDefinition: static member => member.Definition.Module,
            references: static references => references.Owner,
            assemblyReference: static reference => reference.Value.Module,
            referencedTypes: static types => types.Reference.Module,
            typeReference: static type => type.Value.Module,
            memberReference: static member => member.Value.Module,
            moduleReference: static module => module.Value.Module,
            importedMethod: static import => import.Value.Shown.Module,
            forwardedType: static forwarded => forwarded.Value.Module,
            baseTypes: static baseTypes => baseTypes.Type.Module,
            baseType: static baseType => baseType.Value.Module,
            derivedTypes: static derivedTypes => derivedTypes.Type.Module,
            derivedType: static derived => derived.Value.Module,
            unreadableModule: static unreadable => unreadable.Value.Module,
            resources: static resources => resources.Owner,
            resourceContainer: static container => container.Value.Module,
            resourceEntry: static entry => entry.Value.Module,
            metadata: static metadata => metadata.Value.Module,
            metadataTables: static tables => tables.Folder.Module,
            metadataTable: static table => table.Value.Module,
            package: static package => package.Value.Package,
            packageFile: static file => file.Value.Package);
    }

    /// <summary>The name of what a node of <paramref name="subject"/>, read from <paramref name="assembly"/> and
    /// labelled <paramref name="text"/>, stands for: a module's assembly's, a type's or member's own, else the
    /// label.</summary>
    private static string GetName(NodeSubject subject, LoadedAssembly assembly, string text)
    {
        if (subject is NodeSubject.Module)
        {
            return assembly.Name;
        }

        if (subject is NodeSubject.TypeDefinition type)
        {
            return NodeDefinitions.GetSymbol(type.Definition).Name;
        }

        if (subject is NodeSubject.MemberDefinition member)
        {
            return NodeDefinitions.GetSymbol(member.Definition).Name;
        }

        return text;
    }

    /// <summary>Whether a node of <paramref name="subject"/> shows decompiled code (<see cref="NodeContentLoader.GetContentAsync"/>),
    /// which changes with the language and options, rather than a grid, a picture or why the file failed to load,
    /// which do not. Told by what the subject is, reading nothing.</summary>
    private static bool IsCode(NodeSubject subject)
    {
        return !HasGrid(subject) && subject is not NodeSubject.FailedAssembly && !IsImage(subject);
    }

    /// <summary>Whether a node of <paramref name="subject"/> shows a grid: a metadata header, directory, table or
    /// heap's, or a <c>.resources</c> file's entries.</summary>
    private static bool HasGrid(NodeSubject subject)
    {
        if (subject is NodeSubject.MetadataTable table)
        {
            return table.Value.Part is MetadataPart.Grid or MetadataPart.DebugDirectory;
        }

        if (subject is NodeSubject.ResourceContainer container)
        {
            return container.Value.Contents is ContainerContents.ResourcesFile;
        }

        return false;
    }

    /// <summary>Whether a node of <paramref name="subject"/> stands for a picture.</summary>
    internal static bool IsImage(NodeSubject subject)
    {
        if (subject is NodeSubject.ResourceEntry entry)
        {
            return entry.Value.IsImage;
        }

        if (subject is NodeSubject.PackageFile file)
        {
            return file.Value.IsImage;
        }

        return false;
    }

    /// <summary>The bytes of the resource a node of <paramref name="subject"/> stands for; null for one of anything else.</summary>
    internal static ResourceData? FindResourceData(NodeSubject subject)
    {
        return subject.Match<ResourceData?>(
            module: static _ => null,
            @namespace: static _ => null,
            failedAssembly: static _ => null,
            typeDefinition: static _ => null,
            memberDefinition: static _ => null,
            references: static _ => null,
            assemblyReference: static _ => null,
            referencedTypes: static _ => null,
            typeReference: static _ => null,
            memberReference: static _ => null,
            moduleReference: static _ => null,
            importedMethod: static _ => null,
            forwardedType: static _ => null,
            baseTypes: static _ => null,
            baseType: static _ => null,
            derivedTypes: static _ => null,
            derivedType: static _ => null,
            unreadableModule: static _ => null,
            resources: static _ => null,
            resourceContainer: static container => ResourceNodes.CreateData(container.Value),
            resourceEntry: static entry => ResourceNodes.CreateData(entry.Value),
            metadata: static _ => null,
            metadataTables: static _ => null,
            metadataTable: static _ => null,
            package: static _ => null,
            packageFile: static file => ResourceNodes.CreateData(file.Value));
    }

    /// <summary>A qualified name for copying a node of <paramref name="subject"/>, labelled <paramref name="text"/>,
    /// e.g. <c>System.Collections.Generic.List&lt;T&gt;.Add(T)</c>.</summary>
    private static string ComposeFullName(NodeSubject subject, string text)
    {
        return subject.Match(text,
            module: static (_, module) => module.Value.FullName,
            @namespace: static (_, ns) => ns.Value.Name,
            failedAssembly: static (_, failed) => failed.Value.FilePath.Text,
            typeDefinition: static (_, type) => EntityText.FullName(NodeDefinitions.GetSymbol(type.Definition)),
            memberDefinition: static (_, member) => EntityText.FullName(NodeDefinitions.GetSymbol(member.Definition)),
            references: static (label, _) => label,
            assemblyReference: static (_, reference) => reference.Value.Reference.FullName,
            referencedTypes: static (label, _) => label,
            typeReference: static (_, type) => type.Value.FullName,
            memberReference: static (_, member) => member.Value.FullName,
            moduleReference: static (label, _) => label,
            importedMethod: static (_, import) => $"{import.Value.DeclaringTypeName}.{import.Value.Label}",
            forwardedType: static (_, forwarded) => forwarded.Value.FullName,
            baseTypes: static (label, _) => label,
            baseType: static (label, _) => label,
            derivedTypes: static (label, _) => label,
            derivedType: static (label, _) => label,
            unreadableModule: static (label, _) => label,
            resources: static (label, _) => label,
            resourceContainer: static (label, _) => label,
            resourceEntry: static (label, _) => label,
            metadata: static (label, _) => label,
            metadataTables: static (label, _) => label,
            metadataTable: static (label, _) => label,
            package: static (_, package) => package.Value.FullName,
            packageFile: static (_, file) => file.Value.File.FullName);
    }

    private static string GetText(NodeSubject subject)
    {
        return subject.Match(
            module: static module => BrowseRoots.GetModuleLabel(module.Value),
            @namespace: static ns => ns.Value.Label,
            failedAssembly: static failed => failed.Value.FilePath.FileName,
            typeDefinition: static type => EntityText.Label(NodeDefinitions.GetSymbol(type.Definition)),
            memberDefinition: static member => EntityText.Label(NodeDefinitions.GetSymbol(member.Definition)),
            references: static _ => "References",
            assemblyReference: static reference => reference.Value.Reference.Name,
            referencedTypes: static _ => "Referenced types",
            typeReference: static type => type.Value.Label,
            memberReference: static member => EntityText.Label(NodeDefinitions.GetDeclared(member.Value)),
            moduleReference: static module => module.Value.Name,
            importedMethod: static import => import.Value.Label,
            forwardedType: static forwarded => forwarded.Value.Label,
            baseTypes: static _ => "Base Types",
            baseType: static baseType => EntityText.TypeName(NodeDefinitions.GetSymbols(baseType.Value).Type),
            derivedTypes: static _ => "Derived Types",
            derivedType: static derived => EntityText.TypeName(NodeDefinitions.GetSymbol(derived.Value)),
            unreadableModule: static unreadable => $"{unreadable.Value.Module.FilePath.FileName}: couldn't be read ({unreadable.Value.Why.Message})",
            resources: static _ => "Resources",
            resourceContainer: static container => container.Value.Resource.Name,
            resourceEntry: static entry => entry.Value.Name,
            metadata: static metadata => metadata.Value.Title,
            metadataTables: static _ => "Tables",
            metadataTable: static table => table.Value.Title,
            package: static package => package.Value.Label,
            packageFile: static file => file.Value.File.Name);
    }

    /// <summary>Whether a node of <paramref name="subject"/> may have children, guessed without building them.</summary>
    private static bool CanHaveChildren(NodeSubject subject)
    {
        return subject.Match(
            module: static _ => true,
            @namespace: static ns => !ns.Value.IsEnclosingOnly,
            failedAssembly: static _ => false,
            typeDefinition: static type => EntityNodes.CanListMembers(type.Definition),
            memberDefinition: static member => NodeDefinitions.GetSymbol(member.Definition) is IProperty or IEvent,
            references: static references => ReferenceNodes.HasReferences(references.Owner),
            assemblyReference: static _ => true,
            referencedTypes: static types => ForwardedTypes.UsesReference(LoadedAssemblies.EnsureReferenceUsage(types.Reference.Module), types.Reference.Reference.Handle),
            typeReference: static type => ReferenceNodes.UsesAnythingOf(type.Value),
            memberReference: static _ => false,
            moduleReference: static module => module.Value.Imports.Count > 0,
            importedMethod: static _ => false,
            forwardedType: static _ => false,
            baseTypes: static _ => true,
            baseType: static _ => false,
            derivedTypes: static _ => true,
            derivedType: static derived => !NodeDefinitions.GetSymbol(derived.Value).IsSealed,
            unreadableModule: static _ => false,
            resources: static _ => true,
            resourceContainer: static _ => true,
            resourceEntry: static _ => false,
            metadata: static _ => true,
            metadataTables: static _ => true,
            metadataTable: static table => table.Value.Part is MetadataPart.DebugDirectory,
            package: static _ => true,
            packageFile: static _ => false);
    }

    /// <summary>The module whose type system building the children of a node of <paramref name="subject"/> reads: a
    /// module's own, a type reference's or a module reference's module; null for any other.</summary>
    internal static LoadedModule? FindModuleReadForChildren(NodeSubject subject)
    {
        if (subject is NodeSubject.Module module)
        {
            return module.Value;
        }

        if (subject is NodeSubject.TypeReference type)
        {
            return type.Value.Module;
        }

        if (subject is NodeSubject.ModuleReference reference)
        {
            return reference.Value.Module;
        }

        return null;
    }

    /// <summary>The module of the type whose derived types are the children of a node of <paramref name="subject"/>;
    /// null for any other.</summary>
    internal static LoadedModule? FindDerivedTypesModule(NodeSubject subject)
    {
        if (subject is NodeSubject.DerivedTypes derivedTypes)
        {
            return derivedTypes.Type.Module;
        }

        if (subject is NodeSubject.DerivedType derived)
        {
            return derived.Value.Module;
        }

        return null;
    }

    /// <summary>Builds the children in ILSpy's order, with <paramref name="reads"/>, what the build reads, awaited before
    /// it began; the <see cref="BrowseSession"/> calls it once.</summary>
    internal static BrowseNode[] CreateChildren(BrowseNode parent, ChildrenReads reads)
    {
        return parent.Subject.Match((parent, reads),
            module: static (s, module) => ModuleNodes.ListModule(s.parent, module.Value, s.reads.RequireMain()),
            @namespace: static (s, ns) => ModuleNodes.ListNamespace(s.parent, ns.Value),
            failedAssembly: static (_, _) => [],
            typeDefinition: static (s, type) => EntityNodes.ListMembers(s.parent, type.Definition),
            memberDefinition: static (s, member) => EntityNodes.ListAccessors(s.parent, member.Definition),
            references: static (s, references) => ReferenceNodes.ListAssemblyReferences(references.Owner, s.parent).Concat(ReferenceNodes.ListModuleReferences(references.Owner, s.parent)).ToArray(),
            assemblyReference: static (s, reference) => ReferenceNodes.ListReferenceContents(s.parent, reference.Value),
            referencedTypes: static (s, types) => ReferenceNodes.ListUsedTypes(s.parent, types.Reference),
            typeReference: static (s, type) => ReferenceNodes.ListUsedOf(s.parent, type.Value, s.reads.RequireMain()),
            memberReference: static (_, _) => [],
            moduleReference: static (s, module) => ReferenceNodes.ListImports(s.parent, module.Value, s.reads.RequireMain()),
            importedMethod: static (_, _) => [],
            forwardedType: static (_, _) => [],
            baseTypes: static (s, baseTypes) => EntityNodes.ListBaseTypes(s.parent, baseTypes.Type),
            baseType: static (_, _) => [],
            derivedTypes: static (s, derivedTypes) => EntityNodes.FindDerivedTypes(derivedTypes.Type.Module, s.parent, NodeDefinitions.GetSymbol(derivedTypes.Type), s.reads),
            derivedType: static (s, derived) => EntityNodes.FindDerivedTypes(derived.Value.Module, s.parent, NodeDefinitions.GetSymbol(derived.Value), s.reads),
            unreadableModule: static (_, _) => [],
            resources: static (s, resources) => ResourceNodes.ListResources(s.parent, resources.Owner),
            resourceContainer: static (s, container) => ResourceNodes.ListEntries(s.parent, container.Value),
            resourceEntry: static (_, _) => [],
            metadata: static (s, metadata) => MetadataNodes.ListMetadata(s.parent, metadata.Value),
            metadataTables: static (s, tables) => MetadataTableNodes.List(s.parent, tables.Folder),
            metadataTable: static (s, table) => table.Value.Part is MetadataPart.DebugDirectory directory ? MetadataNodes.ListDebugEntries(s.parent, table.Value.Module, directory.File) : [],
            package: static (s, package) => PackageNodes.ListPackageFolder(s.parent, package.Value),
            packageFile: static (_, _) => []);
    }

    /// <summary>The node of <paramref name="subject"/>, under <paramref name="parent"/>, which for a namespace may be one
    /// extending the name of <paramref name="enclosingNamespace"/> by a segment, with the values it shows (its full
    /// name, visibility, definition and activation) read now from what the subject stands for.</summary>
    internal static BrowseNode Create(BrowseNode? parent, NodeSubject subject, BrowseNode? enclosingNamespace = null)
    {
        var text = GetText(subject);
        var assembly = GetAssembly(subject);
        var segment = parent is null ? null : NodeDefinitions.FindPathSegment(subject);
        return new BrowseNode
        {
            Assembly = assembly,
            Parent = parent,
            Text = text,
            Subject = subject,
            EnclosingNamespace = enclosingNamespace,
            Segment = segment,
            Path = NodeDefinitions.ExtendPath(parent, assembly, segment, text),
            FullName = ComposeFullName(subject, text),
            Visibility = NodeDefinitions.GetVisibility(subject),
            Reference = NodeDefinitions.ReferenceOf(subject),
            Activation = NodeDefinitions.GetActivation(subject),
            Analysis = Analysis.Analyzers.Of(subject),
            CanHaveChildren = CanHaveChildren(subject),
            Name = GetName(subject, assembly, text),
            IsCode = IsCode(subject),
            ResourceFileName = FindResourceData(subject)?.FileName,
            ILSpyName = ILSpyTree.NameOf(subject),
            EntityFullName = FindEntityFullName(subject),
        };
    }

    /// <summary>The ILSpy full name of the type or member a node of <paramref name="subject"/> defines; null for one
    /// that defines none.</summary>
    private static string? FindEntityFullName(NodeSubject subject)
    {
        if (subject is NodeSubject.TypeDefinition type)
        {
            return NodeDefinitions.GetSymbol(type.Definition).FullName;
        }

        if (subject is NodeSubject.MemberDefinition member)
        {
            return NodeDefinitions.GetSymbol(member.Definition).FullName;
        }

        return null;
    }

    /// <summary>The bytes of the resource <paramref name="node"/> stands for, as a saved copy holds them: for
    /// BAML, the XAML it was compiled from.</summary>
    public static byte[] ReadResource(BrowseNode node)
    {
        return RequireResource(node).ReadData();
    }

    private static ResourceData RequireResource(BrowseNode node)
    {
        return FindResourceData(node.Subject) ?? throw new InvalidOperationException($"{node.Text} is no resource.");
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
}
