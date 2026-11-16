// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.AnalysisArea;

/// <summary>A module's custom attributes as the analyzers read them: an attribute's type, and what
/// carries each attribute. Each step reads under a catch of its own, noting a part the library fails reading as passed
/// over; which attributes are wanted, and which methods carry a parameter that carries one, are decided
/// outside.</summary>
internal static class CustomAttributes
{
    /// <summary>Whether the attribute built by <paramref name="customAttributeCtor"/> is of the type
    /// <paramref name="decoder"/> looks for.</summary>
    public static bool IsOfType(EntityHandle customAttributeCtor, MetadataReader metadata, FindTypeDecoder decoder)
    {
        return decoder.GetTypeFromEntity(metadata, customAttributeCtor.GetDeclaringType(metadata));
    }

    /// <summary>What carries each attribute of <paramref name="main"/>, read from <paramref name="file"/>, that
    /// <paramref name="matches"/> accepts, in attribute order, then the method, or the property or event it is an
    /// accessor of, for each parameter that carries one. <paramref name="matches"/>, the analysis's test, runs outside the
    /// reads, and reads what it needs under its own.</summary>
    public static IEnumerable<ISymbol> ListOwners(MetadataModule main, MetadataFile file, Func<CustomAttribute, bool> matches, PassedOver passedOver)
    {
        var parameters = new HashSet<ParameterHandle>();
        var handles = ReadAttributeHandles(file, passedOver);
        foreach (var handle in handles ?? [])
        {
            var attribute = ReadAttribute(file, handle, passedOver);
            if (attribute is null || !matches(attribute.Value.Attribute))
            {
                continue;
            }

            var parent = attribute.Value.Parent;
            if (parent.Kind == HandleKind.Parameter)
            {
                parameters.Add((ParameterHandle)parent);
                continue;
            }

            var owner = ReadParentEntity(main, file, parent, passedOver);
            if (owner is not null)
            {
                yield return owner;
            }
        }
        if (parameters.Count == 0)
        {
            yield break;
        }

        var methods = ReadMethodHandles(file, passedOver);
        foreach (var method in methods ?? [])
        {
            var methodParameters = ReadParameters(file, method, passedOver);
            if (methodParameters is null || !methodParameters.Any(parameters.Contains))
            {
                continue;
            }

            var owner = ReadMethodOwner(main, file, method, passedOver);
            if (owner is not null)
            {
                yield return owner;
            }
        }
    }

    private static CustomAttributeHandle[]? ReadAttributeHandles(MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return file.Metadata.CustomAttributes.ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The attribute and what it is applied to; null when the library fails reading them, noted.
    private static (CustomAttribute Attribute, EntityHandle Parent)? ReadAttribute(MetadataFile file, CustomAttributeHandle handle, PassedOver passedOver)
    {
        try
        {
            var attribute = file.Metadata.GetCustomAttribute(handle);
            return (attribute, attribute.Parent);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static MethodDefinitionHandle[]? ReadMethodHandles(MetadataFile file, PassedOver passedOver)
    {
        try
        {
            return file.Metadata.MethodDefinitions.ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static ParameterHandle[]? ReadParameters(MetadataFile file, MethodDefinitionHandle method, PassedOver passedOver)
    {
        try
        {
            return file.Metadata.GetMethodDefinition(method).GetParameters().ToArray();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // The method, or the property or event it is an accessor of; null when it resolves to none, or the library fails
    // resolving it, noted.
    private static ISymbol? ReadMethodOwner(MetadataModule main, MetadataFile file, MethodDefinitionHandle handle, PassedOver passedOver)
    {
        try
        {
            var method = main.ResolveMethod(handle, default);
            if (method is null)
            {
                return null;
            }

            return method.IsAccessor ? method.AccessorOwner : method;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    // What the attribute is applied to, as the analyzer shows it: an accessor's property or event, the module for an
    // assembly or module attribute, the entity declaring a generic parameter; null for anything else, or when the
    // library fails resolving it, noted.
    private static ISymbol? ReadParentEntity(MetadataModule main, MetadataFile file, EntityHandle parent, PassedOver passedOver)
    {
        try
        {
            return FindParentEntity(main, file.Metadata, parent);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            passedOver.Note(file, e);
            return null;
        }
    }

    private static ISymbol? FindParentEntity(MetadataModule main, MetadataReader metadata, EntityHandle parent)
    {
        switch (parent.Kind)
        {
            case HandleKind.MethodDefinition:
                var entity = main.ResolveEntity(parent);
                if (entity is not IMethod method)
                {
                    return null;
                }

                return method.AccessorOwner ?? method;
            case HandleKind.FieldDefinition:
            case HandleKind.PropertyDefinition:
            case HandleKind.EventDefinition:
            case HandleKind.TypeDefinition:
                return main.ResolveEntity(parent);
            case HandleKind.AssemblyDefinition:
            case HandleKind.ModuleDefinition:
                return main;
            case HandleKind.GenericParameterConstraint:
                var gpc = metadata.GetGenericParameterConstraint((GenericParameterConstraintHandle)parent);
                var gp = metadata.GetGenericParameter(gpc.Parameter);
                return main.ResolveEntity(gp.Parent);
            case HandleKind.GenericParameter:
                gp = metadata.GetGenericParameter((GenericParameterHandle)parent);
                return main.ResolveEntity(gp.Parent);
            default:
                return null;
        }
    }
}
