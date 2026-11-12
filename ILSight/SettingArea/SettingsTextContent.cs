using System.Diagnostics;
using ILSight.AppSettingArea;

namespace ILSight.SettingArea;

/// <summary>What the settings file's text holds: not JSON of the settings, <c>null</c>, or the settings, each value
/// read on its own.</summary>
public abstract record SettingsTextContent
{
    private SettingsTextContent()
    {
    }

    /// <summary>The text is not JSON of the settings; <c>Detail</c> is as the JSON reader said it.</summary>
    public sealed record NotJson(string Detail) : SettingsTextContent;

    /// <summary>The text holds <c>null</c>.</summary>
    public sealed record Empty : SettingsTextContent;

    /// <summary>The settings, with defaults for the values named in <c>LeftOut</c>, which are not what their fields
    /// hold; and what was wrong with the saved layout, which is told on its own when no value was left out.</summary>
    public sealed record Values : SettingsTextContent
    {
        public required AppSettings Settings { get; init; }

        public required IReadOnlyList<string> LeftOut { get; init; }

        public required SettingsProblem LayoutProblem { get; init; }
    }

    public TResult Match<TResult>(Func<NotJson, TResult> notJson, Func<Empty, TResult> empty, Func<Values, TResult> values)
    {
        return this switch
        {
            NotJson n => notJson(n),
            Empty e => empty(e),
            Values v => values(v),
            _ => throw new UnreachableException($"SettingsTextContent.Match does not handle {GetType().Name}."),
        };
    }
}
