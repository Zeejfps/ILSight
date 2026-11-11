namespace ILSight.App.Tests.Fixtures;

public class Farewell
{
    /// <summary>Says <c>Bye</c> to whoever is leaving.</summary>
    public string Say()
    {
        return "Bye";
    }

    public static bool IsLeapYear(int year)
    {
        return false;
    }

    public static string Compose(string greeting, string name, int repetitions, bool shout)
    {
        return greeting + name + repetitions + shout;
    }
}

internal sealed class Hidden
{
    public int Secret()
    {
        return 42;
    }
}
