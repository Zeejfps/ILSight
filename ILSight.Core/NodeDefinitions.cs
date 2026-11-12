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
            return new BaseTypeInfo { Module = module, Type = type, Target = new BaseTypeTarget.Unresolved(), Visibility = NodeVisibility.Open };
        }

        var reference = CodeReferences.Find(definition);
        if (reference == null)
        {
            return new BaseTypeInfo { Module = module, Type = type, Target = new BaseTypeTarget.Unaddressed(definition), Visibility = NodeVisibility.Open };
        }

        return new BaseTypeInfo { Module = module, Type = type, Target = new BaseTypeTarget.Navigable(definition, reference), Visibility = GetReferencedVisibility(definition) };
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
        return new NamespaceInfo
        {
            Module = module,
            Name = name,
            Types = types,
            Forwarded = forwarded,
            IsPublicApi = forwarded.Count > 0 || types.Any(EntityText.IsPublicApi),
            IsCompilerGenerated = forwarded.Count == 0 && types.All(Members.IsCompilerGenerated),
        };
    }

    /// <summary>The derived type <paramref name="type"/>; null when it has no reference to be navigated to.</summary>
    internal static DerivedTypeInfo? FindDerivedType(LoadedModule module, ITypeDefinition type)
    {
        var reference = CodeReferences.Find(type);
        if (reference == null)
        {
            return null;
        }

        return new DerivedTypeInfo { Module = module, Symbol = type, Reference = reference, Visibility = GetReferencedVisibility(type) };
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

        return new DefinedType
        {
            Module = module, Symbol = type, Handle = (TypeDefinitionHandle)type.MetadataToken, Reference = reference, Visibility = GetDefinitionVisibility(type),
            Name = type.Name, Arity = type.TypeParameterCount,
        };
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

        return new DefinedMember { Module = module, Symbol = member, Reference = reference, Visibility = GetDefinitionVisibility(member) };
    }

    /// <summary>A reference to the type or member a node of <paramref name="subject"/> defines, as documents
    /// point at it; null for a node that defines none.</summary>
    public static CodeReference.Entity? ReferenceOf(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.TypeDefinition type => type.Definition.Reference,
            NodeSubject.MemberDefinition member => member.Definition.Reference,
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ImportedMethod or NodeSubject.ForwardedType
                or NodeSubject.BaseTypes or NodeSubject.BaseType or NodeSubject.DerivedTypes or NodeSubject.DerivedType or NodeSubject.Resources
                or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables
                or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace
                or NodeSubject.FailedAssembly => null,
            _ => throw new UnreachableException(),
        };
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
        return new NodeVisibility(EntityText.Map(entity.Accessibility), EntityText.IsPublicApi(entity), Members.IsOrSitsInCompilerGenerated(entity));
    }

    /// <summary>How visible a type another node points at is: as declared, and never compiler-generated.</summary>
    internal static NodeVisibility GetReferencedVisibility(IEntity entity)
    {
        return new NodeVisibility(EntityText.Map(entity.Accessibility), EntityText.IsPublicApi(entity), false);
    }

    /// <summary>How visible what a node of <paramref name="subject"/> stands for is.</summary>
    public static NodeVisibility GetVisibility(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.TypeDefinition type => type.Definition.Visibility,
            NodeSubject.MemberDefinition member => member.Definition.Visibility,
            NodeSubject.ImportedMethod import => import.Value.Shown.Visibility with { IsCompilerGenerated = false },
            NodeSubject.DerivedType derived => derived.Value.Visibility,
            NodeSubject.BaseType baseType => baseType.Value.Visibility,
            NodeSubject.Namespace ns => new NodeVisibility(NodeAccessibility.None, ns.Value.IsPublicApi, ns.Value.IsCompilerGenerated),
            NodeSubject.ResourceContainer container => GetResourceVisibility(container.Value.Resource),
            NodeSubject.ResourceEntry entry => entry.Value.Source switch
            {
                ResourceSource.Manifest manifest => GetResourceVisibility(manifest.Resource),
                ResourceSource.Entry or ResourceSource.Baml => NodeVisibility.Open,
                _ => throw new UnreachableException(),
            },
            NodeSubject.References or NodeSubject.AssemblyReference or NodeSubject.ReferencedTypes or NodeSubject.TypeReference
                or NodeSubject.MemberReference or NodeSubject.ModuleReference or NodeSubject.ForwardedType or NodeSubject.BaseTypes
                or NodeSubject.DerivedTypes or NodeSubject.Resources or NodeSubject.Metadata or NodeSubject.MetadataTables
                or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module
                or NodeSubject.FailedAssembly => NodeVisibility.Open,
            _ => throw new UnreachableException(),
        };
    }

    private static NodeVisibility GetResourceVisibility(ICSharpCode.Decompiler.Metadata.Resource resource)
    {
        var (isPublic, accessibility) = ResourceNodes.Visibility(resource);
        return new NodeVisibility(accessibility, isPublic, false);
    }

    /// <summary>What activating a node of <paramref name="subject"/> does.</summary>
    public static Activation GetActivation(NodeSubject subject)
    {
        return subject switch
        {
            NodeSubject.AssemblyReference reference => new Activation.Navigate(CodeReferences.CreateAssembly(reference.Value.Reference.Name,
                reference.Value.Module.FilePath, reference.Value.Reference.Handle)),
            NodeSubject.TypeReference type => new Activation.Navigate(CodeReferences.CreateEntity(EntityKind.Type, type.Value.Label, type.Value.Module.FilePath,
                type.Value.Handle)),
            NodeSubject.MemberReference member => new Activation.Navigate(CodeReferences.CreateEntity(CodeReferences.GetKind(member.Value.Declared.SymbolKind),
                member.Value.Declared.Name, member.Value.Module.FilePath, member.Value.Handle)),
            NodeSubject.ForwardedType forwarded => new Activation.Navigate(CodeReferences.CreateEntity(EntityKind.Type, forwarded.Value.Label,
                forwarded.Value.Module.FilePath, forwarded.Value.Handle)),
            NodeSubject.ImportedMethod import => new Activation.Navigate(import.Value.Shown.Reference),
            NodeSubject.DerivedType derived => new Activation.Navigate(derived.Value.Reference),
            NodeSubject.BaseType baseType => baseType.Value.Target switch
            {
                BaseTypeTarget.Navigable navigable => new Activation.Navigate(navigable.Reference),
                BaseTypeTarget.Unaddressed or BaseTypeTarget.Unresolved => Open,
                _ => throw new UnreachableException(),
            },
            NodeSubject.TypeDefinition or NodeSubject.MemberDefinition or NodeSubject.References or NodeSubject.ReferencedTypes
                or NodeSubject.ModuleReference or NodeSubject.BaseTypes or NodeSubject.DerivedTypes or NodeSubject.Resources
                or NodeSubject.ResourceContainer or NodeSubject.ResourceEntry or NodeSubject.Metadata or NodeSubject.MetadataTables
                or NodeSubject.MetadataTable or NodeSubject.Package or NodeSubject.PackageFile or NodeSubject.Module or NodeSubject.Namespace
                or NodeSubject.FailedAssembly => Open,
            _ => throw new UnreachableException(),
        };
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

        var step = new NodePathStep(segment, text);
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
        return subject switch
        {
            NodeSubject.TypeDefinition type => new NodePathSegment.Definition(IdStringProvider.GetIdString(type.Definition.Symbol)),
            NodeSubject.MemberDefinition member => new NodePathSegment.Definition(IdStringProvider.GetIdString(member.Definition.Symbol)),
            NodeSubject.References => new NodePathSegment.Folder(NodeFolder.References),
            NodeSubject.AssemblyReference reference => new NodePathSegment.AssemblyReference(reference.Value.Reference.Name),
            NodeSubject.ReferencedTypes => new NodePathSegment.Folder(NodeFolder.ReferencedTypes),
            NodeSubject.TypeReference type => new NodePathSegment.UsedType(type.Value.ReflectionName),
            NodeSubject.MemberReference member => new NodePathSegment.UsedMember(GetUsedMemberId(member.Value.Declared)),
            NodeSubject.ModuleReference module => new NodePathSegment.ModuleReference(module.Value.Name),
            NodeSubject.ImportedMethod import => new NodePathSegment.ImportedMethod(import.Value.Id),
            NodeSubject.ForwardedType forwarded => new NodePathSegment.ForwardedType(forwarded.Value.ReflectionName),
            NodeSubject.BaseTypes => new NodePathSegment.Folder(NodeFolder.BaseTypes),
            NodeSubject.BaseType baseType => new NodePathSegment.BaseType(baseType.Value.Type.ReflectionName),
            NodeSubject.DerivedTypes => new NodePathSegment.Folder(NodeFolder.DerivedTypes),
            NodeSubject.DerivedType derived => new NodePathSegment.DerivedType(IdStringProvider.GetIdString(derived.Value.Symbol)),
            NodeSubject.Resources => new NodePathSegment.Folder(NodeFolder.Resources),
            NodeSubject.ResourceContainer container => new NodePathSegment.Resource(container.Value.Resource.Name),
            NodeSubject.ResourceEntry entry => new NodePathSegment.Resource(entry.Value.Name),
            NodeSubject.Metadata metadata => new NodePathSegment.Metadata(LoadedAssemblies.IsOwnMetadata(metadata.Value) ? MetadataSource.Own : MetadataSource.Debug),
            NodeSubject.MetadataTables => new NodePathSegment.Folder(NodeFolder.MetadataTables),
            NodeSubject.MetadataTable table => new NodePathSegment.MetadataPart(table.Value.Key),
            NodeSubject.Package package => package.Value.IsRoot ? null : new NodePathSegment.PackageFolder(package.Value.Folder.FullName),
            NodeSubject.PackageFile file => new NodePathSegment.PackageFile(file.Value.File.FullName),
            NodeSubject.Module module => module.Value.Place is null ? null : new NodePathSegment.EmbeddedModule(module.Value.Place.File.FullName),
            NodeSubject.Namespace ns => new NodePathSegment.Namespace(ns.Value.Name),
            NodeSubject.FailedAssembly => null,
            _ => throw new UnreachableException(),
        };
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
