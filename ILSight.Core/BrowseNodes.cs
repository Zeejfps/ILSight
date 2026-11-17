using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;

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

    // Decompiled code changes with the language and options; a grid, a picture or why the file failed to load do not.
    private static bool IsCode(NodeSubject subject)
    {
        return !HasGrid(subject) && subject is not NodeSubject.FailedAssembly && !IsImage(subject);
    }

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

    // The node's text: its identity among its siblings and in the decompiled summaries. For the folders the tree makes
    // and an unreadable module's row it is English here, and the app words what a person reads from the subject
    // (NodeText.Label: the folders by kind, the row as the module-unreadable cause's message, from its module and why).
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

    // A guess, made without building the children.
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
    /// it began; the <see cref="BrowseSession"/> calls it once. Each read of the library is under a catch of its own
    /// (<see cref="ListingReads"/>), the app's rules over what was read between them: what the library throws ends the
    /// listing as a <see cref="ListingReadException"/>.</summary>
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
    /// extending the name of <paramref name="enclosingNamespace"/> by a segment: its facts read
    /// (<see cref="ReadFacts"/>), then the node made of them (<see cref="Make"/>). For a node made outside a listing (a
    /// tree's root); a listing's child is <see cref="CreateListed"/>.</summary>
    internal static BrowseNode Create(BrowseNode? parent, NodeSubject subject, BrowseNode? enclosingNamespace = null)
    {
        var facts = ReadFacts(subject);
        return Make(parent, subject, facts, enclosingNamespace);
    }

    /// <summary>As <see cref="Create"/>, for a child of a listing: its facts read under a catch of their own
    /// (<see cref="ListingReads"/>), the node made after it.</summary>
    internal static BrowseNode CreateListed(BrowseNode parent, NodeSubject subject, BrowseNode? enclosingNamespace = null)
    {
        var facts = ListingReads.ValueOf(ReadListedFacts(subject));
        return Make(parent, subject, facts, enclosingNamespace);
    }

    // What a node shows of what it stands for, read from its subject: the values of BrowseNode of the same names, before
    // the node is made.
    private sealed record NodeFacts
    {
        public required string Text { get; init; }

        public required string FullName { get; init; }

        // The segment naming the node among its siblings; a tree's root keeps none.
        public required NodePathSegment? Segment { get; init; }

        public required NodeVisibility Visibility { get; init; }

        public required CodeReference.Entity? Reference { get; init; }

        public required Activation Activation { get; init; }

        public required AnalysisArea.AnalysisFacts? AnalysisFacts { get; init; }

        public required bool CanHaveChildren { get; init; }

        public required string Name { get; init; }

        public required string? ResourceFileName { get; init; }

        public required string? ILSpyName { get; init; }

        public required string? EntityFullName { get; init; }
    }

    private static Outcome<NodeFacts> ReadListedFacts(NodeSubject subject)
    {
        try
        {
            return new Outcome<NodeFacts>.Ok(ReadFacts(subject));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<NodeFacts>.Failed(e);
        }
    }

    // Only reads: the full name and the visibility are the library's values (its formatter, the flags a definition
    // read), the activation the reference a read took out, each picked by the subject's case; what analyses the node
    // offers, the app's table, is decided when first asked for (BrowseNode.Analysis).
    private static NodeFacts ReadFacts(NodeSubject subject)
    {
        var text = GetText(subject);
        return new NodeFacts
        {
            Text = text,
            FullName = ComposeFullName(subject, text),
            Segment = NodeDefinitions.FindPathSegment(subject),
            Visibility = NodeDefinitions.GetVisibility(subject),
            Reference = NodeDefinitions.ReferenceOf(subject),
            Activation = NodeDefinitions.GetActivation(subject),
            AnalysisFacts = AnalysisArea.Analyzers.Of(subject),
            CanHaveChildren = CanHaveChildren(subject),
            Name = GetName(subject, GetAssembly(subject), text),
            ResourceFileName = FindResourceData(subject)?.FileName,
            ILSpyName = ILSpyTree.NameOf(subject),
            EntityFullName = FindEntityFullName(subject),
        };
    }

    // The node made of facts read: its place in the tree (the path, the app's), and whether it shows code.
    private static BrowseNode Make(BrowseNode? parent, NodeSubject subject, NodeFacts facts, BrowseNode? enclosingNamespace)
    {
        var assembly = GetAssembly(subject);
        var segment = parent is null ? null : facts.Segment;
        return new BrowseNode
        {
            Assembly = assembly,
            Parent = parent,
            Text = facts.Text,
            Subject = subject,
            EnclosingNamespace = enclosingNamespace,
            Segment = segment,
            Path = NodeDefinitions.ExtendPath(parent, assembly, segment, facts.Text),
            FullName = facts.FullName,
            Visibility = facts.Visibility,
            Reference = facts.Reference,
            Activation = facts.Activation,
            AnalysisFacts = facts.AnalysisFacts,
            CanHaveChildren = facts.CanHaveChildren,
            Name = facts.Name,
            IsCode = IsCode(subject),
            ResourceFileName = facts.ResourceFileName,
            ILSpyName = facts.ILSpyName,
            EntityFullName = facts.EntityFullName,
        };
    }

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

    /// <summary>The resource <paramref name="node"/> stands for; the app's own failure when it stands for none.</summary>
    internal static ResourceData RequireResource(BrowseNode node)
    {
        return FindResourceData(node.Subject) ?? throw new InvalidOperationException($"{node.Text} is no resource.");
    }

    /// <summary>The node of <paramref name="type"/>, a child of <paramref name="parent"/>'s listing, its definition and
    /// facts read under the listing's catches; null for a type with no metadata token, which the tree does not
    /// list.</summary>
    internal static BrowseNode? CreateListedType(LoadedModule module, BrowseNode parent, ITypeDefinition type)
    {
        var definition = ListingReads.ValueOf(ReadDefinedType(module, type));
        if (definition is null)
        {
            return null;
        }

        return CreateListed(parent, new NodeSubject.TypeDefinition { Definition = definition });
    }

    /// <summary>The node of <paramref name="member"/>, a child of <paramref name="parent"/>'s listing, as
    /// <see cref="CreateListedType"/>; null for a member with no metadata token, which the tree does not list.</summary>
    internal static BrowseNode? CreateListedMember(LoadedModule module, BrowseNode parent, IMember member)
    {
        var definition = ListingReads.ValueOf(ReadDefinedMember(module, member));
        if (definition is null)
        {
            return null;
        }

        return CreateListed(parent, new NodeSubject.MemberDefinition { Definition = definition });
    }

    private static Outcome<DefinedType?> ReadDefinedType(LoadedModule module, ITypeDefinition type)
    {
        try
        {
            return new Outcome<DefinedType?>.Ok(NodeDefinitions.FindType(module, type));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DefinedType?>.Failed(e);
        }
    }

    private static Outcome<DefinedMember?> ReadDefinedMember(LoadedModule module, IMember member)
    {
        try
        {
            return new Outcome<DefinedMember?>.Ok(NodeDefinitions.FindMember(module, member));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DefinedMember?>.Failed(e);
        }
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
