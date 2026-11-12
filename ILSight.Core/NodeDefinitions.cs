using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.TypeSystem;
using ILSight.Core.Resources;

namespace ILSight.Core;

/// <summary>What a node stands for and what that makes it: the definition of a type or member, as documents
/// and metadata name it, how visible it is, what activating it does, and how a path names it.</summary>
public static class NodeDefinitions
{
    private static readonly Activation.OpenSelf Open = new();

    /// <summary>The base type <paramref name="type"/> as <paramref name="module"/> names it, navigable when
    /// its definition is found and can be referenced.</summary>
    internal static BaseTypeInfo CreateBaseType(LoadedModule module, IType type)
    {
        var definition = type.GetDefinition();
        if (definition is null)
        {
            return KeepBase(new BaseTypeInfo { Module = module, Target = new BaseTypeTarget.Unresolved(), Visibility = NodeVisibility.Open }, type, null);
        }

        var reference = CodeReferences.Find(definition);
        if (reference == null)
        {
            return KeepBase(new BaseTypeInfo { Module = module, Target = new BaseTypeTarget.Unaddressed(), Visibility = NodeVisibility.Open }, type, definition);
        }

        var info = new BaseTypeInfo { Module = module, Target = new BaseTypeTarget.Navigable(reference), Visibility = GetReferencedVisibility(definition) };
        return KeepBase(info, type, definition);
    }

    /// <summary><paramref name="type"/> as <paramref name="module"/> forwards it.</summary>
    internal static ForwardedTypeInfo CreateForwardedType(LoadedModule module, ForwardedType type)
    {
        return new ForwardedTypeInfo
        {
            Module = module,
            Handle = type.Handle,
            Namespace = type.Namespace,
            Label = type.Label,
            SortName = type.SortName,
            Arity = type.Arity,
            Destination = type.Destination,
        };
    }

    /// <summary>The namespace <paramref name="name"/> of <paramref name="module"/>, holding <paramref name="types"/>
    /// and forwarding <paramref name="forwarded"/>.</summary>
    internal static NamespaceInfo CreateNamespace(LoadedModule module, string name, IReadOnlyList<ITypeDefinition> types, IReadOnlyList<ForwardedTypeInfo> forwarded)
    {
        var info = new NamespaceInfo
        {
            Module = module,
            Name = name,
            TypeCount = types.Count,
            Forwarded = forwarded,
            IsPublicApi = forwarded.Count > 0 || types.Any(EntityText.IsPublicApi),
            IsCompilerGenerated = forwarded.Count == 0 && types.All(Members.IsCompilerGenerated),
        };
        return LibraryObjects<NamespaceInfo, IReadOnlyList<ITypeDefinition>>.Keep(info, types);
    }

    /// <summary>The types <paramref name="ns"/> holds itself, kept against it when it was made.</summary>
    internal static IReadOnlyList<ITypeDefinition> GetTypes(NamespaceInfo ns)
    {
        return LibraryObjects<NamespaceInfo, IReadOnlyList<ITypeDefinition>>.Get(ns);
    }

    /// <summary>The library's type <paramref name="type"/> stands for, kept against it when it was made.</summary>
    internal static ITypeDefinition GetSymbol(DefinedType type)
    {
        return LibraryObjects<DefinedType, ITypeDefinition>.Get(type);
    }

    /// <summary>The library's member <paramref name="member"/> stands for, kept against it when it was made.</summary>
    internal static IMember GetSymbol(DefinedMember member)
    {
        return LibraryObjects<DefinedMember, IMember>.Get(member);
    }

    /// <summary>The library's derived type <paramref name="type"/> stands for, kept against it when it was made.</summary>
    internal static ITypeDefinition GetSymbol(DerivedTypeInfo type)
    {
        return LibraryObjects<DerivedTypeInfo, ITypeDefinition>.Get(type);
    }

    /// <summary>The library's member <paramref name="member"/> stands for, as its type declares it, kept against it
    /// when it was made.</summary>
    internal static IMember GetDeclared(MemberReferenceInfo member)
    {
        return LibraryObjects<MemberReferenceInfo, IMember>.Get(member);
    }

    /// <summary>The library's type and definition <paramref name="baseType"/> stands for, kept against it when it
    /// was made.</summary>
    internal static BaseTypeSymbols GetSymbols(BaseTypeInfo baseType)
    {
        return LibraryObjects<BaseTypeInfo, BaseTypeSymbols>.Get(baseType);
    }

    private static BaseTypeInfo KeepBase(BaseTypeInfo info, IType type, ITypeDefinition? definition)
    {
        return LibraryObjects<BaseTypeInfo, BaseTypeSymbols>.Keep(info, new BaseTypeSymbols { Type = type, Definition = definition });
    }

