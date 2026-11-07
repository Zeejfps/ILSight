using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>A search hit: the <see cref="Node"/> found, its label, and where it lives.</summary>
/// <param name="Location">Where it lives, as ILSpy's results show it: the namespace and enclosing
/// type of a type or member, <c>-</c> for the global namespace, the resources file holding a
/// resource entry, an assembly's file; empty for a namespace or a resource of its own.</param>
/// <param name="Fitness">How closely the hit matches, as ILSpy ranks it: the shorter the name the
/// closer, and 0 for compiler-generated names.</param>
public sealed record SearchResult(BrowseNode Node, string Text, string Location, float Fitness);

/// <summary>Searches assemblies for the nodes of a <see cref="BrowseSession"/> a query finds.</summary>
public static class AssemblySearch
{
    /// <summary>Searches <paramref name="assemblies"/> for what <paramref name="query"/> asks, on a
    /// background thread. Results arrive in batches as they are found.</summary>
    public static IAsyncEnumerable<IReadOnlyList<SearchResult>> SearchAsync(BrowseSession session, IEnumerable<LoadedModule> assemblies,
        SearchQuery query, CancellationToken cancellationToken = default)
    {
        var searched = assemblies.ToImmutableArray();
        return Batches.Stream<SearchResult>((emit, token) =>
        {
            if (query.IsEmpty) return;
            var search = new Search { Session = session, Query = query, Emit = emit, CancellationToken = token };
            foreach (var assembly in searched)
                AssemblySearch.SearchAssembly(search, assembly);
        }, cancellationToken);
    }

    private sealed class Search
    {
        public required BrowseSession Session { get; init; }

        public required SearchQuery Query { get; init; }

        public required Action<SearchResult> Emit { get; init; }

        public required CancellationToken CancellationToken { get; init; }

        public readonly HashSet<BrowseNode> Found = new(ReferenceEqualityComparer.Instance);
}

        private static void SearchAssembly(Search search, LoadedModule assembly)
        {
            if (search.Query.InAssembly != null && !IsInAssembly(assembly, search.Query.InAssembly)) return;
            var kinds = search.Query.Kinds;
            Scan(kinds, SearchKinds.Assemblies, a => MatchAssembly(search, a), assembly);
            Scan(kinds, SearchKinds.Namespaces, a => ScanNamespaces(search, a), assembly);
            Scan(kinds, SearchKinds.TypesAndMembers, a => ScanEntities(search, a), assembly);
            Scan(kinds, SearchKinds.Tokens, a => FindToken(search, a), assembly);
            Scan(kinds, SearchKinds.Literals, a => ScanLiterals(search, a), assembly);
            Scan(kinds, SearchKinds.Resources, a => ScanResources(search, a), assembly);
        }

        private static void Scan(SearchKinds kinds, SearchKinds wanted, Action<LoadedModule> scan, LoadedModule assembly)
        {
            if ((kinds & wanted) == 0) return;
            try
            {
                scan(assembly);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
            }
        }

        private static bool IsInAssembly(LoadedModule assembly, string scope)
    {
        return assembly.FilePath.FileName.Contains(scope, StringComparison.OrdinalIgnoreCase) ||
                assembly.FullName.Contains(scope, StringComparison.OrdinalIgnoreCase);
    }

        private static bool IsInNamespace(Search search, string ns)
    {
        return search.Query.InNamespace == null || (search.Query.InNamespace.Length == 0 ? ns.Length == 0 : ns.Contains(search.Query.InNamespace, StringComparison.OrdinalIgnoreCase));
    }

