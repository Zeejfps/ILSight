using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.MetadataArea;
using ILSight.Core.ResourceArea;

namespace ILSight.Core;

public static class BrowseNodes
{
    private static LoadedAssembly GetAssembly(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.Module module => module.Value,
            NodeSubject.Namespace ns => ns.Value.Module,
            NodeSubject.FailedAssembly failed => failed.Value,
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
            NodeSubject.UnreadableModule unreadable => unreadable.Value.Module,
            NodeSubject.Resources resources => resources.Owner,
            NodeSubject.ResourceContainer container => container.Value.Module,
            NodeSubject.ResourceEntry entry => entry.Value.Module,
            NodeSubject.Metadata metadata => metadata.Value.Module,
            NodeSubject.MetadataTables tables => tables.Folder.Module,
            NodeSubject.MetadataTable table => table.Value.Module,
            NodeSubject.Package package => package.Value.Package,
            NodeSubject.PackageFile file => file.Value.Package,
        };
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
        return subject switch
        {
            NodeSubject.Module or NodeSubject.Namespace or NodeSubject.FailedAssembly or NodeSubject.TypeDefinition
                or NodeSubject.MemberDefinition or NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes
                or NodeSubject.TypeReference or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod
                or NodeSubject.ForwardedType or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes
                or NodeSubject.DerivedType or NodeSubject.UnreadableModule or NodeSubject.Resources or NodeSubject.Metadata
                or NodeSubject.MetadataTables or NodeSubject.MetadataTable or NodeSubject.Package => null,
            NodeSubject.ResourceContainer container => ResourceNodes.CreateData(container.Value),
            NodeSubject.ResourceEntry entry => ResourceNodes.CreateData(entry.Value),
            NodeSubject.PackageFile file => ResourceNodes.CreateData(file.Value),
        };
    }

    private static string ComposeFullName(NodeSubject subject, string text)
    {
        return subject switch
        {
            NodeSubject.Module module => module.Value.FullName,
            NodeSubject.Namespace ns => ns.Value.Name,
            NodeSubject.FailedAssembly failed => failed.Value.FilePath.Text,
            NodeSubject.TypeDefinition type => EntityText.FullName(NodeDefinitions.GetSymbol(type.Definition)),
            NodeSubject.MemberDefinition member => EntityText.FullName(NodeDefinitions.GetSymbol(member.Definition)),
            NodeSubject.References or NodeSubject.ReferencedTypes or NodeSubject.ModuleReference or NodeSubject.BaseTypes
                or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.UnreadableModule
                or NodeSubject.Resources or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata
                or NodeSubject.MetadataTables or NodeSubject.MetadataTable => text,
            NodeSubject.AssemblyReference reference => reference.Value.Reference.FullName,
            NodeSubject.TypeReference type => type.Value.FullName,
            NodeSubject.MemberReference member => member.Value.FullName,
            NodeSubject.ImportedMethod import => $"{import.Value.DeclaringTypeName}.{import.Value.Label}",
            NodeSubject.ForwardedType forwarded => forwarded.Value.FullName,
            NodeSubject.Package package => package.Value.FullName,
            NodeSubject.PackageFile file => file.Value.File.FullName,
        };
    }

    // The node's text: its identity among its siblings and in the decompiled summaries. For the folders the tree makes
    // and an unreadable module's row it is English here, and the app words what a person reads from the subject
    // (NodeText.Label: the folders by kind, the row as the module-unreadable cause's message, from its module and why).
    private static string GetText(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.Module module => BrowseRoots.GetModuleLabel(module.Value),
            NodeSubject.Namespace ns => ns.Value.Label,
            NodeSubject.FailedAssembly failed => failed.Value.FilePath.FileName,
            NodeSubject.TypeDefinition type => EntityText.Label(NodeDefinitions.GetSymbol(type.Definition)),
            NodeSubject.MemberDefinition member => EntityText.Label(NodeDefinitions.GetSymbol(member.Definition)),
            NodeSubject.References => "References",
            NodeSubject.AssemblyReference reference => reference.Value.Reference.Name,
            NodeSubject.ReferencedTypes => "Referenced types",
            NodeSubject.TypeReference type => type.Value.Label,
            NodeSubject.MemberReference member => EntityText.Label(NodeDefinitions.GetDeclared(member.Value)),
            NodeSubject.ModuleReference module => module.Value.Name,
            NodeSubject.ImportedMethod import => import.Value.Label,
            NodeSubject.ForwardedType forwarded => forwarded.Value.Label,
            NodeSubject.BaseTypes => "Base Types",
            NodeSubject.BaseType baseType => EntityText.TypeName(NodeDefinitions.GetSymbols(baseType.Value).Type),
            NodeSubject.DerivedTypes => "Derived Types",
            NodeSubject.DerivedType derived => EntityText.TypeName(NodeDefinitions.GetSymbol(derived.Value)),
            NodeSubject.UnreadableModule unreadable => $"{unreadable.Value.Module.FilePath.FileName}: couldn't be read ({unreadable.Value.Why.Message})",
            NodeSubject.Resources => "Resources",
            NodeSubject.ResourceContainer container => container.Value.Resource.Name,
            NodeSubject.ResourceEntry entry => entry.Value.Name,
            NodeSubject.Metadata metadata => metadata.Value.Title,
            NodeSubject.MetadataTables => "Tables",
            NodeSubject.MetadataTable table => table.Value.Title,
            NodeSubject.Package package => package.Value.Label,
            NodeSubject.PackageFile file => file.Value.File.Name,
        };
    }

    // A guess, made without building the children.
    private static bool CanHaveChildren(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.Module or NodeSubject.AssemblyReference or NodeSubject.BaseTypes or NodeSubject.DerivedTypes
                or NodeSubject.Resources or NodeSubject.ResourceContainer or NodeSubject.Metadata or NodeSubject.MetadataTables
                or NodeSubject.Package => true,
            NodeSubject.Namespace ns => !ns.Value.IsEnclosingOnly,
            NodeSubject.FailedAssembly or NodeSubject.MemberReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseType or NodeSubject.UnreadableModule or NodeSubject.ResourceEntry or NodeSubject.PackageFile => false,
            NodeSubject.TypeDefinition type => EntityNodes.CanListMembers(type.Definition),
            NodeSubject.MemberDefinition member => NodeDefinitions.GetSymbol(member.Definition) is IProperty or IEvent,
            NodeSubject.References references => ReferenceNodes.HasReferences(references.Owner),
            NodeSubject.ReferencedTypes types => ForwardedTypes.UsesReference(LoadedAssemblies.EnsureReferenceUsage(types.Reference.Module), types.Reference.Reference.Handle),
            NodeSubject.TypeReference type => ReferenceNodes.UsesAnythingOf(type.Value),
            NodeSubject.ModuleReference module => module.Value.Imports.Count > 0,
            NodeSubject.DerivedType derived => !NodeDefinitions.GetSymbol(derived.Value).IsSealed,
            NodeSubject.MetadataTable table => table.Value.Part is MetadataPart.DebugDirectory,
        };
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
        return parent.Subject switch
        {
            NodeSubject.Module module => ModuleNodes.ListModule(parent, module.Value, reads.RequireMain()),
            NodeSubject.Namespace ns => ModuleNodes.ListNamespace(parent, ns.Value),
            NodeSubject.FailedAssembly or NodeSubject.MemberReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseType or NodeSubject.UnreadableModule or NodeSubject.ResourceEntry or NodeSubject.PackageFile => [],
            NodeSubject.TypeDefinition type => EntityNodes.ListMembers(parent, type.Definition),
            NodeSubject.MemberDefinition member => EntityNodes.ListAccessors(parent, member.Definition),
            NodeSubject.References references => ReferenceNodes.ListAssemblyReferences(references.Owner, parent).Concat(ReferenceNodes.ListModuleReferences(references.Owner, parent)).ToArray(),
            NodeSubject.AssemblyReference reference => ReferenceNodes.ListReferenceContents(parent, reference.Value),
            NodeSubject.ReferencedTypes types => ReferenceNodes.ListUsedTypes(parent, types.Reference),
            NodeSubject.TypeReference type => ReferenceNodes.ListUsedOf(parent, type.Value, reads.RequireMain()),
            NodeSubject.ModuleReference module => ReferenceNodes.ListImports(parent, module.Value, reads.RequireMain()),
            NodeSubject.BaseTypes baseTypes => EntityNodes.ListBaseTypes(parent, baseTypes.Type),
            NodeSubject.DerivedTypes derivedTypes => EntityNodes.FindDerivedTypes(derivedTypes.Type.Module, parent, NodeDefinitions.GetSymbol(derivedTypes.Type), reads),
            NodeSubject.DerivedType derived => EntityNodes.FindDerivedTypes(derived.Value.Module, parent, NodeDefinitions.GetSymbol(derived.Value), reads),
            NodeSubject.Resources resources => ResourceNodes.ListResources(parent, resources.Owner),
            NodeSubject.ResourceContainer container => ResourceNodes.ListEntries(parent, container.Value),
            NodeSubject.Metadata metadata => MetadataNodes.ListMetadata(parent, metadata.Value),
            NodeSubject.MetadataTables tables => MetadataTableNodes.List(parent, tables.Folder),
            NodeSubject.MetadataTable table => table.Value.Part is MetadataPart.DebugDirectory directory ? MetadataNodes.ListDebugEntries(parent, table.Value.Module, directory.File) : [],
            NodeSubject.Package package => PackageNodes.ListPackageFolder(parent, package.Value),
        };
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