    /// <summary>The derived type <paramref name="type"/>; null when it has no reference to be navigated to.</summary>
    internal static DerivedTypeInfo? FindDerivedType(LoadedModule module, ITypeDefinition type)
    {
        var reference = CodeReferences.Find(type);
        if (reference == null)
        {
            return null;
        }

        var info = new DerivedTypeInfo { Module = module, Reference = reference, Visibility = GetReferencedVisibility(type) };
        return LibraryObjects<DerivedTypeInfo, ITypeDefinition>.Keep(info, type);
    }

    /// <summary>The definition of <paramref name="type"/> in <paramref name="module"/>; null when it has no
    /// metadata file or token, as a type the decompiler made up has not.</summary>
    internal static DefinedType? FindType(LoadedModule module, ITypeDefinition type)
    {
        if (type.MetadataToken.Kind != HandleKind.TypeDefinition)
        {
            return null;
        }

        var reference = CodeReferences.Find(type);
        if (reference == null)
        {
            return null;
        }

        var defined = new DefinedType
        {
            Module = module, Handle = (TypeDefinitionHandle)type.MetadataToken, Reference = reference, Visibility = GetDefinitionVisibility(type),
            Name = type.Name, Arity = type.TypeParameterCount, Shape = EntityText.ShapeOf(type),
        };
        return LibraryObjects<DefinedType, ITypeDefinition>.Keep(defined, type);
    }

    /// <summary>The definition of <paramref name="member"/> in <paramref name="module"/>; null when it has
    /// no metadata file or token.</summary>
    internal static DefinedMember? FindMember(LoadedModule module, IMember member)
    {
        var reference = CodeReferences.Find(member);
        if (reference == null)
        {
            return null;
        }

        var defined = new DefinedMember { Module = module, Reference = reference, Visibility = GetDefinitionVisibility(member), Shape = EntityText.ShapeOf(member) };
        return LibraryObjects<DefinedMember, IMember>.Keep(defined, member);
    }

    /// <summary>A reference to the type or member a node of <paramref name="subject"/> defines, as documents
    /// point at it; null for a node that defines none.</summary>
    public static CodeReference.Entity? ReferenceOf(NodeSubject subject)
    {
        return subject.Match<CodeReference.Entity?>(
            module: static _ => null,
            @namespace: static _ => null,
            failedAssembly: static _ => null,
            typeDefinition: static type => type.Definition.Reference,
            memberDefinition: static member => member.Definition.Reference,
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
            resources: static _ => null,
            resourceContainer: static _ => null,
            resourceEntry: static _ => null,
            metadata: static _ => null,
            metadataTables: static _ => null,
            metadataTable: static _ => null,
            package: static _ => null,
            packageFile: static _ => null);
    }

    /// <summary>The metadata token of the type or member a node of <paramref name="subject"/> defines;
    /// null for a node that defines none.</summary>
    public static int? TokenOf(NodeSubject subject)
    {
        var reference = ReferenceOf(subject);
        if (reference == null)
        {
            return null;
        }

        return MetadataTokens.GetToken(reference.Handle);
    }

    /// <summary>How visible a definition is, compiler-generated when the compiler made it up or what holds it.</summary>
    internal static NodeVisibility GetDefinitionVisibility(IEntity entity)
    {
        return new NodeVisibility
        {
            Accessibility = EntityText.Map(entity.Accessibility),
            IsPublicApi = EntityText.IsPublicApi(entity),
            IsCompilerGenerated = Members.IsOrSitsInCompilerGenerated(entity),
        };
    }

    /// <summary>How visible a type another node points at is: as declared, and never compiler-generated.</summary>
    internal static NodeVisibility GetReferencedVisibility(IEntity entity)
    {
        return new NodeVisibility
        {
            Accessibility = EntityText.Map(entity.Accessibility),
            IsPublicApi = EntityText.IsPublicApi(entity),
            IsCompilerGenerated = false,
        };
    }

