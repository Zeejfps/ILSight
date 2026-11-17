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

namespace ILSight.Core.DiagramArea;

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

    // The settings compiler-generated members are recognised by, among them automatic properties' backing fields.
    private static readonly DecompilerSettings HiddenSettings = new();

    /// <summary>Writes the diagrammer of <paramref name="assembly"/> into <paramref name="folder"/>,
    /// creating it, and returns the path of its page.</summary>
    /// <remarks>Once the read of the assembly's type system ended: for an assembly the library could not read, says so
    /// and writes nothing. The diagram's model is read out of the library's type system a step at a time, each under a
    /// catch of its own, the app's rules and drawing between them: what the library throws at a step is
    /// <see cref="Outcome{T}.Failed"/>. The page is made of that model and written after
    /// the catch: what the disk throws writing it (<see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>)
    /// is <see cref="Outcome{T}.Failed"/> too, and a bug of the app's making it goes on to the edge. The cancellation of
    /// <paramref name="cancellationToken"/> is let through.</remarks>
    public static async Task<Outcome<TypeSystemWork<string>>> WriteAsync(LoadedModule assembly, string folder, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var work = await LoadedAssemblies.WithTypeSystemAsync(assembly, (_, typeSystem) => ReadDiagram(assembly, typeSystem.MainModule, progress,
            cancellationToken), cancellationToken);
        return work switch
        {
            TypeSystemWork<Outcome<DiagramRead>>.Done { Value: Outcome<DiagramRead>.Ok read } => WritePage(folder, read.Value),
            TypeSystemWork<Outcome<DiagramRead>>.Done { Value: Outcome<DiagramRead>.Failed failed } => new Outcome<TypeSystemWork<string>>.Failed(failed.Error),
            TypeSystemWork<Outcome<DiagramRead>>.Unreadable unreadable =>
                new Outcome<TypeSystemWork<string>>.Ok(new TypeSystemWork<string>.Unreadable { Module = unreadable.Module }),
            TypeSystemWork<Outcome<DiagramRead>>.Closed closed => new Outcome<TypeSystemWork<string>>.Ok(new TypeSystemWork<string>.Closed { Module = closed.Module }),
        };
    }

    // The page made of the diagram read and written into folder with its assets: its path; what the disk throws
    // writing it, the export's failure.
    private static Outcome<TypeSystemWork<string>> WritePage(string folder, DiagramRead diagram)
    {
        var page = BuildPage(diagram);
        var assets = ReadAssets();
        string written;
        try
        {
            written = Write(folder, page, assets);
        }
        catch (IOException e)
        {
            return new Outcome<TypeSystemWork<string>>.Failed(e);
        }
        catch (UnauthorizedAccessException e)
        {
            return new Outcome<TypeSystemWork<string>>.Failed(e);
        }

        return new Outcome<TypeSystemWork<string>>.Ok(new TypeSystemWork<string>.Done { Value = written });
    }

    // What the page is made of, read out of the library's type system: the model and the assembly's name and version.
    private sealed class DiagramRead
    {
        public required DiagramModel Model { get; init; }

        public required string AssemblyName { get; init; }

        public required string AssemblyVersion { get; init; }
    }

    // The diagram, read a step at a time (BuildModel); what the library throws at a step, the export's failure. The
    // app's rules and the drawing run between the steps, outside their catches, so a bug of the app's goes on to the edge.
    private static Outcome<DiagramRead> ReadDiagram(LoadedModule assembly, MetadataModule main, IProgress<ExportProgress>? progress,
        CancellationToken cancellationToken)
    {
        DiagramModel model;
        try
        {
            model = BuildModel(assembly, main, progress, cancellationToken);
        }
        catch (DiagramReadException e)
        {
            return new Outcome<DiagramRead>.Failed(e.Error);
        }

        var identity = ReadIdentity(main);
        return identity switch
        {
            Outcome<(string Name, string Version)>.Ok ok =>
                new Outcome<DiagramRead>.Ok(new DiagramRead { Model = model, AssemblyName = ok.Value.Name, AssemblyVersion = ok.Value.Version }),
            Outcome<(string Name, string Version)>.Failed failed => new Outcome<DiagramRead>.Failed(failed.Error),
        };
    }

    private static Outcome<(string Name, string Version)> ReadIdentity(MetadataModule main)
    {
        try
        {
            return new Outcome<(string Name, string Version)>.Ok((main.AssemblyName, main.AssemblyVersion.ToString()));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<(string Name, string Version)>.Failed(e);
        }
    }

    // What the library threw at a step of the diagram's read, thrown on outside that step's catch to end the read as the
    // export's failure.
    private sealed class DiagramReadException(Exception error) : Exception(error.Message, error)
    {
        public Exception Error { get; } = error;
    }

    // The value a step read; what the library threw there, thrown on as the diagram's failed read.
    private static T ValueOf<T>(Outcome<T> read)
    {
        return read switch
        {
            Outcome<T>.Ok ok => ok.Value,
            Outcome<T>.Failed failed => throw new DiagramReadException(failed.Error),
        };
    }

    // The page and its assets written into folder, creating it: the page's path.
    private static string Write(string folder, string page, IReadOnlyList<(string Name, byte[] Bytes)> assets)
    {
        Directory.CreateDirectory(folder);
        var pagePath = Path.Combine(folder, PageFileName);
        File.WriteAllText(pagePath, page);
        foreach (var (name, bytes) in assets)
        {
            File.WriteAllBytes(Path.Combine(folder, name), bytes);
        }

        return pagePath;
    }

    // The page's assets, read out of the app's own resources before the disk is written, so a missing one is the app's
    // bug, never said as the disk's failure.
    private static List<(string Name, byte[] Bytes)> ReadAssets()
    {
        var assets = new List<(string Name, byte[] Bytes)>();
        foreach (var name in Assets)
        {
            using var asset = OpenAsset(name);
            using var bytes = new MemoryStream();
            asset.CopyTo(bytes);
            assets.Add((name, bytes.ToArray()));
        }
        return assets;
    }

    /// <summary>The model of every type of <paramref name="assembly"/> but compiler-generated ones and
    /// those nested in them, by namespace and then by full name.</summary>
    /// <remarks>Read a step at a time, each step under a catch of its own: the module's types with their names, then
    /// for each type its members with what the app's rules of what a card shows ask of them, then what each member shown
    /// is drawn with. The rules and the drawing run outside the catches. What the library throws at a step ends the
    /// read (<see cref="DiagramReadException"/>, the export's failure).</remarks>
    internal static DiagramModel BuildModel(LoadedModule assembly, MetadataModule main, IProgress<ExportProgress>? progress, CancellationToken cancellationToken)
    {
        var picked = ValueOf(ReadPicked(main));
        var names = CreateNames(picked);
        var docs = ValueOf(ReadDocs(LoadedAssemblies.GetFile(assembly)));
        var types = new DiagramTypes { Names = names, Docs = docs, Members = new MermaidMembers { Names = names } };
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

    // The module's types the diagram draws: each but those the compiler generated and those nested in them, by the
    // library's own test, with the names the diagram groups, orders and identifies them by.
    private static Outcome<PickedType[]> ReadPicked(MetadataModule main)
    {
        try
        {
            var picked = main.TypeDefinitions
                .Where(static t => !t.IsCompilerGeneratedOrIsInCompilerGeneratedClass())
                .Select(static t => new PickedType { Type = t, Name = t.Name, Namespace = t.Namespace, FullName = t.FullName })
                .ToArray();
            return new Outcome<PickedType[]>.Ok(picked);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<PickedType[]>.Failed(e);
        }
    }

    private sealed class PickedType
    {
        public required ITypeDefinition Type { get; init; }

        public required string Name { get; init; }

        public required string Namespace { get; init; }

        public required string FullName { get; init; }
    }

    private static Dictionary<string, DiagramType> BuildNamespace(IEnumerable<PickedType> group, DiagramNames names, DiagramTypes types,
        BuiltTypes built, CancellationToken cancellationToken)
    {
        var ordered = group.OrderBy(static t => t.FullName, StringComparer.InvariantCulture);
        var byId = new Dictionary<string, DiagramType>();
        foreach (var type in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byId[ClassDiagrams.GetId(names, type.Type)] = ClassDiagrams.BuildType(types, type.Type);
            CountBuilt(built);
        }
        return byId;
    }

    private static string BuildPage(DiagramRead diagram)
    {
        var json = JsonSerializer.Serialize(diagram.Model, DiagramJson.Default.DiagramModel);
        var template = ReadAsset(TemplateName);
        return template
            .Replace("{{SourceAssemblyName}}", WebUtility.HtmlEncode(diagram.AssemblyName))
            .Replace("{{SourceAssemblyVersion}}", WebUtility.HtmlEncode(diagram.AssemblyVersion))
            .Replace("{{BuilderVersion}}", DecompilerVersionInfo.FullVersionWithCommitHash)
            .Replace("{{RepoUrl}}", RepoUrl)
            .Replace("{{Model}}", json);
    }

    private static string ReadAsset(string name)
    {
        var asset = OpenAsset(name);
        using var reader = new StreamReader(asset);
        return reader.ReadToEnd();
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

    // The names and ids of the types of a diagram.
    private static DiagramNames CreateNames(IReadOnlyCollection<PickedType> picked)
    {
        return new DiagramNames { Picked = new HashSet<IType>(picked.Select(static p => p.Type)), Ids = CreateIds(picked) };
    }

    internal static bool IsPicked(DiagramNames naming, IType type)
    {
        return naming.Picked.Contains(type);
    }

    private static Dictionary<IType, string> CreateIds(IEnumerable<PickedType> types)
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
    private static void AddIds(Dictionary<IType, string> ids, IGrouping<string, PickedType> group)
    {
        var types = group.ToList();
        if (types.Count == 1)
        {
            ids[types[0].Type] = Sanitize(group.Key);
            return;
        }
        for (var i = 0; i < types.Count; i++)
        {
            ids[types[i].Type] = types[i].Name + (i + 1);
        }
    }

    // A type in the main module of an executable is named <Module>.
    private static string Sanitize(string typeName)
    {
        return typeName.Replace('<', '_').Replace('>', '_');
    }

    // The id of a type the diagram draws, which it was given when the diagram's types were picked.
    private static string GetId(DiagramNames naming, ITypeDefinition type)
    {
        return naming.Ids[type];
    }

    private static string GetId(DiagramNames naming, TypeName type)
    {
        return IdentifyType(naming, type).Id;
    }

    private static (string Id, TypeName? OpenGeneric) IdentifyType(DiagramNames naming, TypeName type)
    {
        var openGeneric = type.OpenGeneric;
        var identified = openGeneric ?? type;
        if (naming.Ids.TryGetValue(identified.Type, out var id))
        {
            return (id, openGeneric);
        }

        var created = CreateOutsideId(naming, identified);
        naming.Ids[identified.Type] = created;
        return (created, openGeneric);
    }

    // A type outside the diagram is told apart by its full name, and generic overloads by their
    // type parameters.
    private static string CreateOutsideId(DiagramNames naming, TypeName type)
    {
        var id = Sanitize(type.FullName.Replace('.', '_'));
        if (type.TypeParameterCount == 0)
        {
            return id;
        }

        var parameters = type.TypeParameters.Select(p => GetId(naming, p));
        return id + "_" + string.Join("_", parameters);
    }

    // How a type reads in the diagram: C# keywords for built-in types, a nested type after the type declaring it,
    // generic arguments in ❰ ❱ as mermaid can't take angle brackets.
    private static string GetTypeName(DiagramNames naming, TypeName type)
    {
        if (naming.NamesByType.TryGetValue(type.Type, out var name))
        {
            return name;
        }

        var created = CreateName(naming, type);
        naming.NamesByType[type.Type] = created;
        return created;
    }

    private static string CreateName(DiagramNames naming, TypeName type)
    {
        if (type.TypeParameterCount < 1)
        {
            return CreatePlainName(naming, type);
        }

        if (type.NullableUnderlying is not null)
        {
            return GetTypeName(naming, type.NullableUnderlying) + "?";
        }

        var arguments = type.TypeArguments.Select(a => GetTypeName(naming, a));
        return type.Name + "❰" + string.Join(", ", arguments) + "❱";
    }

    private static string CreatePlainName(DiagramNames naming, TypeName type)
    {
        if (type.ArrayElement is not null)
        {
            return GetTypeName(naming, type.ArrayElement) + "[]";
        }

        if (type.ReferenceElement is not null)
        {
            return "&" + GetTypeName(naming, type.ReferenceElement);
        }

        if (!type.HasDefinition)
        {
            return type.Name;
        }

        if (type.KnownTypeCode != KnownTypeCode.None)
        {
            return GetKeyword(type.KnownTypeCode, type);
        }

        if (type.DeclaringTypeName is not null)
        {
            return type.DeclaringTypeName + '+' + type.Name;
        }

        return type.Name.Replace('<', '❰').Replace('>', '❱');
    }

    private static string GetKeyword(KnownTypeCode code, TypeName type)
    {
        var keyword = KnownTypeReference.GetCSharpNameByTypeCode(code);
        if (keyword is null)
        {
            return type.Name;
        }

        return keyword;
    }

    private static DiagramReference ReferTo(DiagramNames naming, TypeName type, string? propertyName = null)
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

    private static void RecordOutside(DiagramNames naming, string id, TypeName type)
    {
        if (IsPicked(naming, type.Type))
        {
            return;
        }

        naming.OutsideReferences.TryAdd(id, type.Namespace + '.' + GetTypeName(naming, type));
    }

    private static string FormatMethod(MermaidMembers mermaid, DrawnMethod method)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => FormatParameter(mermaid, p)));
        var typeArguments = ListTypeArguments(mermaid, method);
        var returnType = ClassDiagrams.GetTypeName(mermaid.Names, method.ReturnType);
        return $"{GetVisibility(method.Accessibility)}{GetMethodName(mermaid, method)}{typeArguments}({parameters}){GetModifier(method.Modifiers)} {returnType}";
    }

    private static string FormatParameter(MermaidMembers mermaid, DrawnParameter parameter)
    {
        return ClassDiagrams.GetTypeName(mermaid.Names, parameter.Type) + " " + parameter.Name;
    }

    private static string ListTypeArguments(MermaidMembers mermaid, DrawnMethod method)
    {
        if (method.TypeArguments.Length == 0)
        {
            return "";
        }

        var arguments = method.TypeArguments.Select(a => GetTypeName(mermaid.Names, a));
        return "❰" + string.Join(", ", arguments) + "❱";
    }

    // An explicit interface implementation is named after the interface member it implements.
    private static string GetMethodName(MermaidMembers mermaid, DrawnMethod method)
    {
        if (method.Implemented is null)
        {
            return method.Name;
        }

        return ClassDiagrams.GetTypeName(mermaid.Names, method.Implemented.DeclaringType) + '.' + method.Implemented.Name;
    }

    private static string FormatProperty(MermaidMembers mermaid, DrawnProperty property)
    {
        var type = ClassDiagrams.GetTypeName(mermaid.Names, property.ReturnType);
        return $"{GetVisibility(property.Accessibility)}{type} {property.Name}{GetModifier(property.Modifiers)}";
    }

    private static string FormatField(MermaidMembers mermaid, DrawnField field)
    {
        var type = ClassDiagrams.GetTypeName(mermaid.Names, field.ReturnType);
        return $"{GetVisibility(field.Accessibility)}{type} {field.Name}{GetModifier(field.Modifiers)}";
    }

    private static string GetModifier(MemberModifiers modifiers)
    {
        if (modifiers.IsAbstract)
        {
            return "*";
        }

        if (modifiers.IsStatic)
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

    // A type's card, a step at a time: its members read with what the app's rules of what it shows ask of them, the rules
    // applied, then what the members shown are drawn with read, and the card drawn.
    private static DiagramType BuildType(DiagramTypes diagram, ITypeDefinition type)
    {
        var candidates = ValueOf(ReadCandidates(type));
        if (candidates.IsEnum)
        {
            var values = candidates.Fields.Where(IsEnumValue).Select(static f => f.Field).ToArray();
            var drawnEnum = ValueOf(ReadDrawnEnum(diagram, type, values));
            return BuildEnum(diagram, type, drawnEnum);
        }

        var methods = candidates.Methods.Where(IsShownMethod).Select(static m => m.Method).ToArray();
        var fields = candidates.Fields.Where(IsShownField).Select(static f => f.Field).ToArray();
        var drawn = ValueOf(ReadDrawnClass(diagram, type, candidates.Properties, methods, fields));
        return BuildClass(diagram, type, drawn);
    }

    // What the app's rules of what a card shows ask of a type's members: an enum's fields' flags, a class's properties,
    // its methods' flags and its fields' flags and the properties of their type. The library hands its member filter
    // each member as declared, before it is specialized to the type's generic arguments, and ILSpy's rules are asked of
    // that form: the filter only collects them, and each member it returns is the one collected at its place, specialized.
    private static Outcome<TypeCandidates> ReadCandidates(ITypeDefinition type)
    {
        try
        {
            var isEnum = type.Kind == TypeKind.Enum;
            var properties = isEnum ? Array.Empty<IProperty>() : type.GetProperties().ToArray();
            var declaredMethods = new List<IMethod>();
            var methods = isEnum ? Array.Empty<IMethod>() : type.GetMethods(m =>
            {
                declaredMethods.Add(m);
                return true;
            }).ToArray();
            var declaredFields = new List<IField>();
            var fields = type.GetFields(f =>
            {
                declaredFields.Add(f);
                return true;
            }).ToArray();
            return new Outcome<TypeCandidates>.Ok(new TypeCandidates
            {
                IsEnum = isEnum, Properties = properties,
                Methods = methods.Select((m, i) => ReadMethodCandidate(type, m, declaredMethods[i])).ToArray(),
                Fields = fields.Select((f, i) => ReadFieldCandidate(f, declaredFields[i], properties)).ToArray(),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<TypeCandidates>.Failed(e);
        }
    }

    private static MethodCandidate ReadMethodCandidate(ITypeDefinition type, IMethod method, IMethod declared)
    {
        return new MethodCandidate
        {
            Method = method, IsOperator = declared.IsOperator, IsCompilerGenerated = declared.IsCompilerGenerated(),
            IsDeclaredByType = declared.DeclaringTypeDefinition == type, IsDeclaredByObject = TypeFacts.IsObject(declared.DeclaringType),
            OverridesObject = TypeFacts.OverridesObject(declared),
        };
    }

    // A field's flags, with the names of the properties whose type is its own as declared, which the app's rule of a
    // backing field looks in.
    private static FieldCandidate ReadFieldCandidate(IField field, IField declared, IProperty[] properties)
    {
        return new FieldCandidate
        {
            Field = field, Name = declared.Name, IsConst = declared.IsConst, IsStatic = declared.IsStatic, IsPublic = declared.Accessibility == Accessibility.Public,
            IsHidden = CSharpDecompiler.MemberIsHidden(DecompilerFacts.GetMetadataFile(declared), declared.MetadataToken, HiddenSettings),
            SameTypedProperties = properties.Where(p => declared.ReturnType.Equals(p.ReturnType)).Select(static p => p.Name).ToArray(),
        };
    }

    private sealed class TypeCandidates
    {
        public required bool IsEnum { get; init; }

        public required IProperty[] Properties { get; init; }

        public required MethodCandidate[] Methods { get; init; }

        public required FieldCandidate[] Fields { get; init; }
    }

    private sealed class MethodCandidate
    {
        public required IMethod Method { get; init; }
        public required bool IsOperator { get; init; }
        public required bool IsCompilerGenerated { get; init; }
        public required bool IsDeclaredByType { get; init; }
        public required bool IsDeclaredByObject { get; init; }
        public required bool OverridesObject { get; init; }
    }

    private sealed class FieldCandidate
    {
        public required IField Field { get; init; }
        public required string Name { get; init; }
        public required bool IsConst { get; init; }
        public required bool IsStatic { get; init; }
        public required bool IsPublic { get; init; }
        public required bool IsHidden { get; init; }
        public required string[] SameTypedProperties { get; init; }
    }

    private static bool IsEnumValue(FieldCandidate field)
    {
        if (!field.IsConst)
        {
            return false;
        }

        if (!field.IsStatic)
        {
            return false;
        }

        return field.IsPublic;
    }

    private static bool IsShownMethod(MethodCandidate method)
    {
        if (method.IsOperator)
        {
            return false;
        }

        if (method.IsCompilerGenerated)
        {
            return false;
        }

        if (method.IsDeclaredByType)
        {
            return true;
        }

        if (method.IsDeclaredByObject)
        {
            return false;
        }

        return !method.OverridesObject;
    }

    private static bool IsShownField(FieldCandidate field)
    {
        if (field.IsHidden)
        {
            return false;
        }

        return !field.SameTypedProperties.Any(p => IsBackingField(field.Name, p));
    }

    // A field of a property's type whose name holds the property's, as _name does for Name.
    private static bool IsBackingField(string field, string property)
    {
        return field.Contains(property, StringComparison.OrdinalIgnoreCase);
    }

    // What an enum's card is drawn with: its name, its values' names and the documentation of both.
    private static Outcome<DrawnEnum> ReadDrawnEnum(DiagramTypes diagram, ITypeDefinition type, IField[] values)
    {
        try
        {
            return new Outcome<DrawnEnum>.Ok(new DrawnEnum
            {
                Self = ReadName(type), Values = values.Select(static f => f.Name).ToArray(), Docs = ReadDocTexts(diagram.Docs, type, values),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DrawnEnum>.Failed(e);
        }
    }

    private sealed class DrawnEnum
    {
        public required TypeName Self { get; init; }

        public required string[] Values { get; init; }

        public required DocTexts? Docs { get; init; }
    }

    private static DiagramType BuildEnum(DiagramTypes diagram, ITypeDefinition type, DrawnEnum drawn)
    {
        var values = drawn.Values.Prepend("<<Enumeration>>");
        var id = ClassDiagrams.GetId(diagram.Names, type);
        return new DiagramType
        {
            Name = FindNameUnlessId(diagram, drawn.Self, id),
            Body = WriteDefinition(id, PadMembers(values).TrimEnd(' ')),
            XmlDocs = ClassDiagrams.CollectDocs(drawn.Docs),
        };
    }

    // What a class's card is drawn with: its name and kind, its members shown with the types they name, its base types,
    // and the documentation of it and its members.
    private static Outcome<DrawnClass> ReadDrawnClass(DiagramTypes diagram, ITypeDefinition type, IProperty[] properties, IMethod[] methods, IField[] fields)
    {
        try
        {
            var documented = fields.Concat<IMember>(properties).Concat(methods);
            return new Outcome<DrawnClass>.Ok(new DrawnClass
            {
                Self = ReadName(type), IsInterface = TypeFacts.IsInterface(type), IsAbstract = type.IsAbstract, IsSealed = type.IsSealed,
                Properties = properties.Select(ReadProperty).ToArray(), Methods = methods.Select(ReadMethod).ToArray(), Fields = fields.Select(ReadField).ToArray(),
                Ancestors = type.GetNonInterfaceBaseTypes().Select(t => ReadAncestor(type, t)).ToArray(),
                DirectBases = type.DirectBaseTypes.Select(ReadBase).ToArray(),
                Docs = ReadDocTexts(diagram.Docs, type, documented),
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DrawnClass>.Failed(e);
        }
    }

    private static DrawnProperty ReadProperty(IProperty property)
    {
        return new DrawnProperty
        {
            DeclaringType = property.DeclaringType, Name = property.Name, ReturnType = ReadName(property.ReturnType), IsIndexer = property.IsIndexer,
            IndexerParameters = property.IsIndexer ? property.Parameters.Select(static p => p.Type.Name + " " + p.Name).ToArray() : [],
            Accessibility = property.Accessibility, Modifiers = ReadModifiers(property), ElementType = TypeFacts.TryGetElementType(property, out var elementType) ? ReadName(elementType) : null,
        };
    }

    private static DrawnMethod ReadMethod(IMethod method)
    {
        var implemented = method.IsExplicitInterfaceImplementation ? method.ExplicitlyImplementedInterfaceMembers.FirstOrDefault() : null;
        return new DrawnMethod
        {
            DeclaringType = method.DeclaringType, Name = method.Name, ReturnType = ReadName(method.ReturnType), Accessibility = method.Accessibility,
            Modifiers = ReadModifiers(method), Parameters = method.Parameters.Select(static p => new DrawnParameter { Type = ReadName(p.Type), Name = p.Name }).ToArray(),
            TypeArguments = method.TypeArguments.Select(ReadName).ToArray(),
            Implemented = implemented is null ? null : new ImplementedMember { DeclaringType = ReadName(implemented.DeclaringType), Name = implemented.Name },
        };
    }

    private static DrawnField ReadField(IField field)
    {
        return new DrawnField
        {
            DeclaringType = field.DeclaringType, Name = field.Name, ReturnType = ReadName(field.ReturnType), Accessibility = field.Accessibility,
            Modifiers = ReadModifiers(field),
        };
    }

    private static MemberModifiers ReadModifiers(IMember member)
    {
        return new MemberModifiers { IsAbstract = member.IsAbstract, IsStatic = member.IsStatic };
    }

    // A base class of the type, or the type itself, with whether it is the type and whether it is object.
    private static AncestorRead ReadAncestor(ITypeDefinition type, IType other)
    {
        return new AncestorRead { Type = ReadName(other), IsSelf = other.Equals(type), IsObject = TypeFacts.IsObject(other) };
    }

    private static BaseRead ReadBase(IType type)
    {
        return new BaseRead { Type = ReadName(type), IsInterface = TypeFacts.IsInterface(type), IsObject = TypeFacts.IsObject(type) };
    }

    // What the diagram names a type with, read out of the library: the type itself, kept as the key of its id and its
    // name, and each fact the app's naming reads, the types it is made of read the same way.
    private static TypeName ReadName(IType type)
    {
        var definition = type.GetDefinition();
        var hasNullable = type.TypeParameterCount > 0 && type.IsKnownType(KnownTypeCode.NullableOfT);
        return new TypeName
        {
            Type = type, Name = type.Name, FullName = type.FullName, Namespace = type.Namespace, TypeParameterCount = type.TypeParameterCount,
            TypeArguments = type.TypeArguments.Select(ReadName).ToArray(), TypeParameters = type.TypeParameters.Select(static p => ReadName(p)).ToArray(),
            NullableUnderlying = hasNullable ? ReadName(type.TypeArguments.Single()) : null,
            ArrayElement = type is ArrayType array ? ReadName(array.ElementType) : null,
            ReferenceElement = type is ByReferenceType reference ? ReadName(reference.ElementType) : null,
            HasDefinition = definition is not null, KnownTypeCode = definition?.KnownTypeCode ?? KnownTypeCode.None, DeclaringTypeName = type.DeclaringType?.Name,
            OpenGeneric = ReadOpenGeneric(type),
        };
    }

    // The open generic type a closed one is made from; none for a type that is no closed generic.
    private static TypeName? ReadOpenGeneric(IType type)
    {
        if (type is not ParameterizedType generic)
        {
            return null;
        }

        if (generic.TypeArguments.Any(static a => a is ITypeParameter))
        {
            return null;
        }

        return ReadName(generic.GenericType);
    }

    private sealed class TypeName
    {
        public required IType Type { get; init; }
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public required string Namespace { get; init; }
        public required int TypeParameterCount { get; init; }
        public required TypeName[] TypeArguments { get; init; }
        public required TypeName[] TypeParameters { get; init; }
        public required TypeName? NullableUnderlying { get; init; }
        public required TypeName? ArrayElement { get; init; }
        public required TypeName? ReferenceElement { get; init; }
        public required bool HasDefinition { get; init; }
        public required KnownTypeCode KnownTypeCode { get; init; }
        public required string? DeclaringTypeName { get; init; }
        public required TypeName? OpenGeneric { get; init; }
    }

    private sealed class DrawnClass
    {
        public required TypeName Self { get; init; }
        public required bool IsInterface { get; init; }
        public required bool IsAbstract { get; init; }
        public required bool IsSealed { get; init; }
        public required DrawnProperty[] Properties { get; init; }
        public required DrawnMethod[] Methods { get; init; }
        public required DrawnField[] Fields { get; init; }
        public required AncestorRead[] Ancestors { get; init; }
        public required BaseRead[] DirectBases { get; init; }
        public required DocTexts? Docs { get; init; }
    }

    private sealed class DrawnProperty
    {
        public required IType DeclaringType { get; init; }
        public required string Name { get; init; }
        public required TypeName ReturnType { get; init; }
        public required bool IsIndexer { get; init; }
        public required string[] IndexerParameters { get; init; }
        public required Accessibility Accessibility { get; init; }
        public required MemberModifiers Modifiers { get; init; }
        public required TypeName? ElementType { get; init; }
    }

    private sealed class DrawnMethod
    {
        public required IType DeclaringType { get; init; }
        public required string Name { get; init; }
        public required TypeName ReturnType { get; init; }
        public required Accessibility Accessibility { get; init; }
        public required MemberModifiers Modifiers { get; init; }
        public required DrawnParameter[] Parameters { get; init; }
        public required TypeName[] TypeArguments { get; init; }
        public required ImplementedMember? Implemented { get; init; }
    }

    private sealed class DrawnParameter
    {
        public required TypeName Type { get; init; }
        public required string Name { get; init; }
    }

    private sealed class ImplementedMember
    {
        public required TypeName DeclaringType { get; init; }
        public required string Name { get; init; }
    }

    private sealed class DrawnField
    {
        public required IType DeclaringType { get; init; }
        public required string Name { get; init; }
        public required TypeName ReturnType { get; init; }
        public required Accessibility Accessibility { get; init; }
        public required MemberModifiers Modifiers { get; init; }
    }

    private readonly record struct MemberModifiers
    {
        public required bool IsAbstract { get; init; }
        public required bool IsStatic { get; init; }
    }

    private sealed class AncestorRead
    {
        public required TypeName Type { get; init; }
        public required bool IsSelf { get; init; }
        public required bool IsObject { get; init; }
    }

    private sealed class BaseRead
    {
        public required TypeName Type { get; init; }
        public required bool IsInterface { get; init; }
        public required bool IsObject { get; init; }
    }

    private static DiagramType BuildClass(DiagramTypes diagram, ITypeDefinition type, DrawnClass drawn)
    {
        var id = ClassDiagrams.GetId(diagram.Names, type);
        var members = SplitByDeclaringType(diagram, drawn);
        return new DiagramType
        {
            Name = FindNameUnlessId(diagram, drawn.Self, id),
            Body = WriteDefinition(id, BuildBody(diagram, drawn, members)),
            HasOne = MapHasOne(diagram, members, type),
            HasMany = MapHasMany(diagram, members, type),
            BaseType = MapBaseType(diagram, drawn),
            Interfaces = MapInterfaces(diagram, drawn),
            Inherited = ListInherited(diagram, drawn, id, members),
            XmlDocs = ClassDiagrams.CollectDocs(drawn.Docs),
        };
    }

    private static string? FindNameUnlessId(DiagramTypes diagram, TypeName type, string id)
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

    private static DeclaredMembers SplitByDeclaringType(DiagramTypes diagram, DrawnClass drawn)
    {
        var properties = drawn.Properties;
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
            FlatProperties = GroupByDeclaringType(flat, static p => p.DeclaringType),
            HasOne = GroupByDeclaringType(hasOne, static p => p.DeclaringType),
            HasMany = GroupByDeclaringType(hasMany, static r => r.Property.DeclaringType),
            Methods = GroupByDeclaringType(drawn.Methods, static m => m.DeclaringType),
            Fields = GroupByDeclaringType(drawn.Fields, static f => f.DeclaringType),
        };
    }

    private static Dictionary<IType, T[]> GroupByDeclaringType<T>(IEnumerable<T> items, Func<T, IType> declaringOf)
    {
        return items
            .GroupBy(declaringOf)
            .ToDictionary(static g => g.Key, static g => g.ToArray());
    }

    // A property whose type, or the type it makes nullable, is drawn in the diagram.
    private static bool HoldsOne(DiagramTypes diagram, DrawnProperty property)
    {
        var type = property.ReturnType.NullableUnderlying ?? property.ReturnType;
        return ClassDiagrams.IsPicked(diagram.Names, type.Type);
    }

    // A property of a generic enumerable of a type drawn in the diagram.
    private static ManyRelation? FindManyRelation(DiagramTypes diagram, DrawnProperty property)
    {
        if (property.ElementType is null)
        {
            return null;
        }

        if (!ClassDiagrams.IsPicked(diagram.Names, property.ElementType.Type))
        {
            return null;
        }

        return new ManyRelation { Property = property, ElementType = property.ElementType };
    }

    private static string BuildBody(DiagramTypes diagram, DrawnClass drawn, DeclaredMembers members)
    {
        var own = PadMembers(ListFlatMembers(diagram, members, drawn.Self.Type, ""));
        var annotation = FindAnnotation(drawn);
        if (annotation is null)
        {
            return own.TrimEnd(' ');
        }

        return own + $"<<{annotation}>>" + NewLine;
    }

    // See https://mermaid.js.org/syntax/classDiagram.html#annotations-on-classes; a static class is
    // abstract and sealed.
    private static string? FindAnnotation(DrawnClass drawn)
    {
        if (drawn.IsInterface)
        {
            return "Interface";
        }

        if (!drawn.IsAbstract)
        {
            return null;
        }

        if (drawn.IsSealed)
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

    // Inherited members are kept apart by the base type declaring them, so the diagrammer can leave out those a base
    // type it draws already shows, and are written as members of id for it to add.
    private static Dictionary<string, InheritedMembers> ListInherited(DiagramTypes diagram, DrawnClass drawn, string id, DeclaredMembers members)
    {
        var prefix = id + " : ";
        var inherited = new Dictionary<string, InheritedMembers>();
        var ancestors = drawn.Ancestors.Where(static a => IsAncestor(a));
        foreach (var ancestor in ancestors)
        {
            inherited[ClassDiagrams.GetId(diagram.Names, ancestor.Type)] = GetInheritedFrom(diagram, members, ancestor.Type.Type, prefix);
        }

        return inherited;
    }

    // One of the type's base classes, or the type itself, that is a base class of it other than object.
    private static bool IsAncestor(AncestorRead other)
    {
        if (other.IsSelf)
        {
            return false;
        }

        return !other.IsObject;
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

    private static void RelateOne(DiagramTypes diagram, Dictionary<string, string> relations, DrawnProperty property)
    {
        var type = property.ReturnType;
        var label = property.Name;
        if (property.IsIndexer)
        {
            label += "[" + string.Join(", ", property.IndexerParameters) + "]";
        }

        if (type.NullableUnderlying is not null)
        {
            type = type.NullableUnderlying;
            label += " ?";
        }
        Relate(relations, label, ClassDiagrams.ReferTo(diagram.Names, type, label));
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

    private static Dictionary<string, string?>? MapBaseType(DiagramTypes diagram, DrawnClass drawn)
    {
        var baseType = drawn.DirectBases.FirstOrDefault(static b => IsBaseClass(b));
        if (baseType is null)
        {
            return null;
        }

        var reference = ClassDiagrams.ReferTo(diagram.Names, baseType.Type);
        return new Dictionary<string, string?> { [reference.To] = reference.Label };
    }

    private static bool IsBaseClass(BaseRead type)
    {
        if (type.IsInterface)
        {
            return false;
        }

        return !type.IsObject;
    }

    private static Dictionary<string, string?[]>? MapInterfaces(DiagramTypes diagram, DrawnClass drawn)
    {
        var references = drawn.DirectBases
            .Where(static b => b.IsInterface)
            .Select(i => ClassDiagrams.ReferTo(diagram.Names, i.Type))
            .ToList();
        if (references.Count == 0)
        {
            return null;
        }

        // The closed generic forms of one interface share its open one's id; each keeps its label.
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

    // The documentation the file has, wherever it is found; none when it has none or it cannot be read; what else the
    // library throws finding it, the diagram's failed read.
    private static Outcome<DiagramDocs> ReadDocs(MetadataFile file)
    {
        try
        {
            return new Outcome<DiagramDocs>.Ok(new DiagramDocs { Provider = XmlDocLoader.LoadDocumentation(file) });
        }
        catch (XmlException)
        {
            return new Outcome<DiagramDocs>.Ok(new DiagramDocs { Provider = null });
        }
        catch (IOException)
        {
            return new Outcome<DiagramDocs>.Ok(new DiagramDocs { Provider = null });
        }
        catch (UnauthorizedAccessException)
        {
            return new Outcome<DiagramDocs>.Ok(new DiagramDocs { Provider = null });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return new Outcome<DiagramDocs>.Failed(e);
        }
    }

    // The documentation's XML of the type and of each of the members, by the member's name; none when the assembly has
    // no documentation.
    private static DocTexts? ReadDocTexts(DiagramDocs docs, ITypeDefinition type, IEnumerable<IMember> members)
    {
        var provider = docs.Provider;
        if (provider is null)
        {
            return null;
        }

        return new DocTexts
        {
            Type = ReadDocXml(provider, type), Members = members.Select(m => (m.Name, ReadDocXml(provider, m))).ToArray(),
        };
    }

    private sealed class DocTexts
    {
        public required string? Type { get; init; }

        public required (string Name, string? Xml)[] Members { get; init; }
    }

    // The documentation of a type, under an empty key, and of each of its members, under its name, as plain text; null
    // when none has any, or the assembly has no documentation.
    private static Dictionary<string, string>? CollectDocs(DocTexts? docs)
    {
        if (docs is null)
        {
            return null;
        }

        var found = new Dictionary<string, string>();
        AddDoc(found, string.Empty, docs.Type);
        foreach (var (name, xml) in docs.Members)
        {
            AddDoc(found, name, xml);
        }

        if (found.Count == 0)
        {
            return null;
        }

        return found;
    }

    private static void AddDoc(Dictionary<string, string> found, string key, string? xml)
    {
        var text = FindDocText(xml);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        found[key] = text;
    }

    private static string? FindDocText(string? xml)
    {
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

    // The XML documentation of an assembly's types and members as the diagrammer reads it; no provider when it has none.
    private sealed class DiagramDocs
    {
        public required IDocumentationProvider? Provider { get; init; }
    }

    // Members as lines of a mermaid class definition: visibility, then type and name, with * marking an abstract member
    // and $ a static one (https://mermaid.js.org/syntax/classDiagram.html).
    private sealed class MermaidMembers
    {
        public required DiagramNames Names { get; init; }
    }

    private readonly record struct DiagramReference
    {
        public required string To { get; init; }

        public required string? Label { get; init; }
    }

    private sealed class DiagramTypes
    {
        public required DiagramNames Names { get; init; }

        public required DiagramDocs Docs { get; init; }

        public required MermaidMembers Members { get; init; }
    }

    private readonly record struct ManyRelation
    {
        public required DrawnProperty Property { get; init; }

        public required TypeName ElementType { get; init; }
    }

    private sealed record DeclaredMembers
    {
        public required Dictionary<IType, DrawnProperty[]> FlatProperties { get; init; }

        public required Dictionary<IType, DrawnProperty[]> HasOne { get; init; }

        public required Dictionary<IType, ManyRelation[]> HasMany { get; init; }

        public required Dictionary<IType, DrawnMethod[]> Methods { get; init; }

        public required Dictionary<IType, DrawnField[]> Fields { get; init; }
    }
}
