// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using ICSharpCode.Decompiler.CSharp;
using System.Text.RegularExpressions;
using System.Xml;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.Metadata;
using System.Net;
using System.Text.Json;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.TypeSystem;
using System.Text.Json.Serialization;

namespace ILSight.Core.Diagrams;

/// <summary>ILSpy's HTML class diagrammer for an assembly: a page that draws mermaid class diagrams
/// (https://mermaid.js.org/syntax/classDiagram.html) of the types picked in it, from the assembly's
/// types, their members, relations, inheritance and XML documentation baked into it as JSON. The
/// page loads mermaid from a CDN, so it draws only online.</summary>
public static partial class ClassDiagrams
{
    /// <summary>The file of the page, which opens the diagrammer.</summary>
    public const string PageFileName = "index.html";

    private const string TemplateName = "template.html";
    private const string ResourcePrefix = "diagram/";
    private const string RepoUrl = "https://github.com/icsharpcode/ILSpy";
    private static readonly string[] Assets = ["styles.css", "script.js", "ILSpy.ico"];

    /// <summary>Writes the diagrammer of <paramref name="assembly"/> into <paramref name="folder"/>,
    /// creating it, and returns the path of its page.</summary>
    /// <remarks>Once the read of the assembly's type system ended: for an assembly the library could not read, says so
    /// and writes nothing.</remarks>
    public static Task<TypeSystemWork<string>> WriteAsync(LoadedModule assembly, string folder, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return LoadedAssemblies.WithTypeSystemAsync(assembly, typeSystem => Write(assembly, typeSystem.MainModule, folder, progress, cancellationToken),
            cancellationToken);
    }

    private static string Write(LoadedModule assembly, MetadataModule main, string folder, IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        var model = BuildModel(assembly, main, progress, cancellationToken);
        Directory.CreateDirectory(folder);
        var page = Path.Combine(folder, PageFileName);
        File.WriteAllText(page, WritePage(main, model));
        foreach (var asset in Assets)
        {
            CopyAsset(asset, folder);
        }

        return page;
    }

    /// <summary>The model of every type of <paramref name="assembly"/> but compiler-generated ones and
    /// those nested in them, by namespace and then by full name.</summary>
    internal static DiagramModel BuildModel(LoadedModule assembly, MetadataModule main, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        var picked = main.TypeDefinitions
            .Where(static t => !t.IsCompilerGeneratedOrIsInCompilerGeneratedClass())
            .ToArray();
        var names = CreateNames(picked);
        var types = new DiagramTypes { Names = names, Docs = ReadDocs(LoadedAssemblies.GetFile(assembly)), Members = new MermaidMembers { Names = names } };
        var namespaces = picked
            .GroupBy(static t => t.Namespace)
            .OrderBy(static g => g.Key, StringComparer.InvariantCulture);
        var built = new BuiltTypes { AssemblyName = assembly.Name, Total = picked.Length, Progress = progress };
        var byNamespace = new Dictionary<string, Dictionary<string, DiagramType>>();
        foreach (var group in namespaces)
        {
            byNamespace[group.Key] = BuildNamespace(group, names, types, built, cancellationToken);
        }

        return new DiagramModel { OutsideReferences = names.OutsideReferences, TypesByNamespace = byNamespace };
    }

    private static Dictionary<string, DiagramType> BuildNamespace(IEnumerable<ITypeDefinition> group, DiagramNames names, DiagramTypes types,
        BuiltTypes built, CancellationToken cancellationToken)
    {
        var ordered = group.OrderBy(static t => t.FullName, StringComparer.InvariantCulture);
        var byId = new Dictionary<string, DiagramType>();
        foreach (var type in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byId[ClassDiagrams.GetId(names, type)] = ClassDiagrams.BuildType(types, type);
            CountBuilt(built);
        }
        return byId;
    }

