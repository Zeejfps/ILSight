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

/// <summary>Searches assemblies for the nodes of a <see cref="BrowseSession"/> a query finds.</summary>
public static class AssemblySearch
{
    /// <summary>Searches <paramref name="assemblies"/> for what <paramref name="query"/> asks, on a
    /// background thread. The hits arrive in batches as they are found, and with them each assembly the library
    /// failed on, which the search goes on past.</summary>
    public static IAsyncEnumerable<IReadOnlyList<SearchFind>> SearchAsync(BrowseSession session, IEnumerable<LoadedModule> assemblies,
        SearchQuery query, CancellationToken cancellationToken = default)
    {
        var searched = assemblies.ToImmutableArray();
        return Batches.StreamAsync<SearchFind>(async (emit, token) =>
        {
            if (!query.CanFind)
            {
                return;
            }

            var search = new Search { Session = session, Query = query, Emit = emit, CancellationToken = token };
            foreach (var assembly in searched)
            {
                var why = await AssemblySearch.TrySearchAssemblyAsync(search, assembly, token);
                if (why is not null)
                {
                    emit(new SearchFind.Unsearched { Assembly = assembly, Why = why });
                }
            }
        }, cancellationToken);
    }

    /// <summary>Searches <paramref name="assembly"/> once the read of its type system ended; why the library failed on
    /// it (that read, or the search), else null. Cancellation asked for goes through.</summary>
    private static async Task<string?> TrySearchAssemblyAsync(Search search, LoadedModule assembly, CancellationToken ct)
    {
        var read = await LoadedAssemblies.ReadTypeSystemAsync(assembly, search.CancellationToken);
        return await read.Match(
            r => TrySearchAssemblyAsync(search, assembly, r.TypeSystem.MainModule, ct),
            static u => Task.FromResult<string?>(u.Thrown.SourceException.Message));
    }

