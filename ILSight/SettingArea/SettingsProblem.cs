namespace ILSight.SettingArea;

/// <summary>What was wrong with the settings file, for the screen to tell in the chosen language. A
/// <c>Detail</c> is as the system or the JSON reader said it.</summary>
public union SettingsProblem(SettingsProblem.None, SettingsProblem.NotRead, SettingsProblem.NotJson,
    SettingsProblem.ValuesLeftOut, SettingsProblem.Empty, SettingsProblem.LayoutPartsMissing,
    SettingsProblem.LayoutPartsRepeated)
{
    /// <summary>How the file, which could not be read whole, was kept from being saved over; null for a problem that
    /// leaves the file as it can be read.</summary>
    public SettingsKept? FindKept()
    {
        return this switch
        {
            NotRead c => c.Kept,
            NotJson c => c.Kept,
            ValuesLeftOut c => c.Kept,
            None or Empty or LayoutPartsMissing or LayoutPartsRepeated => (SettingsKept?)null,
        };
    }

    public sealed record None;

    /// <summary>The file could not be read, so the settings are the defaults.</summary>
    public sealed record NotRead
    {
        public required string Detail { get; init; }

        public required SettingsKept Kept { get; init; }
    }

    /// <summary>The file is not JSON of the settings, so the settings are the defaults.</summary>
    public sealed record NotJson
    {
        public required string Detail { get; init; }

        public required SettingsKept Kept { get; init; }
    }

    /// <summary>Some of the file's values, named by their fields, are not what those fields hold, so
    /// they are the defaults; the rest are as the file has them.</summary>
    public sealed record ValuesLeftOut
    {
        public required IReadOnlyList<string> Fields { get; init; }

        public required SettingsKept Kept { get; init; }
    }

    /// <summary>The file holds <c>null</c>, so the settings are the defaults.</summary>
    public sealed record Empty;

    /// <summary>The saved layout lacks a node, a window or a list of panels, so the panels are where
    /// they start out.</summary>
    public sealed record LayoutPartsMissing;

    /// <summary>Two parts of the saved layout have one id, so the panels are where they start out.</summary>
    public sealed record LayoutPartsRepeated;
}