    /// <summary>How visible what a node of <paramref name="subject"/> stands for is.</summary>
    public static NodeVisibility GetVisibility(NodeSubject subject)
    {
        return subject.Match(
            module: static _ => NodeVisibility.Open,
            @namespace: static ns => new NodeVisibility
            {
                Accessibility = NodeAccessibility.None,
                IsPublicApi = ns.Value.IsPublicApi,
                IsCompilerGenerated = ns.Value.IsCompilerGenerated,
            },
            failedAssembly: static _ => NodeVisibility.Open,
            typeDefinition: static type => type.Definition.Visibility,
            memberDefinition: static member => member.Definition.Visibility,
            references: static _ => NodeVisibility.Open,
            assemblyReference: static _ => NodeVisibility.Open,
            referencedTypes: static _ => NodeVisibility.Open,
            typeReference: static _ => NodeVisibility.Open,
            memberReference: static _ => NodeVisibility.Open,
            moduleReference: static _ => NodeVisibility.Open,
            importedMethod: static import => import.Value.Shown.Visibility with { IsCompilerGenerated = false },
            forwardedType: static _ => NodeVisibility.Open,
            baseTypes: static _ => NodeVisibility.Open,
            baseType: static baseType => baseType.Value.Visibility,
            derivedTypes: static _ => NodeVisibility.Open,
            derivedType: static derived => derived.Value.Visibility,
            resources: static _ => NodeVisibility.Open,
            resourceContainer: static container => GetResourceVisibility(container.Value.Resource),
            resourceEntry: static entry => entry.Value.Source.Match(
                static manifest => GetResourceVisibility(manifest.Resource),
                static _ => NodeVisibility.Open,
                static _ => NodeVisibility.Open),
            metadata: static _ => NodeVisibility.Open,
            metadataTables: static _ => NodeVisibility.Open,
            metadataTable: static _ => NodeVisibility.Open,
            package: static _ => NodeVisibility.Open,
            packageFile: static _ => NodeVisibility.Open);
    }

    private static NodeVisibility GetResourceVisibility(ICSharpCode.Decompiler.Metadata.Resource resource)
    {
        var (isPublic, accessibility) = ResourceNodes.Visibility(resource);
        return new NodeVisibility
        {
            Accessibility = accessibility,
            IsPublicApi = isPublic,
            IsCompilerGenerated = false,
        };
    }

    /// <summary>What activating a node of <paramref name="subject"/> does.</summary>
    public static Activation GetActivation(NodeSubject subject)
    {
        return subject.Match<Activation>(
            module: static _ => Open,
            @namespace: static _ => Open,
            failedAssembly: static _ => Open,
            typeDefinition: static _ => Open,
            memberDefinition: static _ => Open,
            references: static _ => Open,
            assemblyReference: static reference => new Activation.Navigate(CodeReferences.CreateAssembly(reference.Value.Reference.Name,
                reference.Value.Module.FilePath, reference.Value.Reference.Handle)),
            referencedTypes: static _ => Open,
            typeReference: static type => new Activation.Navigate(CodeReferences.CreateEntity(EntityKind.Type, type.Value.Label, type.Value.Module.FilePath,
                type.Value.Handle)),
            memberReference: static member => new Activation.Navigate(CodeReferences.CreateEntity(CodeReferences.GetKind(NodeDefinitions.GetDeclared(member.Value).SymbolKind),
                NodeDefinitions.GetDeclared(member.Value).Name, member.Value.Module.FilePath, member.Value.Handle)),
            moduleReference: static _ => Open,
            importedMethod: static import => new Activation.Navigate(import.Value.Shown.Reference),
            forwardedType: static forwarded => new Activation.Navigate(CodeReferences.CreateEntity(EntityKind.Type, forwarded.Value.Label,
                forwarded.Value.Module.FilePath, forwarded.Value.Handle)),
            baseTypes: static _ => Open,
            baseType: static baseType => baseType.Value.Target.Match<Activation>(
                static navigable => new Activation.Navigate(navigable.Reference),
                static _ => Open,
                static _ => Open),
            derivedTypes: static _ => Open,
            derivedType: static derived => new Activation.Navigate(derived.Value.Reference),
            resources: static _ => Open,
            resourceContainer: static _ => Open,
            resourceEntry: static _ => Open,
            metadata: static _ => Open,
            metadataTables: static _ => Open,
            metadataTable: static _ => Open,
            package: static _ => Open,
            packageFile: static _ => Open);
    }

    /// <summary>The reference a node of <paramref name="subject"/> points at, as documents link to it;
    /// null for one that opens itself.</summary>
    internal static CodeReference.Navigable? FindTarget(NodeSubject subject)
    {
        var activation = GetActivation(subject);
        if (activation is not Activation.Navigate navigate)
        {
            return null;
        }

        return navigate.Target;
    }

    /// <summary>The path of a node made under <paramref name="parent"/>, read from <paramref name="assembly"/>, which
    /// <paramref name="segment"/> names among its siblings, labelled <paramref name="text"/>: the parent's path and a
    /// step; for the root of a tree, its file's; for an entry of a package, its file's in the package's.</summary>
    internal static NodePath ExtendPath(BrowseNode? parent, LoadedAssembly assembly, NodePathSegment? segment, string text)
    {
        if (parent is null)
        {
            return new NodePath { FilePath = assembly.FilePath, Steps = [] };
        }

        if (segment is null)
        {
            throw new UnreachableException($"'{text}' sits under '{parent.Text}' but no segment names it.");
        }

        var step = new NodePathStep { Segment = segment, Label = text };
        if (segment.IsPackageEntry)
        {
            return new NodePath { FilePath = parent.Root.Assembly.FilePath, Steps = [step] };
        }

        return new NodePath { FilePath = parent.Path.FilePath, Steps = parent.Path.Steps.Add(step) };
    }

