using System.Diagnostics;
using ICSharpCode.Decompiler.TypeSystem;

namespace ILSight.Core.Comparison;

/// <summary>What matches an entity of one assembly with its counterpart in another: a type's full
/// reflection name, and a member's kind, name, type parameter count and parameter types, so that each
/// overload is matched alone. Types are written as reflection names, which leave out nullability and
/// number type parameters, so that neither a nullability change nor a renamed type parameter breaks a match.</summary>
internal static class ApiKeys
{
    private const string TypePrefix = "T:";
    private const string FieldPrefix = "F:";
    private const string PropertyPrefix = "P:";
    private const string EventPrefix = "E:";
    private const string MethodPrefix = "M:";
    private const string ImplicitConversion = "op_Implicit";
    private const string ExplicitConversion = "op_Explicit";

    public static string Of(IEntity entity)
    {
        return entity switch
        {
            ITypeDefinition type => TypePrefix + type.ReflectionName,
            IField field => FieldPrefix + field.Name,
            IProperty property => PropertyPrefix + property.Name + ParameterList(property.Parameters),
            IEvent @event => EventPrefix + @event.Name,
            IMethod method => OfMethod(method),
            _ => throw new UnreachableException(),
        };
    }

    private static string OfMethod(IMethod method)
    {
        var parameters = ParameterList(method.Parameters);
        var key = $"{MethodPrefix}{method.Name}`{method.TypeParameters.Count}{parameters}";
        if (!IsConversion(method))
        {
            return key;
        }

        // Only the type a conversion operator converts to sets apart its overloads.
        return key + ":" + method.ReturnType.ReflectionName;
    }

    private static bool IsConversion(IMethod method)
    {
        if (method.Name == ImplicitConversion)
        {
            return true;
        }

        return method.Name == ExplicitConversion;
    }

    private static string ParameterList(IReadOnlyList<IParameter> parameters)
    {
        var types = parameters.Select(ParameterKey);
        return "(" + string.Join(",", types) + ")";
    }

    private static string ParameterKey(IParameter parameter)
    {
        return parameter.ReferenceKind + " " + parameter.Type.ReflectionName;
    }
}
