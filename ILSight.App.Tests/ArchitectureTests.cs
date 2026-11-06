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
    public void Every_type_is_a_record_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().Where(IsNotData).Select(GetName).ToHashSet();

        var expected = ShapeExceptions.Seams.Keys.Concat(ShapeExceptions.NotYetData);

        AssertMatches(expected, found);
    }

    [Fact]
    public void No_record_has_behaviour_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().Where(IsRecord).SelectMany(ListMethods).ToHashSet();

        AssertMatches(ShapeExceptions.RecordsWithMethods, found);
    }

    [Fact]
    public void No_static_field_can_change_unless_it_is_listed()
    {
        var found = ListDeclaredTypes().SelectMany(ListMutableStaticFields).ToHashSet();

        AssertMatches(ShapeExceptions.MutableStatics, found);
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

    private static bool IsNotData(Type type)
    {
        if (type.IsEnum) return false;
        if (type.IsSubclassOf(typeof(Delegate))) return false;
        if (IsProjection(type)) return false;
        if (type.IsByRefLike) return false;
        if (IsStaticClass(type)) return false;
        return !IsRecord(type);
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
            if (IsAllowedOnRecord(method)) continue;
            yield return $"{GetName(type)}.{method.Name}";
        }
    }

    /// <summary>Whether a record may have this: auto-property accessors and what the compiler writes
    /// are marked as generated, which leaves a computed property's getter to count as a method.</summary>
    private static bool IsAllowedOnRecord(MethodInfo method)
    {
        if (method.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
        if (method.IsSpecialName && !method.Name.StartsWith("get_", StringComparison.Ordinal)) return true;
        // What a record may write for itself: see rule 7.
        if (method.Name is "Equals" or "GetHashCode" or "PrintMembers") return true;
        return ShapeExceptions.RecordSeams.ContainsKey($"{GetName(method.DeclaringType!)}.{method.Name}");
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
