namespace ILSight;

/// <summary>What was wrong with the settings file, for the screen to tell in the chosen language. A
/// <c>Detail</c> is as the system or the JSON reader said it.</summary>
public abstract record SettingsProblem
{
    private SettingsProblem()
    {
    }

    public sealed record None : SettingsProblem;

    /// <summary>The file could not be read, so the settings are the defaults.</summary>
    public sealed record NotRead(string Detail, SettingsKept Kept) : SettingsProblem;

    /// <summary>The file is not JSON of the settings, so the settings are the defaults.</summary>
    public sealed record NotJson(string Detail, SettingsKept Kept) : SettingsProblem;

    /// <summary>Some of the file's values, named by their fields, are not what those fields hold, so
    /// they are the defaults; the rest are as the file has them.</summary>
    public sealed record ValuesLeftOut(IReadOnlyList<string> Fields, SettingsKept Kept) : SettingsProblem;

    /// <summary>The file holds <c>null</c>, so the settings are the defaults.</summary>
    public sealed record Empty : SettingsProblem;

    /// <summary>The saved layout lacks a node, a window or a list of panels, so the panels are where
    /// they start out.</summary>
    public sealed record LayoutPartsMissing : SettingsProblem;

    /// <summary>Two parts of the saved layout have one id, so the panels are where they start out.</summary>
    public sealed record LayoutPartsRepeated : SettingsProblem;
}
