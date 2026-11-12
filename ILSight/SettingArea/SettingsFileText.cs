using System.Diagnostics;

namespace ILSight.SettingArea;

/// <summary>What reading the settings file gave: no file, its text, or why it could not be read, as the system
/// said it.</summary>
public abstract record SettingsFileText
{
    private SettingsFileText()
    {
    }

    public sealed record None : SettingsFileText;

    public sealed record Text(string Content) : SettingsFileText;

    public sealed record NotRead(string Detail) : SettingsFileText;

    public TResult Match<TResult>(Func<None, TResult> none, Func<Text, TResult> text, Func<NotRead, TResult> notRead)
    {
        return this switch
        {
            None n => none(n),
            Text t => text(t),
            NotRead n => notRead(n),
            _ => throw new UnreachableException($"SettingsFileText.Match does not handle {GetType().Name}."),
        };
    }
}
