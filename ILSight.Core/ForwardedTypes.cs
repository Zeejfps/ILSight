using System.Diagnostics;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core;

/// <summary>Reading the types an assembly forwards from its ExportedType table.</summary>
internal static class ForwardedTypes
{
    /// <summary>The top-level types <paramref name="metadata"/> forwards, by namespace. Nested
    /// forwarded types are left out: they come with the type that holds them.</summary>
    public static Dictionary<string, List<ForwardedType>> ListByNamespace(MetadataReader metadata)
    {
        var byNamespace = new Dictionary<string, List<ForwardedType>>(StringComparer.Ordinal);
        foreach (var handle in metadata.ExportedTypes)
        {
            var exported = metadata.GetExportedType(handle);
            if (!IsTopLevelForwarder(exported))
            {
                continue;
            }

            var type = Read(metadata, handle, exported);
            if (!byNamespace.TryGetValue(type.Namespace, out var types))
            {
                byNamespace.Add(type.Namespace, types = new List<ForwardedType>());
            }

            types.Add(type);
        }
        return byNamespace;
    }

    internal static bool IsTopLevelForwarder(ExportedType exported)
    {
        if (!exported.IsForwarder)
        {
            return false;
        }

        return exported.Implementation.Kind == HandleKind.AssemblyReference;
    }

    /// <summary>The top-level type <paramref name="handle"/> forwards.</summary>
    public static ForwardedType Read(MetadataReader metadata, ExportedTypeHandle handle)
    {
        return Read(metadata, handle, metadata.GetExportedType(handle));
    }

    private static ForwardedType Read(MetadataReader metadata, ExportedTypeHandle handle, ExportedType exported)
    {
        var name = NameOf(metadata, handle);
        var label = LabelOf(name.Name, name.TypeParameterCount);
        if (exported.Implementation.Kind != HandleKind.AssemblyReference)
        {
            throw new UnreachableException("Only top-level forwarded types are read, and each names the assembly it is forwarded to.");
        }

        var destination = metadata.GetAssemblyReference((AssemblyReferenceHandle)exported.Implementation);
        return new ForwardedType
        {
            Handle = handle,
            Namespace = name.Namespace,
            Label = label,
            SortName = name.Name,
            Arity = name.TypeParameterCount,
            Destination = metadata.GetString(destination.Name),
        };
    }

    /// <summary>The name with a slot for each type parameter, as <c>typeof</c> writes an open generic
    /// type: <c>List&lt;&gt;</c>, <c>Dictionary&lt;,&gt;</c>.</summary>
    internal static string LabelOf(string name, int arity)
    {
        if (arity == 0)
        {
            return name;
        }

        var commas = new string(',', arity - 1);
        return $"{name}<{commas}>";
    }

    /// <summary>The full name of the type <paramref name="handle"/> forwards, to find it by.</summary>
    public static TopLevelTypeName NameOf(MetadataReader metadata, ExportedTypeHandle handle)
    {
        var exported = metadata.GetExportedType(handle);
        var ns = metadata.GetString(exported.Namespace);
        var name = ReflectionHelper.SplitTypeParameterCountFromReflectionName(metadata.GetString(exported.Name), out var arity);
        return new TopLevelTypeName(ns, name, arity);
    }

    private static readonly List<TypeReferenceHandle> NoTypes = [];
    private static readonly List<MemberReferenceHandle> NoMembers = [];
    private static readonly List<ExportedTypeHandle> NoForwarders = [];

    /// <summary>What <paramref name="metadata"/> uses of what it references.</summary>
    internal static ReferenceUsage ReadUsage(MetadataReader metadata)
    {
        var usage = new ReferenceUsage();
        foreach (var handle in metadata.TypeReferences)
        {
            AddToList(usage.Types, metadata.GetTypeReference(handle).ResolutionScope, handle);
        }

        foreach (var handle in metadata.MemberReferences)
        {
            ForwardedTypes.AddUsedMember(usage, metadata, handle);
        }

        foreach (var handle in metadata.ExportedTypes)
        {
            ForwardedTypes.AddUsedForwarder(usage, metadata, handle);
        }

        return usage;
    }