        private static void ScanEntities(Search search, LoadedModule assembly)
        {
            var reader = assembly.File.Metadata;
            var kinds = search.Query.Kinds;
            var types = (kinds & SearchKinds.Types) != 0;
            var methods = (kinds & SearchKinds.Methods) != 0;
            var fields = (kinds & SearchKinds.Fields) != 0;
            var properties = (kinds & SearchKinds.Properties) != 0;
            var events = (kinds & SearchKinds.Events) != 0;
            foreach (var typeHandle in reader.TypeDefinitions)
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                var type = reader.GetTypeDefinition(typeHandle);
                if (!IsInNamespace(search, ReadNamespace(reader, type))) continue;
                var typeName = search.Query.FullName ? ReadTypeName(search, reader, typeHandle) : ReadTypeLabel(search, reader, typeHandle);
                if (types && SearchQueries.IsMatch(search.Query, typeName))
                    AddResult(search, assembly, typeHandle);
                if (fields)
                {
                    foreach (var handle in type.GetFields())
                        MatchMember(search, assembly, typeName, ReadName(search, reader, reader.GetFieldDefinition(handle).Name), handle);
                }
                if (properties)
                {
                    foreach (var handle in type.GetProperties())
                        MatchMember(search, assembly, typeName, ReadName(search, reader, reader.GetPropertyDefinition(handle).Name), handle);
                }
                if (events)
                {
                    foreach (var handle in type.GetEvents())
                        MatchMember(search, assembly, typeName, ReadName(search, reader, reader.GetEventDefinition(handle).Name), handle);
                }
                if (methods)
                {
                    foreach (var handle in type.GetMethods())
                    {
                        var method = reader.GetMethodDefinition(handle);
                        if (!reader.StringComparer.StartsWith(method.Name, "."))
                            MatchMember(search, assembly, typeName, AddTypeParameters(search, reader, ReadName(search, reader, method.Name), method.GetGenericParameters(), 0), handle);
                    }
                }
            }
        }

        private static void MatchMember(Search search, LoadedModule assembly, string typeName, string name, EntityHandle handle)
        {
            if (SearchQueries.IsMatch(search.Query, search.Query.FullName ? typeName + "." + name : name))
                AddResult(search, assembly, handle);
        }

        private static string ReadName(Search search, MetadataReader reader, StringHandle handle)
        {
            var name = reader.GetString(handle);
            if (search.Query.KeepArity && !search.Query.Generics) return name;
            var tick = name.IndexOf('`');
            return tick < 0 ? name : name[..tick];
        }

        private static string ReadTypeLabel(Search search, MetadataReader reader, TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = ReadName(search, reader, type.Name);
            if (!search.Query.Generics) return name;
            var declaring = type.GetDeclaringType();
            var inherited = declaring.IsNil ? 0 : reader.GetTypeDefinition(declaring).GetGenericParameters().Count;
            return AddTypeParameters(search, reader, name, type.GetGenericParameters(), inherited);
        }

        private static string AddTypeParameters(Search search, MetadataReader reader, string name, GenericParameterHandleCollection parameters, int inherited)
        {
            if (!search.Query.Generics || parameters.Count <= inherited) return name;
            var text = new StringBuilder(name).Append('<');
            for (var i = inherited; i < parameters.Count; i++)
            {
                if (i > inherited) text.Append(", ");
                text.Append(reader.GetString(reader.GetGenericParameter(parameters[i]).Name));
            }
            return text.Append('>').ToString();
        }

        private static string ReadTypeName(Search search, MetadataReader reader, TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = ReadTypeLabel(search, reader, handle);
            var declaring = type.GetDeclaringType();
            if (!declaring.IsNil) return ReadTypeName(search, reader, declaring) + "." + name;
            var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        private static string ReadNamespace(MetadataReader reader, TypeDefinition type)
        {
            while (true)
            {
                var declaring = type.GetDeclaringType();
                if (declaring.IsNil) return reader.GetString(type.Namespace);
                type = reader.GetTypeDefinition(declaring);
            }
        }

        private static void ScanLiterals(Search search, LoadedModule assembly)
        {
            var file = assembly.File;
            var reader = file.Metadata;
            var literal = SearchQueries.GetLiteral(search.Query);
            foreach (var handle in reader.MethodDefinitions)
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                var method = reader.GetMethodDefinition(handle);
                if (!method.HasBody() || !IsInNamespace(search, ReadNamespace(reader, reader.GetTypeDefinition(method.GetDeclaringType())))) continue;
                BlobReader body;
                try
                {
                    body = file.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
                if (HasMatchInBody(search, ref body, reader, literal))
                    AddResultOrAncestor(search, assembly, handle);
            }
            foreach (var handle in reader.FieldDefinitions)
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                var field = reader.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.Literal) == 0) continue;
                var constantHandle = field.GetDefaultValue();
                if (constantHandle.IsNil || !IsInNamespace(search, ReadNamespace(reader, reader.GetTypeDefinition(field.GetDeclaringType())))) continue;
                var constant = reader.GetConstant(constantHandle);
                var blob = reader.GetBlobReader(constant.Value);
                if (IsConstantMatch(search, blob.ReadConstant(constant.TypeCode), literal))
                    AddResult(search, assembly, handle);
            }
        }

        private static bool IsConstantMatch(Search search, object? value, object? literal)
    {
        return (value, literal) switch
            {
                (null, _) => false,
                (string or char or bool, long or float or double) => false,
                (_, long number) => value is not float and not double && Convert.ToInt64(value is ulong u ? (long)u : value) == number,
                (_, float single) => value is float f && f == single,
                (_, double real) => value is double d && d == real,
                _ => SearchQueries.IsMatch(search.Query, value.ToString() ?? ""),
            };
    }

        private static bool HasMatchInBody(Search search, ref BlobReader body, MetadataReader reader, object? literal)
        {
            while (body.RemainingBytes > 0)
            {
                var code = body.DecodeOpCode();
                switch (literal)
                {
                    case long number when IsIntegerLoad(code):
                        if (ReadIntegerOperand(ref body, code) == number) return true;
                        continue;
                    case float single when code == ILOpCode.Ldc_r4:
                        if (body.ReadSingle() == single) return true;
                        continue;
                    case double real when code == ILOpCode.Ldc_r8:
                        if (body.ReadDouble() == real) return true;
                        continue;
                    case null when code == ILOpCode.Ldstr:
                        if (SearchQueries.IsMatch(search.Query, body.DecodeUserString(reader))) return true;
                        continue;
                }
                body.SkipOperand(code);
            }
            return false;
        }

        private static bool IsIntegerLoad(ILOpCode code)
    {
        return code is >= ILOpCode.Ldc_i4_m1 and <= ILOpCode.Ldc_i8;
    }

        private static long ReadIntegerOperand(ref BlobReader body, ILOpCode code)
    {
        return code switch
            {
                ILOpCode.Ldc_i4_s => body.ReadSByte(),
                ILOpCode.Ldc_i4 => body.ReadInt32(),
                ILOpCode.Ldc_i8 => body.ReadInt64(),
                _ => code - ILOpCode.Ldc_i4_0,
            };
    }

        private static void FindToken(Search search, LoadedModule assembly)
        {
            var term = search.Query.Terms.Count == 1 ? search.Query.Terms[0] : "";
            if (term.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) term = term[2..];
            if (!int.TryParse(term, System.Globalization.NumberStyles.AllowHexSpecifier, null, out var token)) return;
            var handle = MetadataTokenHelpers.EntityHandleOrNil(token);
            if (handle.IsNil) return;
            var reader = assembly.File.Metadata;
            var rows = handle.Kind switch
            {
                HandleKind.TypeDefinition => reader.TypeDefinitions.Count,
                HandleKind.MethodDefinition => reader.MethodDefinitions.Count,
                HandleKind.FieldDefinition => reader.FieldDefinitions.Count,
                HandleKind.PropertyDefinition => reader.PropertyDefinitions.Count,
                HandleKind.EventDefinition => reader.EventDefinitions.Count,
                _ => 0,
            };
            var row = MetadataTokens.GetRowNumber(handle);
            if (row >= 1 && row <= rows)
                AddResultOrAncestor(search, assembly, handle);
        }

        private static void ScanResources(Search search, LoadedModule assembly)
        {
            foreach (var child in search.Session.ChildrenOf(search.Session.RootOf(assembly)))
            {
                if (child.Subject is NodeSubject.Resources)
                    ScanResources(search, child);
            }
        }

        private static void ScanResources(Search search, BrowseNode parent)
        {
            foreach (var node in search.Session.ChildrenOf(parent))
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                if (SearchQueries.IsMatch(search.Query, node.Text))
                    AddResult(search, node, parent.Subject is NodeSubject.ResourceContainer ? parent.Text : "");
                if (search.Session.HasChildren(node))
                    ScanResources(search, node);
            }
        }

        private static void MatchAssembly(Search search, LoadedModule assembly)
        {
            var matches = search.Query.AssemblyName switch
            {
                AssemblyNameKind.FilePath => SearchQueries.IsMatch(search.Query, assembly.FilePath.Text),
                AssemblyNameKind.FullName => SearchQueries.IsMatch(search.Query, assembly.FullName),
                _ => SearchQueries.IsMatch(search.Query, assembly.Name) || SearchQueries.IsMatch(search.Query, assembly.FilePath.FileName),
            };
            if (matches)
                AddResult(search, search.Session.RootOf(assembly), assembly.FilePath.Text);
        }

        private static void ScanNamespaces(Search search, LoadedModule assembly)
        {
            foreach (var node in search.Session.ChildrenOf(search.Session.RootOf(assembly)))
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                if (IsNamespaceFound(search, node))
                    AddResult(search, node, "");
            }
        }

        /// <summary>Whether <paramref name="node"/> is a namespace the assembly puts types in that the
        /// Query finds.</summary>
        private static bool IsNamespaceFound(Search search, BrowseNode node)
        {
            if (node.Subject is not NodeSubject.Namespace ns)
                return false;
            if (ns.Value.IsEnclosingOnly)
                return false;
            return IsInNamespace(search, ns.Value.Name) && SearchQueries.IsMatch(search.Query, node.Text);
        }

        private static void AddResult(Search search, LoadedModule assembly, EntityHandle handle)
        {
            var node = FindNode(search, assembly, handle);
            if (node == null)
                return;
            if (NodeDefinitions.GetVisibility(node.Subject).IsCompilerGenerated)
                return;
            if (!BrowseNodes.IsMember(node.Parent))
                AddResult(search, node);
        }

        private static void AddResultOrAncestor(Search search, LoadedModule assembly, EntityHandle handle)
        {
            BrowseNode? node;
            try
            {
                node = search.Session.FindOrAncestor(assembly, handle);
            }
            catch (Exception e) when (e is BadImageFormatException or InvalidOperationException)
            {
                return;
            }
            node = FindSourceDeclaredAncestor(node);
            if (node != null && BrowseNodes.IsMember(node.Parent))
                node = node.Parent;
            if (node is not null)
                AddResult(search, node);
        }

        /// <summary><paramref name="node"/>, or the nearest of its ancestors the source declared
        /// when the compiler made it up, such as the type that holds a lambda's closure class.</summary>
        private static BrowseNode? FindSourceDeclaredAncestor(BrowseNode? node)
        {
            while (node != null && NodeDefinitions.GetVisibility(node.Subject).IsCompilerGenerated)
                node = node.Parent;
            return node;
        }

        private static BrowseNode? FindNode(Search search, LoadedModule assembly, EntityHandle handle)
        {
            try
            {
                return search.Session.Find(assembly, handle);
            }
            catch (Exception e) when (e is BadImageFormatException or InvalidOperationException)
            {
                return null;
            }
        }

        private static void AddResult(Search search, BrowseNode node)
        {
            var ns = node.Parent;
            while (ns != null && !IsNamespace(ns))
                ns = ns.Parent;
            var name = ns?.Text ?? "";
            var type = node.Parent;
            if (type != null && type.Subject is NodeSubject.TypeDefinition)
                AddResult(search, node, name.Length == 0 ? type.Text : $"{name}.{type.Text}");
            else
                AddResult(search, node, name.Length == 0 ? "-" : name);
        }

        private static bool IsNamespace(BrowseNode node)
    {
        return node.Subject is NodeSubject.Namespace;
    }

        private static void AddResult(Search search, BrowseNode node, string location)
        {
            if (!search.Found.Add(node)) return;
            search.Emit(new SearchResult(node, node.Text, location, GetFitness(node)));
        }

        private static float GetFitness(BrowseNode node)
        {
            var name = GetNodeName(node);
            return name.Length == 0 || name[0] == '<' ? 0f : 1f / name.Length;
        }

        /// <summary>The name a node is matched by: a constructor's its type's, an entity's its own, else its label.</summary>
        private static string GetNodeName(BrowseNode node)
        {
            var entity = BrowseNodes.GetEntity(node);
            if (entity == null)
                return node.Text;
            if (entity is IMethod method && method.IsConstructor && method.DeclaringTypeDefinition != null)
                return method.DeclaringTypeDefinition.Name;
            return entity.Name;
        }
}