    /// <summary>Searches <paramref name="assembly"/>, whose type system read is <paramref name="main"/>'s; why the
    /// library failed on it, else null. Cancellation asked for goes through.</summary>
    private static async Task<string?> TrySearchAssemblyAsync(Search search, LoadedModule assembly, MetadataModule main, CancellationToken ct)
    {
        try
        {
            await SearchAssemblyAsync(search, assembly, main, ct);
            return null;
        }
        catch (OperationCanceledException) when (search.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private sealed class Search
    {
        public required BrowseSession Session { get; init; }

        public required SearchQuery Query { get; init; }

        public required Action<SearchFind> Emit { get; init; }

        public required CancellationToken CancellationToken { get; init; }

        public readonly HashSet<BrowseNode> Found = new(ReferenceEqualityComparer.Instance);
}

        private static async Task SearchAssemblyAsync(Search search, LoadedModule assembly, MetadataModule main, CancellationToken ct)
        {
            if (search.Query.InAssembly is not null && !IsInAssembly(assembly, search.Query.InAssembly))
        {
            return;
        }

        var kinds = search.Query.Kinds;
            await ScanAsync(kinds, SearchKinds.Assemblies, a => MatchAssemblyAsync(search, a, ct), assembly, ct);
            await ScanAsync(kinds, SearchKinds.Namespaces, a => ScanNamespacesAsync(search, a, ct), assembly, ct);
            await ScanAsync(kinds, SearchKinds.TypesAndMembers, a => ScanEntitiesAsync(search, a, main, ct), assembly, ct);
            await ScanAsync(kinds, SearchKinds.Tokens, a => FindTokenAsync(search, a, main, ct), assembly, ct);
            await ScanAsync(kinds, SearchKinds.Literals, a => ScanLiteralsAsync(search, a, main, ct), assembly, ct);
            await ScanAsync(kinds, SearchKinds.Resources, a => ScanResourcesAsync(search, a, ct), assembly, ct);
        }

        private static async Task ScanAsync(SearchKinds kinds, SearchKinds wanted, Func<LoadedModule, Task> scan, LoadedModule assembly, CancellationToken ct)
        {
            if ((kinds & wanted) == 0)
        {
            return;
        }

        await scan(assembly);
        }

        private static bool IsInAssembly(LoadedModule assembly, string scope)
    {
        return assembly.FilePath.FileName.Contains(scope, StringComparison.OrdinalIgnoreCase) ||
                assembly.FullName.Contains(scope, StringComparison.OrdinalIgnoreCase);
    }

        private static bool IsInNamespace(Search search, string ns)
    {
        return search.Query.InNamespace is null || (search.Query.InNamespace.Length == 0 ? ns.Length == 0 : ns.Contains(search.Query.InNamespace, StringComparison.OrdinalIgnoreCase));
    }

        private static async Task ScanEntitiesAsync(Search search, LoadedModule assembly, MetadataModule main, CancellationToken ct)
        {
            var reader = LoadedAssemblies.GetFile(assembly).Metadata;
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
                var namespaceName = ReadNamespace(reader, type);
                if (!IsInNamespace(search, namespaceName))
            {
                continue;
            }

            var typeName = search.Query.FullName ? ReadTypeName(search, reader, typeHandle) : ReadTypeLabel(search, reader, typeHandle);
                if (types && SearchQueries.IsMatch(search.Query, typeName))
            {
                await AddResultAsync(search, assembly, main, typeHandle, ct);
            }

            if (fields)
                {
                    foreach (var handle in type.GetFields())
                {
                    await MatchMemberAsync(search, assembly, main, typeName, ReadName(search, reader, reader.GetFieldDefinition(handle).Name), handle, ct);
                }
            }
                if (properties)
                {
                    foreach (var handle in type.GetProperties())
                {
                    await MatchMemberAsync(search, assembly, main, typeName, ReadName(search, reader, reader.GetPropertyDefinition(handle).Name), handle, ct);
                }
            }
                if (events)
                {
                    foreach (var handle in type.GetEvents())
                {
                    await MatchMemberAsync(search, assembly, main, typeName, ReadName(search, reader, reader.GetEventDefinition(handle).Name), handle, ct);
                }
            }
                if (methods)
                {
                    foreach (var handle in type.GetMethods())
                    {
                        var method = reader.GetMethodDefinition(handle);
                        if (!reader.StringComparer.StartsWith(method.Name, "."))
                    {
                        await MatchMemberAsync(search, assembly, main, typeName, AddTypeParameters(search, reader, ReadName(search, reader, method.Name), method.GetGenericParameters(), 0), handle, ct);
                    }
                }
                }
            }
        }

        private static async Task MatchMemberAsync(Search search, LoadedModule assembly, MetadataModule main, string typeName, string name, EntityHandle handle, CancellationToken ct)
        {
            if (SearchQueries.IsMatch(search.Query, search.Query.FullName ? typeName + "." + name : name))
        {
            await AddResultAsync(search, assembly, main, handle, ct);
        }
    }

        private static string ReadName(Search search, MetadataReader reader, StringHandle handle)
        {
            var name = reader.GetString(handle);
            if (search.Query.KeepArity && !search.Query.Generics)
        {
            return name;
        }

        var tick = name.IndexOf('`');
            return tick < 0 ? name : name[..tick];
        }

        private static string ReadTypeLabel(Search search, MetadataReader reader, TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = ReadName(search, reader, type.Name);
            if (!search.Query.Generics)
        {
            return name;
        }

        var declaring = type.GetDeclaringType();
            var inherited = declaring.IsNil ? 0 : reader.GetTypeDefinition(declaring).GetGenericParameters().Count;
            return AddTypeParameters(search, reader, name, type.GetGenericParameters(), inherited);
        }

        private static string AddTypeParameters(Search search, MetadataReader reader, string name, GenericParameterHandleCollection parameters, int inherited)
        {
            if (!search.Query.Generics || parameters.Count <= inherited)
        {
            return name;
        }

        var text = new StringBuilder(name).Append('<');
            for (var i = inherited; i < parameters.Count; i++)
            {
                if (i > inherited)
            {
                text.Append(", ");
            }

            text.Append(reader.GetString(reader.GetGenericParameter(parameters[i]).Name));
            }
            return text.Append('>').ToString();
        }

        private static string ReadTypeName(Search search, MetadataReader reader, TypeDefinitionHandle handle)
        {
            var type = reader.GetTypeDefinition(handle);
            var name = ReadTypeLabel(search, reader, handle);
            var declaring = type.GetDeclaringType();
            if (!declaring.IsNil)
        {
            return ReadTypeName(search, reader, declaring) + "." + name;
        }

        var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        private static string ReadNamespace(MetadataReader reader, TypeDefinition type)
        {
            while (true)
            {
                var declaring = type.GetDeclaringType();
                if (declaring.IsNil)
            {
                return reader.GetString(type.Namespace);
            }

            type = reader.GetTypeDefinition(declaring);
            }
        }

        private static async Task ScanLiteralsAsync(Search search, LoadedModule assembly, MetadataModule main, CancellationToken ct)
        {
            var file = LoadedAssemblies.GetFile(assembly);
            var reader = file.Metadata;
            var literal = SearchLiteral.FromQuery(search.Query);
            foreach (var handle in reader.MethodDefinitions)
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                var method = reader.GetMethodDefinition(handle);
                if (!method.HasBody())
            {
                continue;
            }

                var namespaceName = ReadNamespace(reader, reader.GetTypeDefinition(method.GetDeclaringType()));
                if (!IsInNamespace(search, namespaceName))
            {
                continue;
            }

            BlobReader body;
                try
                {
                    body = file.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
                }
                catch (BadImageFormatException)
                {
                    continue;
                }
                if (HasMatchInBody(search, body, reader, literal))
            {
                await AddResultOrAncestorAsync(search, assembly, main, handle, ct);
            }
        }
            foreach (var handle in reader.FieldDefinitions)
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                var field = reader.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.Literal) == 0)
            {
                continue;
            }