    /// <summary>The types named in <paramref name="scope"/>: an assembly reference, or a type
    /// reference for the types nested in it.</summary>
    internal static IReadOnlyList<TypeReferenceHandle> ListTypesIn(ReferenceUsage usage, EntityHandle scope)
    {
        return usage.Types.GetValueOrDefault(scope, NoTypes);
    }

    /// <summary>The members used of the type <paramref name="type"/> names.</summary>
    internal static IReadOnlyList<MemberReferenceHandle> ListMembersOf(ReferenceUsage usage, TypeReferenceHandle type)
    {
        return usage.UsedMembers.GetValueOrDefault(type, NoMembers);
    }

    /// <summary>The top-level types forwarded to the assembly <paramref name="reference"/> names.</summary>
    internal static IReadOnlyList<ExportedTypeHandle> ListForwardedTo(ReferenceUsage usage, AssemblyReferenceHandle reference)
    {
        return usage.Forwarders.GetValueOrDefault(reference, NoForwarders);
    }

    /// <summary>Whether anything is used of, or forwarded to, the assembly <paramref name="reference"/> names.</summary>
    internal static bool UsesReference(ReferenceUsage usage, AssemblyReferenceHandle reference)
    {
        var types = ListTypesIn(usage, reference);
        if (types.Count > 0)
        {
            return true;
        }

        return ListForwardedTo(usage, reference).Count > 0;
    }

    private static void AddUsedMember(ReferenceUsage usage, MetadataReader metadata, MemberReferenceHandle handle)
    {
        var type = FindUsedType(metadata, metadata.GetMemberReference(handle).Parent);
        if (type is null)
        {
            return;
        }

        AddToList(usage.UsedMembers, type.Value, handle);
    }

    /// <summary>The type reference a member's <paramref name="parent"/> names: itself, or the generic
    /// type of a generic instance; null for anything else.</summary>
    private static EntityHandle? FindUsedType(MetadataReader metadata, EntityHandle parent)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            return parent;
        }

        if (parent.Kind != HandleKind.TypeSpecification)
        {
            return null;
        }

        return FindGenericType(metadata, (TypeSpecificationHandle)parent);
    }

    /// <summary>The type reference of the generic type <paramref name="handle"/> instantiates; null when it
    /// is no generic instance of a referenced type, or its signature is unreadable.</summary>
    private static EntityHandle? FindGenericType(MetadataReader metadata, TypeSpecificationHandle handle)
    {
        try
        {
            var reader = metadata.GetBlobReader(metadata.GetTypeSpecification(handle).Signature);
            var typeCode = reader.ReadSignatureTypeCode();
            if (typeCode != SignatureTypeCode.GenericTypeInstance)
            {
                return null;
            }

            SkipClassOrValueType(ref reader);
            var type = reader.ReadTypeHandle();
            if (type.Kind != HandleKind.TypeReference)
            {
                return null;
            }

            return type;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static void SkipClassOrValueType(ref BlobReader reader)
    {
        reader.ReadByte();
    }

    private static void AddUsedForwarder(ReferenceUsage usage, MetadataReader metadata, ExportedTypeHandle handle)
    {
        var exported = metadata.GetExportedType(handle);
        if (!ForwardedTypes.IsTopLevelForwarder(exported))
        {
            return;
        }

        AddToList(usage.Forwarders, exported.Implementation, handle);
    }

    private static void AddToList<T>(Dictionary<EntityHandle, List<T>> lists, EntityHandle key, T item)
    {
        if (!lists.TryGetValue(key, out var list))
        {
            list = new List<T>();
            lists.Add(key, list);
        }
        list.Add(item);
    }
}
