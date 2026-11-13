using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.Metadata;

namespace ILSight.Tests;

/// <summary>Reads what the types of one assembly name, by the top-level types they are in.</summary>
internal sealed class TypeUseScan : ISignatureTypeProvider<ImmutableArray<TypeDefinitionHandle>, object?>
{
    private readonly PEReader _pe;
    private readonly MetadataReader _metadata;

    public TypeUseScan(PEReader pe, MetadataReader metadata)
    {
        _pe = pe;
        _metadata = metadata;
    }

    public Dictionary<string, HashSet<string>> Uses { get; } = new();

    public void Type(TypeDefinitionHandle handle)
    {
        var uses = UsesOf(Outermost(handle));
        var type = _metadata.GetTypeDefinition(handle);
        Add(uses, ListTypesNamedBy(type.BaseType));
        foreach (var implementation in type.GetInterfaceImplementations())
        {
            Add(uses, ListTypesNamedBy(_metadata.GetInterfaceImplementation(implementation).Interface));
        }

        foreach (var field in type.GetFields())
        {
            Add(uses, _metadata.GetFieldDefinition(field).DecodeSignature(this, null));
        }

        foreach (var property in type.GetProperties())
        {
            Add(uses, Flatten(_metadata.GetPropertyDefinition(property).DecodeSignature(this, null)));
        }

        foreach (var method in type.GetMethods())
        {
            Method(uses, method);
        }
    }

    private void Method(HashSet<string> uses, MethodDefinitionHandle handle)
    {
        var method = _metadata.GetMethodDefinition(handle);
        Add(uses, Flatten(method.DecodeSignature(this, null)));
        if (method.RelativeVirtualAddress == 0)
        {
            return;
        }

        var body = _pe.GetMethodBody(method.RelativeVirtualAddress);
        if (!body.LocalSignature.IsNil)
        {
            foreach (var local in _metadata.GetStandaloneSignature(body.LocalSignature).DecodeLocalSignature(this, null))
            {
                Add(uses, local);
            }
        }
        var il = body.GetILReader();
        while (il.RemainingBytes > 0)
        {
            var opCode = il.DecodeOpCode();
            var operandType = opCode.GetOperandType();
            if (operandType is OperandType.Field or OperandType.Method or OperandType.Tok or OperandType.Type)
            {
                Add(uses, ListTypesNamedBy(MetadataTokenHelpers.EntityHandleOrNil(il.ReadInt32())));
            }
            else
            {
                il.SkipOperand(opCode);
            }
        }
    }

    private HashSet<string> UsesOf(TypeDefinitionHandle outermost)
    {
        var name = NameOf(outermost);
        if (!Uses.TryGetValue(name, out var uses))
        {
            uses = new HashSet<string>(StringComparer.Ordinal);
            Uses.Add(name, uses);
        }
        return uses;
    }

    private void Add(HashSet<string> uses, ImmutableArray<TypeDefinitionHandle> types)
    {
        foreach (var type in types)
        {
            uses.Add(NameOf(Outermost(type)));
        }
    }

    private ImmutableArray<TypeDefinitionHandle> ListTypesNamedBy(EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return [];
        }

        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return [(TypeDefinitionHandle)handle];
            case HandleKind.TypeSpecification:
                return _metadata.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null);
            case HandleKind.FieldDefinition:
                var field = _metadata.GetFieldDefinition((FieldDefinitionHandle)handle);
                return [field.GetDeclaringType(), .. field.DecodeSignature(this, null)];
            case HandleKind.MethodDefinition:
                var method = _metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                return [method.GetDeclaringType(), .. Flatten(method.DecodeSignature(this, null))];
            case HandleKind.MemberReference:
                var member = _metadata.GetMemberReference((MemberReferenceHandle)handle);
                var memberKind = member.GetKind();
                var signature = memberKind == MemberReferenceKind.Field
                    ? member.DecodeFieldSignature(this, null)
                    : Flatten(member.DecodeMethodSignature(this, null));
                return [.. ListTypesNamedBy(member.Parent), .. signature];
            case HandleKind.MethodSpecification:
                var specification = _metadata.GetMethodSpecification((MethodSpecificationHandle)handle);
                return [.. ListTypesNamedBy(specification.Method), .. specification.DecodeSignature(this, null).SelectMany(static a => a)];
            default:
                return [];
        }
    }

    private static ImmutableArray<TypeDefinitionHandle> Flatten(MethodSignature<ImmutableArray<TypeDefinitionHandle>> signature)
    {
        return [.. signature.ReturnType, .. signature.ParameterTypes.SelectMany(static p => p)];
    }

    private TypeDefinitionHandle Outermost(TypeDefinitionHandle handle)
    {
        while (true)
        {
            var declaring = _metadata.GetTypeDefinition(handle).GetDeclaringType();
            if (declaring.IsNil)
            {
                return handle;
            }

            handle = declaring;
        }
    }

    private string NameOf(TypeDefinitionHandle handle)
    {
        var type = _metadata.GetTypeDefinition(handle);
        var name = _metadata.GetString(type.Name);
        var ns = _metadata.GetString(type.Namespace);
        return ns.Length == 0 ? name : $"{ns}.{name}";
    }

    public ImmutableArray<TypeDefinitionHandle> GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        return [handle];
    }

    public ImmutableArray<TypeDefinitionHandle> GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        return [];
    }

    public ImmutableArray<TypeDefinitionHandle> GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle,
        byte rawTypeKind)
    {
        return ListTypesNamedBy(handle);
    }

    public ImmutableArray<TypeDefinitionHandle> GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return [];
    }

    public ImmutableArray<TypeDefinitionHandle> GetSZArrayType(ImmutableArray<TypeDefinitionHandle> elementType)
    {
        return elementType;
    }

    public ImmutableArray<TypeDefinitionHandle> GetArrayType(ImmutableArray<TypeDefinitionHandle> elementType, ArrayShape shape)
    {
        return elementType;
    }

    public ImmutableArray<TypeDefinitionHandle> GetByReferenceType(ImmutableArray<TypeDefinitionHandle> elementType)
    {
        return elementType;
    }

    public ImmutableArray<TypeDefinitionHandle> GetPointerType(ImmutableArray<TypeDefinitionHandle> elementType)
    {
        return elementType;
    }

    public ImmutableArray<TypeDefinitionHandle> GetPinnedType(ImmutableArray<TypeDefinitionHandle> elementType)
    {
        return elementType;
    }

    public ImmutableArray<TypeDefinitionHandle> GetGenericInstantiation(ImmutableArray<TypeDefinitionHandle> genericType,
        ImmutableArray<ImmutableArray<TypeDefinitionHandle>> typeArguments)
    {
        return [.. genericType, .. typeArguments.SelectMany(static a => a)];
    }

    public ImmutableArray<TypeDefinitionHandle> GetGenericMethodParameter(object? context, int index)
    {
        return [];
    }

    public ImmutableArray<TypeDefinitionHandle> GetGenericTypeParameter(object? context, int index)
    {
        return [];
    }

    public ImmutableArray<TypeDefinitionHandle> GetFunctionPointerType(MethodSignature<ImmutableArray<TypeDefinitionHandle>> signature)
    {
        return Flatten(signature);
    }

    public ImmutableArray<TypeDefinitionHandle> GetModifiedType(ImmutableArray<TypeDefinitionHandle> modifier, ImmutableArray<TypeDefinitionHandle> unmodifiedType,
        bool isRequired)
    {
        return unmodifiedType;
    }
}
