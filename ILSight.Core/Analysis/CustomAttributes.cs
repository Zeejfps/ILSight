// Derived from ILSpy (MIT): see LICENSE-ILSpy.txt

using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Analysis;

/// <summary>A module's custom attributes as the analyzers read them: an attribute's type, and what
/// carries each attribute.</summary>
internal static class CustomAttributes
{
    /// <summary>Whether the attribute built by <paramref name="customAttributeCtor"/> is of the type
    /// <paramref name="decoder"/> looks for.</summary>
    public static bool IsOfType(EntityHandle customAttributeCtor, MetadataReader metadata, FindTypeDecoder decoder)
    {
        return decoder.GetTypeFromEntity(metadata, customAttributeCtor.GetDeclaringType(metadata));
    }

    /// <summary>What carries each attribute of <paramref name="ts"/>'s module that
    /// <paramref name="matches"/> accepts, in attribute order, then the method, or the property or
    /// event it is an accessor of, for each parameter that carries one.</summary>
    public static IEnumerable<ISymbol> ListOwners(DecompilerTypeSystem ts, Func<CustomAttribute, bool> matches)
    {
        var metadata = ts.MainModule.MetadataFile.Metadata;
        var parameters = new HashSet<ParameterHandle>();
        foreach (var h in metadata.CustomAttributes)
        {
            var attribute = metadata.GetCustomAttribute(h);
            if (!matches(attribute))
            {
                continue;
            }

            if (attribute.Parent.Kind == HandleKind.Parameter)
            {
                parameters.Add((ParameterHandle)attribute.Parent);
            }
            else
            {
                var parent = FindParentEntity(ts, attribute);
                if (parent is not null)
                {
                    yield return parent;
                }
            }
        }
        if (parameters.Count == 0)
        {
            yield break;
        }

        foreach (var h in metadata.MethodDefinitions)
        {
            var methodParameters = metadata.GetMethodDefinition(h).GetParameters();
            if (!methodParameters.Any(parameters.Contains))
            {
                continue;
            }

            var method = ts.MainModule.ResolveMethod(h, default);
            if (method is not null)
            {
                yield return method.IsAccessor ? method.AccessorOwner : method;
            }
        }
    }

    private static ISymbol? FindParentEntity(DecompilerTypeSystem ts, CustomAttribute customAttribute)
    {
        var metadata = ts.MainModule.MetadataFile.Metadata;
        switch (customAttribute.Parent.Kind)
        {
            case HandleKind.MethodDefinition:
                var entity = ts.MainModule.ResolveEntity(customAttribute.Parent);
                if (entity is not IMethod parent)
                {
                    return null;
                }

                return parent.AccessorOwner ?? parent;
            case HandleKind.FieldDefinition:
            case HandleKind.PropertyDefinition:
            case HandleKind.EventDefinition:
            case HandleKind.TypeDefinition:
                return ts.MainModule.ResolveEntity(customAttribute.Parent);
            case HandleKind.AssemblyDefinition:
            case HandleKind.ModuleDefinition:
                return ts.MainModule;
            case HandleKind.GenericParameterConstraint:
                var gpc = metadata.GetGenericParameterConstraint((GenericParameterConstraintHandle)customAttribute.Parent);
                var gp = metadata.GetGenericParameter(gpc.Parameter);
                return ts.MainModule.ResolveEntity(gp.Parent);
            case HandleKind.GenericParameter:
                gp = metadata.GetGenericParameter((GenericParameterHandle)customAttribute.Parent);
                return ts.MainModule.ResolveEntity(gp.Parent);
            default:
                return null;
        }
    }
}