            var constantHandle = field.GetDefaultValue();
                if (constantHandle.IsNil)
            {
                continue;
            }

                var namespaceName = ReadNamespace(reader, reader.GetTypeDefinition(field.GetDeclaringType()));
                if (!IsInNamespace(search, namespaceName))
            {
                continue;
            }

            var constant = reader.GetConstant(constantHandle);
                var blob = reader.GetBlobReader(constant.Value);
                var value = blob.ReadConstant(constant.TypeCode);
                if (IsConstantMatch(search, value, literal))
            {
                await AddResultAsync(search, assembly, main, handle, ct);
            }
        }
        }

    private static bool IsConstantMatch(Search search, object? value, SearchLiteral literal)
    {
        if (value is null)
        {
            return false;
        }

        return literal.Match(
            integer => IsIntegerConstant(value, integer.Value),
            single => value is float f && f == single.Value,
            real => value is double d && d == real.Value,
            _ => SearchQueries.IsMatch(search.Query, value.ToString() ?? ""));
    }

    /// <summary>Whether <paramref name="value"/> is an integer constant, of any width, equal to <paramref name="number"/>.</summary>
    private static bool IsIntegerConstant(object value, long number)
    {
        if (value is string or char or bool or float or double)
        {
            return false;
        }

        var integer = Convert.ToInt64(value is ulong u ? (long)u : value);
        return integer == number;
    }

    /// <summary>Whether the method <paramref name="body"/> loads <paramref name="literal"/>.</summary>
    private static bool HasMatchInBody(Search search, BlobReader body, MetadataReader reader, SearchLiteral literal)
    {
        return literal.Match(
            integer => HasIntegerLoad(body, integer.Value),
            single => HasSingleLoad(body, single.Value),
            real => HasDoubleLoad(body, real.Value),
            _ => HasStringLoad(search, body, reader));
    }

    private static bool HasIntegerLoad(BlobReader body, long value)
    {
        while (TrySkipToLoad(ref body, IsIntegerLoad, out var code))
        {
            var operand = ReadIntegerOperand(ref body, code);
            if (operand == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSingleLoad(BlobReader body, float value)
    {
        while (TrySkipToLoad(ref body, static code => code == ILOpCode.Ldc_r4, out _))
        {
            var operand = body.ReadSingle();
            if (operand == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasDoubleLoad(BlobReader body, double value)
    {
        while (TrySkipToLoad(ref body, static code => code == ILOpCode.Ldc_r8, out _))
        {
            var operand = body.ReadDouble();
            if (operand == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasStringLoad(Search search, BlobReader body, MetadataReader reader)
    {
        while (TrySkipToLoad(ref body, static code => code == ILOpCode.Ldstr, out _))
        {
            var operand = body.DecodeUserString(reader);
            if (SearchQueries.IsMatch(search.Query, operand))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Moves <paramref name="body"/> past instructions to the next one <paramref name="isLoad"/>
    /// accepts, up to its operand, and gives its <paramref name="code"/>; false at the end of the body.</summary>
    private static bool TrySkipToLoad(ref BlobReader body, Func<ILOpCode, bool> isLoad, out ILOpCode code)
    {
        while (body.RemainingBytes > 0)
        {
            code = body.DecodeOpCode();
            if (isLoad(code))
            {
                return true;
            }

            body.SkipOperand(code);
        }

        code = default;
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

        private static async Task FindTokenAsync(Search search, LoadedModule assembly, MetadataModule main, CancellationToken ct)
        {
            var term = SearchQueries.FindSingleTerm(search.Query);
            if (term is null)
            {
                return;
            }

            if (term.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            term = term[2..];
        }

        if (!int.TryParse(term, System.Globalization.NumberStyles.AllowHexSpecifier, null, out var token))
        {
            return;
        }

        var handle = MetadataTokenHelpers.EntityHandleOrNil(token);
            if (handle.IsNil)
        {
            return;
        }

        var reader = LoadedAssemblies.GetFile(assembly).Metadata;
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
        {
            await AddResultOrAncestorAsync(search, assembly, main, handle, ct);
        }
    }

        private static async Task ScanResourcesAsync(Search search, LoadedModule assembly, CancellationToken ct)
        {
            var root = await BrowseSessions.EnsureRootAsync(search.Session, assembly, search.CancellationToken);
            foreach (var child in await BrowseSessions.ListChildrenAsync(search.Session, root, search.CancellationToken))
            {
                if (child.Subject is NodeSubject.Resources)
            {
                await ScanResourcesAsync(search, child, ct);
            }
        }
        }

        private static async Task ScanResourcesAsync(Search search, BrowseNode parent, CancellationToken ct)
        {
            foreach (var node in await BrowseSessions.ListChildrenAsync(search.Session, parent, search.CancellationToken))
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                if (SearchQueries.IsMatch(search.Query, node.Text))
            {
                AddResult(search, node, parent.Subject is NodeSubject.ResourceContainer ? parent.Text : "");
            }

            if (search.Session.HasChildren(node))
            {
                await ScanResourcesAsync(search, node, ct);
            }
        }
        }

        private static async Task MatchAssemblyAsync(Search search, LoadedModule assembly, CancellationToken ct)
        {
            var matches = search.Query.AssemblyName switch
            {
                AssemblyNameKind.FilePath => SearchQueries.IsMatch(search.Query, assembly.FilePath.Text),
                AssemblyNameKind.FullName => SearchQueries.IsMatch(search.Query, assembly.FullName),
                _ => SearchQueries.IsMatch(search.Query, assembly.Name) || SearchQueries.IsMatch(search.Query, assembly.FilePath.FileName),
            };
            if (matches)
        {
            AddResult(search, await BrowseSessions.EnsureRootAsync(search.Session, assembly, search.CancellationToken), assembly.FilePath.Text);
        }
    }

        private static async Task ScanNamespacesAsync(Search search, LoadedModule assembly, CancellationToken ct)
        {
            var root = await BrowseSessions.EnsureRootAsync(search.Session, assembly, search.CancellationToken);
            foreach (var node in await BrowseSessions.ListChildrenAsync(search.Session, root, search.CancellationToken))
            {
                search.CancellationToken.ThrowIfCancellationRequested();
                if (IsNamespaceFound(search, node))
            {
                AddResult(search, node, "");
            }
        }
        }

        /// <summary>Whether <paramref name="node"/> is a namespace the assembly puts types in that the
        /// Query finds.</summary>
        private static bool IsNamespaceFound(Search search, BrowseNode node)
        {
            if (node.Subject is not NodeSubject.Namespace ns)
        {
            return false;
        }

        if (ns.Value.IsEnclosingOnly)
        {
            return false;
        }

        return IsInNamespace(search, ns.Value.Name) && SearchQueries.IsMatch(search.Query, node.Text);
        }

        private static async Task AddResultAsync(Search search, LoadedModule assembly, MetadataModule main, EntityHandle handle, CancellationToken ct)
        {
            var node = await FindNodeAsync(search, assembly, main, handle, ct);
            if (node is null)
        {
            return;
        }

        var visibility = node.Visibility;
        if (visibility.IsCompilerGenerated)
        {
            return;
        }

        if (!BrowseNodes.IsMember(node.Parent))
        {
            AddResult(search, node);
        }
    }

        private static async Task AddResultOrAncestorAsync(Search search, LoadedModule assembly, MetadataModule main, EntityHandle handle, CancellationToken ct)
        {
            BrowseNode? node;
            try
            {
                node = await BrowseSessions.FindOrAncestorAsync(search.Session, assembly, main, handle, search.CancellationToken);
            }
            catch (BadImageFormatException)
            {
                return;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            node = FindSourceDeclaredAncestor(node);
            if (node is not null && BrowseNodes.IsMember(node.Parent))
        {
            node = node.Parent;
        }

        if (node is not null)
        {
            AddResult(search, node);
        }
    }

        /// <summary><paramref name="node"/>, or the nearest of its ancestors the source declared
        /// when the compiler made it up, such as the type that holds a lambda's closure class.</summary>
        private static BrowseNode? FindSourceDeclaredAncestor(BrowseNode? node)
        {
            while (node is not null)
        {
            var visibility = node.Visibility;
            if (!visibility.IsCompilerGenerated)
            {
                break;
            }

            node = node.Parent;
        }

        return node;
        }

        private static async Task<BrowseNode?> FindNodeAsync(Search search, LoadedModule assembly, MetadataModule main, EntityHandle handle, CancellationToken ct)
        {
            try
            {
                return await BrowseSessions.FindNodeAsync(search.Session, assembly, main, handle, search.CancellationToken);
            }
            catch (BadImageFormatException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static void AddResult(Search search, BrowseNode node)
        {
            var ns = node.Parent;
            while (ns is not null && !IsNamespace(ns))
        {
            ns = ns.Parent;
        }

        var name = ns?.Text ?? "";
            var type = node.Parent;
            if (type is not null && type.Subject is NodeSubject.TypeDefinition)
        {
            AddResult(search, node, name.Length == 0 ? type.Text : $"{name}.{type.Text}");
        }
        else
        {
            AddResult(search, node, name.Length == 0 ? "-" : name);
        }
    }

        private static bool IsNamespace(BrowseNode node)
    {
        return node.Subject is NodeSubject.Namespace;
    }

        private static void AddResult(Search search, BrowseNode node, string location)
        {
            if (!search.Found.Add(node))
        {
            return;
        }

        search.Emit(new SearchFind.Hit
        {
            Result = new SearchResult
            {
                Node = node,
                Text = node.Text,
                Location = location,
                Fitness = GetFitness(node),
            },
        });
        }

        private static float GetFitness(BrowseNode node)
        {
            var name = GetNodeName(node);
            return name.Length == 0 || name[0] == '<' ? 0f : 1f / name.Length;
        }

        /// <summary>The name a node is matched by: a constructor's its type's, an entity's its own, else its label.</summary>
        private static string GetNodeName(BrowseNode node)
        {
            var entity = BrowseNodes.FindEntity(node);
            if (entity is null)
        {
            return node.Text;
        }

        if (entity is IMethod method && method.IsConstructor && method.DeclaringTypeDefinition is not null)
        {
            return method.DeclaringTypeDefinition.Name;
        }

        return entity.Name;
        }
}
