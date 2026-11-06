using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ILSight.Core;

namespace ILSight.App.Tests;

/// <summary>Holds the app to docs/data_and_functions.md. What doesn't fit yet is listed in
/// <see cref="ShapeExceptions"/>, and an entry that no longer applies fails too, so that the lists can
/// only shrink. These rules are all ones the compiler can't check.</summary>
public sealed class ArchitectureTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly Assembly[] Scope = [typeof(AppUpdates).Assembly, typeof(AssemblyList).Assembly];

    [Fact]
    public void No_type_has_behaviour_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().Where(static type => HasBehaviour(type) || IsSelfContained(type)).Select(GetName).ToHashSet();

        var expected = ShapeExceptions.Seams.Keys.Concat(ShapeExceptions.SelfContained).Concat(ShapeExceptions.NotYetData);

        AssertMatches(expected, found);
    }

    [Fact]
    public void A_type_listed_as_self_contained_names_only_the_BCL_and_other_such_types()
    {
        var entangled = ListDeclaredTypes().Where(IsSelfContained)
            .SelectMany(static type => ListForeignTypes(type).Select(foreign => $"{GetName(type)} names {foreign}"));

        Assert.Empty(entangled);
    }

    [Fact]
    public void No_record_has_a_method_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().Where(static type => IsRecord(type) && !IsSelfContained(type)).SelectMany(ListMethods).ToHashSet();

        AssertMatches(ShapeExceptions.RecordsWithMethods, found);
    }

    [Fact]
    public void No_static_field_can_change_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().SelectMany(ListMutableStaticFields).ToHashSet();

        AssertMatches(ShapeExceptions.AllowedStatics.Keys, found);
    }

    private static void AssertMatches(IEnumerable<string> listed, HashSet<string> found)
    {
        var expected = listed.ToHashSet();
        var unlisted = found.Except(expected).Order();
        var stale = expected.Except(found).Order();

        Assert.True(found.SetEquals(expected),
            $"New, and not allowed:\n  {string.Join("\n  ", unlisted)}\n"
            + $"Gone, so take them off the list:\n  {string.Join("\n  ", stale)}");
    }

    private static IEnumerable<Type> ListDeclaredTypes()
    {
        return Scope.SelectMany(static assembly => assembly.GetTypes()).Where(static type => !IsGenerated(type));
    }

    private static bool IsGenerated(Type type)
    {
        for (var current = type; current != null; current = current.DeclaringType)
        {
            if (current.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
            if (current.IsDefined(typeof(GeneratedCodeAttribute), false)) return true;
            // The compiler's inline array types carry neither attribute.
            if (current.Name.StartsWith("<>", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>What builds a case of a struct union: a static function giving back the union.</summary>
    private static bool IsCaseOfStructUnion(MethodInfo method)
    {
        var union = method.DeclaringType!;
        return method.IsStatic && method.ReturnType == union && ShapeExceptions.StructUnions.Contains(GetName(union));
    }

    private static bool IsSelfContained(Type type)
    {
        return ShapeExceptions.SelfContained.Contains(GetName(type));
    }

    /// <summary>Whether a class, struct or interface does more than hold data. Records are listed method by
    /// method instead, as most of them have few.</summary>
    private static bool HasBehaviour(Type type)
    {
        if (type.IsEnum) return false;
        if (type.IsSubclassOf(typeof(Delegate))) return false;
        if (IsProjection(type)) return false;
        if (type.IsByRefLike) return false;
        if (IsStaticClass(type)) return false;
        if (IsRecord(type)) return false;
        return type.IsInterface || ListMethods(type).Any();
    }

    /// <summary>An interface holding only properties, whose accessors are the only special methods one can have.</summary>
    private static bool IsProjection(Type type)
    {
        if (!type.IsInterface) return false;
        return type.GetMethods(Declared | BindingFlags.Instance | BindingFlags.Static).All(static method => method.IsSpecialName);
    }

    private static bool IsStaticClass(Type type)
    {
        return type.IsClass && type.IsAbstract && type.IsSealed;
    }

    /// <summary>Reflection has no record flag, so this goes by what the compiler generates for one: a
    /// clone method on a class and <c>PrintMembers</c> on a struct. A class that wrote its own would pass.</summary>
    private static bool IsRecord(Type type)
    {
        if (type.IsInterface) return false;
        const BindingFlags instance = Declared | BindingFlags.Instance;
        if (type.IsValueType)
        {
            return type.GetMethod("PrintMembers", instance, [typeof(StringBuilder)]) != null;
        }
        return type.GetMethod("<Clone>$", instance) != null;
    }

    private static IEnumerable<string> ListMethods(Type type)
    {
        foreach (var method in type.GetMethods(Declared | BindingFlags.Instance | BindingFlags.Static))
        {
            if (IsAllowedOnData(method)) continue;
            yield return $"{GetName(type)}.{method.Name}";
        }
    }

    /// <summary>Whether a type holding data may have this: what the compiler writes is marked as generated, and
    /// what is left of the special methods are computed properties and operators.</summary>
    private static bool IsAllowedOnData(MethodInfo method)
    {
        if (method.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
        if (method.IsSpecialName) return true;
        // What a record may write for itself: see rule 7.
        if (method.Name is "Equals" or "GetHashCode" or "PrintMembers") return true;
        if (IsCaseOfStructUnion(method)) return true;
        return ShapeExceptions.RecordSeams.ContainsKey($"{GetName(method.DeclaringType!)}.{method.Name}");
    }

    /// <summary>What <paramref name="type"/> names in its members that is neither the BCL, part of itself,
    /// nor self-contained too. Reflection stops at signatures, so what a method body reaches for goes
    /// unseen, as does a constraint on a type parameter.</summary>
    private static IEnumerable<string> ListForeignTypes(Type type)
    {
        const BindingFlags all = Declared | BindingFlags.Instance | BindingFlags.Static;
        var named = new List<Type?> { type.BaseType };
        named.AddRange(type.GetInterfaces());
        named.AddRange(type.GetFields(all).Select(static field => field.FieldType));
        named.AddRange(type.GetProperties(all).Select(static property => property.PropertyType));
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            named.AddRange(method.GetParameters().Select(static parameter => parameter.ParameterType));
            if (method is MethodInfo info) named.Add(info.ReturnType);
        }
        return named.OfType<Type>().SelectMany(ListPartsOf).Where(part => !IsBcl(part) && !IsWithin(part, type) && !IsSelfContained(part)).Select(GetName).Distinct();
    }

    private static IEnumerable<Type> ListPartsOf(Type type)
    {
        if (type.IsGenericParameter) yield break;
        if (type.HasElementType)
        {
            foreach (var part in ListPartsOf(type.GetElementType()!)) yield return part;
            yield break;
        }
        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        foreach (var part in type.GetGenericArguments().SelectMany(ListPartsOf)) yield return part;
    }

    private static bool IsBcl(Type type)
    {
        var assembly = type.Assembly.GetName().Name!;
        return assembly == "System" || assembly.StartsWith("System.", StringComparison.Ordinal);
    }

    private static bool IsWithin(Type part, Type type)
    {
        for (var current = part; current != null; current = current.DeclaringType)
        {
            if (current == type) return true;
        }
        return false;
    }

    private static IEnumerable<string> ListMutableStaticFields(Type type)
    {
        foreach (var field in type.GetFields(Declared | BindingFlags.Static))
        {
            if (field.IsLiteral) continue;
            if (field.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
            if (field.IsInitOnly && !IsCache(field.FieldType)) continue;
            yield return $"{GetName(type)}.{field.Name}";
        }
    }

    /// <summary>A <c>readonly</c> field holding one of these still changes under everyone reading it. A
    /// lookup table built once and only read isn't one, and can't be told apart from a cache by its type,
    /// so a plain dictionary or set goes unchecked.</summary>
    private static bool IsCache(Type type)
    {
        if (!type.IsGenericType) return false;
        var definition = type.GetGenericTypeDefinition();
        return definition == typeof(ConditionalWeakTable<,>)
            || definition == typeof(ConcurrentDictionary<,>)
            || definition == typeof(Lazy<>);
    }

    private static string GetName(Type type)
    {
        return type.FullName ?? type.Name;
    }
}