    /// <summary>The text of <paramref name="path"/>: its file, then the label of each step.</summary>
    internal static string DescribePath(NodePath path)
    {
        if (path.Steps.Length == 0)
        {
            return path.FilePath.Text;
        }

        return $"{path.FilePath}: {string.Join(" › ", path.Steps.Select(static s => s.Label))}";
    }

    /// <summary>The segment naming a node of <paramref name="subject"/>; null for the root of a tree.</summary>
    internal static NodePathSegment? FindPathSegment(NodeSubject subject)
    {
        return subject.Match<NodePathSegment?>(
            module: static module => module.Value.Place is null ? null : new NodePathSegment.EmbeddedModule(module.Value.Place.File.FullName),
            @namespace: static ns => new NodePathSegment.Namespace(ns.Value.Name),
            failedAssembly: static _ => null,
            typeDefinition: static type => new NodePathSegment.Definition(IdStringProvider.GetIdString(NodeDefinitions.GetSymbol(type.Definition))),
            memberDefinition: static member => new NodePathSegment.Definition(IdStringProvider.GetIdString(NodeDefinitions.GetSymbol(member.Definition))),
            references: static _ => new NodePathSegment.Folder(NodeFolder.References),
            assemblyReference: static reference => new NodePathSegment.AssemblyReference(reference.Value.Reference.Name),
            referencedTypes: static _ => new NodePathSegment.Folder(NodeFolder.ReferencedTypes),
            typeReference: static type => new NodePathSegment.UsedType(type.Value.ReflectionName),
            memberReference: static member => new NodePathSegment.UsedMember(GetUsedMemberId(NodeDefinitions.GetDeclared(member.Value))),
            moduleReference: static module => new NodePathSegment.ModuleReference(module.Value.Name),
            importedMethod: static import => new NodePathSegment.ImportedMethod(import.Value.Id),
            forwardedType: static forwarded => new NodePathSegment.ForwardedType(forwarded.Value.ReflectionName),
            baseTypes: static _ => new NodePathSegment.Folder(NodeFolder.BaseTypes),
            baseType: static baseType => new NodePathSegment.BaseType(GetSymbols(baseType.Value).Type.ReflectionName),
            derivedTypes: static _ => new NodePathSegment.Folder(NodeFolder.DerivedTypes),
            derivedType: static derived => new NodePathSegment.DerivedType(IdStringProvider.GetIdString(NodeDefinitions.GetSymbol(derived.Value))),
            resources: static _ => new NodePathSegment.Folder(NodeFolder.Resources),
            resourceContainer: static container => new NodePathSegment.Resource(container.Value.Resource.Name),
            resourceEntry: static entry => new NodePathSegment.Resource(entry.Value.Name),
            metadata: static metadata => new NodePathSegment.Metadata(LoadedAssemblies.IsOwnMetadata(metadata.Value) ? MetadataSource.Own : MetadataSource.Debug),
            metadataTables: static _ => new NodePathSegment.Folder(NodeFolder.MetadataTables),
            metadataTable: static table => new NodePathSegment.MetadataPart(table.Value.Key),
            package: static package => package.Value.IsRoot ? null : new NodePathSegment.PackageFolder(package.Value.Folder.FullName),
            packageFile: static file => new NodePathSegment.PackageFile(file.Value.File.FullName));
    }

    /// <summary>The documentation id of <paramref name="member"/>, written from its signature as the type
    /// system reads it, as <see cref="IdStringProvider"/> writes one for a member backed by metadata, so that
    /// a member of an assembly that does not resolve has one too.</summary>
    private static string GetUsedMemberId(IMember member)
    {
        var id = new StringBuilder();
        id.Append(member.SymbolKind switch
        {
            SymbolKind.Field => "F:",
            SymbolKind.Property or SymbolKind.Indexer => "P:",
            SymbolKind.Event => "E:",
            _ => "M:",
        });
        id.Append(IdStringProvider.GetTypeName(member.DeclaringType)).Append('.');
        id.Append(member.Name.Replace('.', '#').Replace('<', '{').Replace('>', '}'));
        if (member is IMethod method && method.TypeParameters.Count > 0)
        {
            id.Append("``").Append(method.TypeParameters.Count);
        }

        if (member is IParameterizedMember parameterized && parameterized.Parameters.Count > 0)
        {
            id.Append('(').AppendJoin(',', parameterized.Parameters.Select(static p => IdStringProvider.GetTypeName(p.Type))).Append(')');
        }

        if (member.SymbolKind == SymbolKind.Operator && member.Name is "op_Implicit" or "op_Explicit")
        {
            id.Append('~').Append(IdStringProvider.GetTypeName(member.ReturnType));
        }

        return id.ToString();
    }
}
