using System.Reflection;

namespace ILSight.Tests;

/// <summary>A library object whose method kind (a constructor's, a destructor's) the library fails decoding, answering
/// anything else with nothing.</summary>
public class KindUnreadableMethod : DispatchProxy
{
    public static T Create<T>() where T : class
    {
        return Create<T, KindUnreadableMethod>();
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        if (targetMethod.Name is "get_IsConstructor" or "get_IsDestructor")
        {
            throw new BadImageFormatException("The metadata behind the method's kind is corrupt.");
        }

        var returned = targetMethod.ReturnType;
        return returned.IsValueType && returned != typeof(void) ? Activator.CreateInstance(returned) : null;
    }
}
