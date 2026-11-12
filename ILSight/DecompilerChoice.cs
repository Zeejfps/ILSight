using ILSight.Core;

namespace ILSight;

/// <summary>The language code is shown in and how C# is decompiled, as the user chose them, and the setup made from
/// them (<see cref="Setup"/>): the one value the views read and pass on, made again only when one of them changes, so a
/// frame makes none. A workflow that shows code once its wait is over takes this and reads <see cref="Setup"/> then,
/// as it stands after the wait: a language switched meanwhile is the one it shows the code in.</summary>
public sealed class DecompilerChoice
{
    private DecompilerSetup? _setup;

    /// <summary>The language and the options as they stand now.</summary>
    public required DecompilerSetup Setup
    {
        get => _setup ?? throw new InvalidOperationException("The choice is made with how it decompiles.");
        init => _setup = value;
    }

    /// <summary>Shows code in <paramref name="language"/> from now on.</summary>
    public void UseLanguage(DecompilationLanguage language)
    {
        if (language == Setup.Language)
        {
            return;
        }

        _setup = Setup with { Language = language };
    }

    /// <summary>Decompiles C# with <paramref name="options"/> from now on.</summary>
    public void UseOptions(DecompilerOptions options)
    {
        if (options == Setup.Options)
        {
            return;
        }

        _setup = Setup with { Options = options };
    }
}
