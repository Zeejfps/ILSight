using ILSight.AppSettingArea;

namespace ILSight.SettingArea;

/// <summary>What the settings file's text holds: not JSON of the settings, <c>null</c>, or the settings, each value
/// read on its own.</summary>
public union SettingsTextContent(SettingsTextContent.NotJson, SettingsTextContent.Empty, SettingsTextContent.Values)
{
    /// <summary>The text is not JSON of the settings; <c>Detail</c> is as the JSON reader said it.</summary>
    public sealed record NotJson(string Detail);

    /// <summary>The text holds <c>null</c>.</summary>
    public sealed record Empty;

    /// <summary>The settings, with defaults for the values named in <c>LeftOut</c>, which are not what their fields
    /// hold; and what was wrong with the saved layout, which is told on its own when no value was left out.</summary>
    public sealed record Values
    {
        public required AppSettings Settings { get; init; }

        public required IReadOnlyList<string> LeftOut { get; init; }

        public required SettingsProblem LayoutProblem { get; init; }
    }
}