    private static string WritePage(MetadataModule module, DiagramModel model)
    {
        var json = JsonSerializer.Serialize(model, DiagramJson.Default.DiagramModel);
        return ReadAsset(TemplateName)
            .Replace("{{SourceAssemblyName}}", WebUtility.HtmlEncode(module.AssemblyName))
            .Replace("{{SourceAssemblyVersion}}", WebUtility.HtmlEncode(module.AssemblyVersion.ToString()))
            .Replace("{{BuilderVersion}}", DecompilerVersionInfo.FullVersionWithCommitHash)
            .Replace("{{RepoUrl}}", RepoUrl)
            .Replace("{{Model}}", json);
    }

    private static string ReadAsset(string name)
    {
        using var reader = new StreamReader(OpenAsset(name));
        return reader.ReadToEnd();
    }

    private static void CopyAsset(string name, string folder)
    {
        using var asset = OpenAsset(name);
        using var file = File.Create(Path.Combine(folder, name));
        asset.CopyTo(file);
    }

    private static Stream OpenAsset(string name)
    {
        var resource = ResourcePrefix + name;
        var stream = typeof(ClassDiagrams).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            throw new FileNotFoundException("The diagrammer's resource is missing.", resource);
        }

        return stream;
    }

    /// <summary>How many of an assembly's types are built, reported as the share of them done.</summary>
    private sealed class BuiltTypes
    {
        public required string AssemblyName { get; init; }

        public required int Total { get; init; }

        public required IProgress<ExportProgress>? Progress { get; init; }

        internal int Done;
    }

    private static void CountBuilt(BuiltTypes built)
    {
        built.Done++;
        built.Progress?.Report(new ExportProgress
        {
            Name = built.AssemblyName,
            Part = 0,
            Parts = 1,
            Fraction = (float)built.Done / built.Total,
        });
    }

    /// <summary>The names and ids of <paramref name="picked"/>, the types of a diagram.</summary>
    internal static DiagramNames CreateNames(IReadOnlyCollection<ITypeDefinition> picked)
    {
        return new DiagramNames { Picked = new HashSet<IType>(picked), Ids = CreateIds(picked) };
    }

    internal static bool IsPicked(DiagramNames naming, IType type)
    {
        return naming.Picked.Contains(type);
    }

    private static Dictionary<IType, string> CreateIds(IEnumerable<ITypeDefinition> types)
    {
        var ids = new Dictionary<IType, string>();
        var groups = types.GroupBy(static t => t.Name);
        foreach (var group in groups)
        {
            AddIds(ids, group);
        }

        return ids;
    }

    // A name only one type has is its id; types sharing a name are numbered.
    private static void AddIds(Dictionary<IType, string> ids, IGrouping<string, ITypeDefinition> group)
    {
        var types = group.ToList();
        if (types.Count == 1)
        {
            ids[types[0]] = Sanitize(group.Key);
            return;
        }
        for (var i = 0; i < types.Count; i++)
        {
            ids[types[i]] = types[i].Name + (i + 1);
        }
    }

    // A type in the main module of an executable is named <Module>.
    private static string Sanitize(string typeName)
    {
        return typeName.Replace('<', '_').Replace('>', '_');
    }

    internal static string GetId(DiagramNames naming, IType type)
    {
        return IdentifyType(naming, type).Id;
    }

    /// <summary>The id of <paramref name="type"/>, which for a closed generic type is the id of its
    /// open one, returned beside it.</summary>
    private static (string Id, IType? OpenGeneric) IdentifyType(DiagramNames naming, IType type)
    {
        var openGeneric = FindOpenGeneric(type);
        var identified = openGeneric ?? type;
        if (naming.Ids.TryGetValue(identified, out var id))
        {
            return (id, openGeneric);
        }

        var created = CreateOutsideId(naming, identified);
        naming.Ids[identified] = created;
        return (created, openGeneric);
    }

    private static IType? FindOpenGeneric(IType type)
    {
        if (type is not ParameterizedType generic)
        {
            return null;
        }

        if (generic.TypeArguments.Any(static a => a is ITypeParameter))
        {
            return null;
        }

        return generic.GenericType;
    }

    // A type outside the diagram is told apart by its full name, and generic overloads by their
    // type parameters.
    private static string CreateOutsideId(DiagramNames naming, IType type)
    {
        var id = Sanitize(type.FullName.Replace('.', '_'));
        if (type.TypeParameterCount == 0)
        {
            return id;
        }

        var parameters = type.TypeParameters.Select(p => GetId(naming, p));
        return id + "_" + string.Join("_", parameters);
    }

    /// <summary>How <paramref name="type"/> reads in the diagram: C# keywords for built-in types, a
    /// nested type after the type declaring it, generic arguments in ❰ ❱ as mermaid can't take
    /// angle brackets.</summary>
    internal static string GetTypeName(DiagramNames naming, IType type)
    {
        if (naming.NamesByType.TryGetValue(type, out var name))
        {
            return name;
        }

        var created = CreateName(naming, type);
        naming.NamesByType[type] = created;
        return created;
    }

    private static string CreateName(DiagramNames naming, IType type)
    {
        if (type.TypeParameterCount < 1)
        {
            return CreatePlainName(naming, type);
        }

        if (TypeFacts.TryGetNullable(type, out var underlying))
        {
            return GetTypeName(naming, underlying) + "?";
        }

        var arguments = type.TypeArguments.Select(a => GetTypeName(naming, a));
        return type.Name + "❰" + string.Join(", ", arguments) + "❱";
    }

    private static string CreatePlainName(DiagramNames naming, IType type)
    {
        if (type is ArrayType array)
        {
            return GetTypeName(naming, array.ElementType) + "[]";
        }

        if (type is ByReferenceType reference)
        {
            return "&" + GetTypeName(naming, reference.ElementType);
        }

        var definition = type.GetDefinition();
        if (definition is null)
        {
            return type.Name;
        }

        if (definition.KnownTypeCode != KnownTypeCode.None)
        {
            return GetKeyword(definition.KnownTypeCode, type);
        }

        if (type.DeclaringType is not null)
        {
            return type.DeclaringType.Name + '+' + type.Name;
        }

        return type.Name.Replace('<', '❰').Replace('>', '❱');
    }

    private static string GetKeyword(KnownTypeCode code, IType type)
    {
        var keyword = KnownTypeReference.GetCSharpNameByTypeCode(code);
        if (keyword is null)
        {
            return type.Name;
        }

        return keyword;
    }

    /// <summary>A reference to <paramref name="type"/>, labelled with <paramref name="propertyName"/>
    /// for a property's relation, else with the closed generic type for a base type. Records the type
    /// as an outside reference when the diagram leaves it out.</summary>
    private static DiagramReference ReferTo(DiagramNames naming, IType type, string? propertyName = null)
    {
        var (id, openGeneric) = IdentifyType(naming, type);
        var referred = openGeneric ?? type;
        RecordOutside(naming, id, referred);
        if (propertyName is not null)
        {
            return new DiagramReference { To = id, Label = propertyName };
        }

        if (openGeneric is null)
        {
            return new DiagramReference { To = id, Label = null };
        }

        return new DiagramReference { To = id, Label = GetTypeName(naming, type) };
    }

    private static void RecordOutside(DiagramNames naming, string id, IType type)
    {
        if (IsPicked(naming, type))
        {
            return;
        }

        naming.OutsideReferences.TryAdd(id, type.Namespace + '.' + GetTypeName(naming, type));
    }

    internal static string FormatMethod(MermaidMembers mermaid, IMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => FormatParameter(mermaid, p)));
        var typeArguments = ListTypeArguments(mermaid, method);
        var returnType = ClassDiagrams.GetTypeName(mermaid.Names, method.ReturnType);
        return $"{GetVisibility(method.Accessibility)}{GetMethodName(mermaid, method)}{typeArguments}({parameters}){GetModifier(method)} {returnType}";
    }

    private static string FormatParameter(MermaidMembers mermaid, IParameter parameter)
    {
        return ClassDiagrams.GetTypeName(mermaid.Names, parameter.Type) + " " + parameter.Name;
    }

    private static string ListTypeArguments(MermaidMembers mermaid, IMethod method)
    {
        if (method.TypeArguments.Count == 0)
        {
            return "";
        }

        var arguments = method.TypeArguments.Select(a => GetTypeName(mermaid.Names, a));
        return "❰" + string.Join(", ", arguments) + "❱";
    }

    // An explicit interface implementation is named after the interface member it implements.
    private static string GetMethodName(MermaidMembers mermaid, IMethod method)
    {
        if (!method.IsExplicitInterfaceImplementation)
        {
            return method.Name;
        }

        var implemented = method.ExplicitlyImplementedInterfaceMembers.FirstOrDefault();
        if (implemented is null)
        {
            return method.Name;
        }

        return ClassDiagrams.GetTypeName(mermaid.Names, implemented.DeclaringType) + '.' + implemented.Name;
    }

    internal static string FormatProperty(MermaidMembers mermaid, IProperty property)
    {
        var type = ClassDiagrams.GetTypeName(mermaid.Names, property.ReturnType);
        return $"{GetVisibility(property.Accessibility)}{type} {property.Name}{GetModifier(property)}";
    }

    internal static string FormatField(MermaidMembers mermaid, IField field)
    {
        var type = ClassDiagrams.GetTypeName(mermaid.Names, field.ReturnType);
        return $"{GetVisibility(field.Accessibility)}{type} {field.Name}{GetModifier(field)}";
    }

    private static string GetModifier(IMember member)
    {
        if (member.IsAbstract)
        {
            return "*";
        }

        if (member.IsStatic)
        {
            return "$";
        }

        return "";
    }

    private static string GetVisibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Private => "-",
            Accessibility.ProtectedAndInternal => "~",
            Accessibility.Internal => "~",
            Accessibility.Protected => "#",
            Accessibility.ProtectedOrInternal => "#",
            Accessibility.Public => "+",
            _ => "",
        };
    }

    private const string NewLine = "\n";
    private const string MemberSeparator = NewLine + "    ";

    private static DiagramType BuildType(DiagramTypes diagram, ITypeDefinition type)
    {
        if (type.Kind == TypeKind.Enum)
        {
            return BuildEnum(diagram, type);
        }

        return BuildClass(diagram, type);
    }

    private static DiagramType BuildEnum(DiagramTypes diagram, ITypeDefinition type)
    {
        var fields = type
            .GetFields(IsEnumValue)
            .ToArray();
        var values = fields
            .Select(static f => f.Name)
            .Prepend("<<Enumeration>>");
        var id = ClassDiagrams.GetId(diagram.Names, type);
        return new DiagramType
        {
            Name = FindNameUnlessId(diagram, type, id),
            Body = WriteDefinition(id, PadMembers(values).TrimEnd(' ')),
            XmlDocs = ClassDiagrams.CollectDocs(diagram.Docs, type, fields),
        };
    }

    private static bool IsEnumValue(IField field)
    {
        if (!field.IsConst)
        {
            return false;
        }

        if (!field.IsStatic)
        {
            return false;
        }

        return field.Accessibility == Accessibility.Public;
    }

    private static DiagramType BuildClass(DiagramTypes diagram, ITypeDefinition type)
    {
        var id = ClassDiagrams.GetId(diagram.Names, type);
        var properties = type
            .GetProperties()
            .ToArray();
        var methods = ListMethods(type);
        var fields = ListFields(diagram, type, properties);
        var members = SplitByDeclaringType(diagram, properties, methods, fields);
        var documented = fields
            .Concat<IMember>(properties)
            .Concat(methods);
        return new DiagramType
        {
            Name = FindNameUnlessId(diagram, type, id),
            Body = WriteDefinition(id, WriteBody(diagram, type, members)),
            HasOne = MapHasOne(diagram, members, type),
            HasMany = MapHasMany(diagram, members, type),
            BaseType = MapBaseType(diagram, type),
            Interfaces = MapInterfaces(diagram, type),
            Inherited = ListInherited(diagram, type, id, members),
            XmlDocs = ClassDiagrams.CollectDocs(diagram.Docs, type, documented),
        };
    }

    private static string? FindNameUnlessId(DiagramTypes diagram, IType type, string id)
    {
        var name = ClassDiagrams.GetTypeName(diagram.Names, type);
        if (name == id)
        {
            return null;
        }

        return name;
    }

    private static string WriteDefinition(string id, string body)
    {
        return $"class {id} {{{body}}}";
    }

    private static string PadMembers(IEnumerable<string> lines)
    {
        return MemberSeparator + string.Join(MemberSeparator, lines) + MemberSeparator;
    }

    /// <summary>The methods <paramref name="type"/> shows: its own and those it inherits, but no
    /// operators, nothing compiler-generated, and nothing object declares or a base type overrides
    /// from it.</summary>
    private static IMethod[] ListMethods(ITypeDefinition type)
    {
        return type
            .GetMethods(m => IsShownMethod(type, m))
            .ToArray();
    }

    private static bool IsShownMethod(ITypeDefinition type, IMethod method)
    {
        if (method.IsOperator)
        {
            return false;
        }

        if (method.IsCompilerGenerated())
        {
            return false;
        }

        if (method.DeclaringTypeDefinition == type)
        {
            return true;
        }

        if (TypeFacts.IsObject(method.DeclaringType))
        {
            return false;
        }

        return !TypeFacts.OverridesObject(method);
    }

    /// <summary>The fields <paramref name="type"/> shows: none compiler-generated, and none that
    /// looks like the backing field of one of <paramref name="properties"/>.</summary>
    private static IField[] ListFields(DiagramTypes diagram, ITypeDefinition type, IProperty[] properties)
    {
        return type
            .GetFields(f => IsShownField(diagram, f, properties))
            .ToArray();
    }

    private static bool IsShownField(DiagramTypes diagram, IField field, IProperty[] properties)
    {
        if (IsHiddenEntity(diagram, field))
        {
            return false;
        }

        return !properties.Any(p => IsBackingField(field, p));
    }

    private static bool IsHiddenEntity(DiagramTypes diagram, IEntity entity)
    {
        return CSharpDecompiler.MemberIsHidden(DecompilerFacts.GetMetadataFile(entity), entity.MetadataToken, diagram.Settings);
    }

    // A field of a property's type whose name holds the property's, as _name does for Name.
    private static bool IsBackingField(IField field, IProperty property)
    {
        if (!field.ReturnType.Equals(property.ReturnType))
        {
            return false;
        }

        return field.Name.Contains(property.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static DeclaredMembers SplitByDeclaringType(DiagramTypes diagram, IProperty[] properties, IMethod[] methods, IField[] fields)
    {
        var hasOne = properties
            .Where(p => HoldsOne(diagram, p))
            .ToArray();
        var hasMany = properties
            .Select(p => FindManyRelation(diagram, p))
            .OfType<ManyRelation>()
            .ToArray();
        var related = hasMany
            .Select(static r => r.Property)
            .Concat(hasOne)
            .ToHashSet();
        var flat = properties
            .Where(p => !related.Contains(p))
            .ToArray();
        return new DeclaredMembers
        {
            FlatProperties = GroupByDeclaringType(flat, static p => p),
            HasOne = GroupByDeclaringType(hasOne, static p => p),
            HasMany = GroupByDeclaringType(hasMany, static r => r.Property),
            Methods = GroupByDeclaringType(methods, static m => m),
            Fields = GroupByDeclaringType(fields, static f => f),
        };
    }

    private static Dictionary<IType, T[]> GroupByDeclaringType<T>(IEnumerable<T> items, Func<T, IMember> memberOf)
    {
        return items
            .GroupBy(i => memberOf(i).DeclaringType)
            .ToDictionary(static g => g.Key, static g => g.ToArray());
    }

    // A property whose type, or the type it makes nullable, is drawn in the diagram.
    private static bool HoldsOne(DiagramTypes diagram, IProperty property)
    {
        var type = property.ReturnType;
        if (TypeFacts.TryGetNullable(type, out var underlying))
        {
            type = underlying;
        }

        return ClassDiagrams.IsPicked(diagram.Names, type);
    }

    // A property of a generic enumerable of a type drawn in the diagram.
    private static ManyRelation? FindManyRelation(DiagramTypes diagram, IProperty property)
    {
        if (!TypeFacts.TryGetElementType(property, out var elementType))
        {
            return null;
        }

        if (!ClassDiagrams.IsPicked(diagram.Names, elementType))
        {
            return null;
        }

        return new ManyRelation { Property = property, ElementType = elementType };
    }

    private static string WriteBody(DiagramTypes diagram, ITypeDefinition type, DeclaredMembers members)
    {
        var own = PadMembers(ListFlatMembers(diagram, members, type, ""));
        var annotation = FindAnnotation(type);
        if (annotation is null)
        {
            return own.TrimEnd(' ');
        }

        return own + $"<<{annotation}>>" + NewLine;
    }

    // See https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes; a static class is
    // abstract and sealed.
    private static string? FindAnnotation(ITypeDefinition type)
    {
        if (TypeFacts.IsInterface(type))
        {
            return "Interface";
        }

        if (!type.IsAbstract)
        {
            return null;
        }

        if (type.IsSealed)
        {
            return "Service";
        }

        return "Abstract";
    }

    private static IEnumerable<string> ListFlatMembers(DiagramTypes diagram, DeclaredMembers members, IType declaring, string prefix)
    {
        var properties = GetDeclared(members.FlatProperties, declaring).Select(p => prefix + ClassDiagrams.FormatProperty(diagram.Members, p));
        var methods = GetDeclared(members.Methods, declaring).Select(m => prefix + ClassDiagrams.FormatMethod(diagram.Members, m));
        var fields = GetDeclared(members.Fields, declaring).Select(f => prefix + ClassDiagrams.FormatField(diagram.Members, f));
        return properties
            .Concat(methods)
            .Concat(fields);
    }

    private static T[] GetDeclared<T>(Dictionary<IType, T[]> byType, IType declaring)
    {
        if (byType.TryGetValue(declaring, out var declared))
        {
            return declared;
        }

        return [];
    }

    /// <summary>The members <paramref name="type"/> inherits, by the id of each base type declaring
    /// them, written as members of <paramref name="id"/> for the diagrammer to add.</summary>
    private static Dictionary<string, InheritedMembers> ListInherited(DiagramTypes diagram, ITypeDefinition type, string id, DeclaredMembers members)
    {
        var prefix = id + " : ";
        var inherited = new Dictionary<string, InheritedMembers>();
        var ancestors = type
            .GetNonInterfaceBaseTypes()
            .Where(t => TypeFacts.IsAncestor(type, t));
        foreach (var ancestor in ancestors)
        {
            inherited[ClassDiagrams.GetId(diagram.Names, ancestor)] = GetInheritedFrom(diagram, members, ancestor, prefix);
        }

        return inherited;
    }

    private static InheritedMembers GetInheritedFrom(DiagramTypes diagram, DeclaredMembers members, IType ancestor, string prefix)
    {
        var flat = ListFlatMembers(diagram, members, ancestor, prefix).ToList();
        string? flatMembers = null;
        if (flat.Count > 0)
        {
            flatMembers = string.Join(NewLine, flat);
        }

        return new InheritedMembers
        {
            FlatMembers = flatMembers,
            HasOne = MapHasOne(diagram, members, ancestor),
            HasMany = MapHasMany(diagram, members, ancestor),
        };
    }

    private static Dictionary<string, string>? MapHasOne(DiagramTypes diagram, DeclaredMembers members, IType declaring)
    {
        if (!members.HasOne.TryGetValue(declaring, out var properties))
        {
            return null;
        }

        var relations = new Dictionary<string, string>();
        foreach (var property in properties)
        {
            RelateOne(diagram, relations, property);
        }

        return relations;
    }

    private static void RelateOne(DiagramTypes diagram, Dictionary<string, string> relations, IProperty property)
    {
        var type = property.ReturnType;
        var label = property.Name;
        if (property.IsIndexer)
        {
            label += WriteIndexerParameters(property);
        }

        if (TypeFacts.TryGetNullable(type, out var underlying))
        {
            type = underlying;
            label += " ?";
        }
        Relate(relations, label, ClassDiagrams.ReferTo(diagram.Names, type, label));
    }

    private static string WriteIndexerParameters(IProperty indexer)
    {
        var parameters = indexer.Parameters.Select(static p => p.Type.Name + " " + p.Name);
        return "[" + string.Join(", ", parameters) + "]";
    }

    private static Dictionary<string, string>? MapHasMany(DiagramTypes diagram, DeclaredMembers members, IType declaring)
    {
        if (!members.HasMany.TryGetValue(declaring, out var many))
        {
            return null;
        }

        var relations = new Dictionary<string, string>();
        foreach (var relation in many)
        {
            Relate(relations, relation.Property.Name, ClassDiagrams.ReferTo(diagram.Names, relation.ElementType, relation.Property.Name));
        }

        return relations;
    }

    // Relations are keyed by label, the name of the property; a label two properties share keeps the last.
    private static void Relate(Dictionary<string, string> relations, string label, DiagramReference reference)
    {
        relations[label] = reference.To;
    }

    /// <summary>The class <paramref name="type"/> derives from, unless that is object.</summary>
    private static Dictionary<string, string?>? MapBaseType(DiagramTypes diagram, ITypeDefinition type)
    {
        var baseType = type.DirectBaseTypes.FirstOrDefault(IsBaseClass);
        if (baseType is null)
        {
            return null;
        }

        var reference = ClassDiagrams.ReferTo(diagram.Names, baseType);
        return new Dictionary<string, string?> { [reference.To] = reference.Label };
    }

    private static bool IsBaseClass(IType type)
    {
        if (TypeFacts.IsInterface(type))
        {
            return false;
        }

        return !TypeFacts.IsObject(type);
    }

    /// <summary>The interfaces <paramref name="type"/> implements directly, each with the labels of
    /// the closed generic forms it implements.</summary>
    private static Dictionary<string, string?[]>? MapInterfaces(DiagramTypes diagram, ITypeDefinition type)
    {
        var references = type.DirectBaseTypes
            .Where(TypeFacts.IsInterface)
            .Select(i => ClassDiagrams.ReferTo(diagram.Names, i))
            .ToList();
        if (references.Count == 0)
        {
            return null;
        }

        return references
            .GroupBy(static r => r.To)
            .ToDictionary(static g => g.Key, ListLabels);
    }

    private static string?[] ListLabels(IEnumerable<DiagramReference> references)
    {
        return references
            .Select(static r => r.Label)
            .ToArray();
    }

    /// <summary>The documentation <paramref name="file"/> has, wherever it is found; none when it
    /// has none or it cannot be read.</summary>
    internal static DiagramDocs ReadDocs(MetadataFile file)
    {
        try
        {
            return new DiagramDocs { Provider = XmlDocLoader.LoadDocumentation(file) };
        }
        catch (XmlException)
        {
            return new DiagramDocs { Provider = null };
        }
        catch (IOException)
        {
            return new DiagramDocs { Provider = null };
        }
        catch (UnauthorizedAccessException)
        {
            return new DiagramDocs { Provider = null };
        }
    }

    /// <summary>The documentation of <paramref name="type"/>, under an empty key, and of each of
    /// <paramref name="members"/>, under its name; null when none has any.</summary>
    internal static Dictionary<string, string>? CollectDocs(DiagramDocs docs, ITypeDefinition type, IEnumerable<IMember> members)
    {
        var provider = docs.Provider;
        if (provider is null)
        {
            return null;
        }

        var found = new Dictionary<string, string>();
        AddDoc(provider, found, string.Empty, type);
        foreach (var member in members)
        {
            AddDoc(provider, found, member.Name, member);
        }

        if (found.Count == 0)
        {
            return null;
        }

        return found;
    }

    private static void AddDoc(IDocumentationProvider provider, Dictionary<string, string> found, string key, IEntity entity)
    {
        var text = FindDocText(provider, entity);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        found[key] = text;
    }

    private static string? FindDocText(IDocumentationProvider provider, IEntity entity)
    {
        var xml = ReadDocXml(provider, entity);
        if (xml is null)
        {
            return null;
        }

        var text = xml
            .Replace("<summary>", null)
            .Replace("</summary>", null)
            .Replace("<para>", "\n")
            .Replace("</para>", "\n")
            .Trim()
            .Replace('<', '[')
            .Replace('>', ']');
        var plain = Noise().Replace(text, string.Empty);
        return HorizontalSpace().Replace(plain, " ");
    }

    private static string? ReadDocXml(IDocumentationProvider provider, IEntity entity)
    {
        try
        {
            return provider.GetDocumentation(entity);
        }
        catch (XmlException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Spaces at either end of a line, and what references leave behind once their brackets are
    // squared: the start of a see cref (with its symbol kind) or paramref up to the name, and the
    // quote and slash after it.
    [GeneratedRegex("""^[ \t]+|[ \t]+$|(see\s.ref="(.:)?)|(paramref\sname=")|("\s/)""", RegexOptions.Multiline)]
    private static partial Regex Noise();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex HorizontalSpace();

    [JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(DiagramModel))]
    private sealed partial class DiagramJson : JsonSerializerContext;

    /// <summary>A reference from one type of a diagram to another: the id it points to, and the label it
    /// is drawn with, when it has one.</summary>
    private readonly record struct DiagramReference
    {
        public required string To { get; init; }

        public required string? Label { get; init; }
    }

    /// <summary>The types of a diagram, each as a mermaid class definition of its own members, with
    /// properties holding diagram types drawn as relations instead, and the members it inherits kept
    /// apart by the type declaring them, so the diagrammer can leave out those a base type it draws
    /// already shows.</summary>
    private sealed class DiagramTypes
    {
        public required DiagramNames Names { get; init; }

        public required DiagramDocs Docs { get; init; }

        // The settings compiler-generated members are recognised by, among them automatic properties'
        // backing fields.
        public readonly DecompilerSettings Settings = new();
        public required MermaidMembers Members { get; init; }
    }

    /// <summary>A property holding many of a diagram's types, and the type it holds.</summary>
    private readonly record struct ManyRelation
    {
        public required IProperty Property { get; init; }

        public required IType ElementType { get; init; }
    }

    /// <summary>A type's members as the diagram splits them, each kind by the type declaring it:
    /// plain properties, properties holding one or many diagram types, methods and fields.</summary>
    private sealed record DeclaredMembers
    {
        public required Dictionary<IType, IProperty[]> FlatProperties { get; init; }

        public required Dictionary<IType, IProperty[]> HasOne { get; init; }

        public required Dictionary<IType, ManyRelation[]> HasMany { get; init; }

        public required Dictionary<IType, IMethod[]> Methods { get; init; }

        public required Dictionary<IType, IField[]> Fields { get; init; }
    }
}
