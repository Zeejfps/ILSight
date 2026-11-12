namespace ILSight.SettingsKeeping;

/// <summary>What was wrong with the settings file, for the screen to tell in the chosen language. A
/// <c>Detail</c> is as the system or the JSON reader said it.</summary>
public abstract record SettingsProblem
{
    private SettingsProblem()
    {
    }

    /// <summary>How the file, which could not be read whole, was kept from being saved over; null for a problem that
    /// leaves the file as it can be read.</summary>
    public virtual SettingsKept? FindKept()
    {
        return null;
    }

    public sealed record None : SettingsProblem;

    /// <summary>The file could not be read, so the settings are the defaults.</summary>
    public sealed record NotRead(string Detail, SettingsKept Kept) : SettingsProblem
    {
        public override SettingsKept FindKept()
        {
            return Kept;
        }
    }

    /// <summary>The file is not JSON of the settings, so the settings are the defaults.</summary>
    public sealed record NotJson(string Detail, SettingsKept Kept) : SettingsProblem
    {
        public override SettingsKept FindKept()
        {
            return Kept;
        }
    }

    /// <summary>Some of the file's values, named by their fields, are not what those fields hold, so
    /// they are the defaults; the rest are as the file has them.</summary>
    public sealed record ValuesLeftOut(IReadOnlyList<string> Fields, SettingsKept Kept) : SettingsProblem
    {
        public override SettingsKept FindKept()
        {
            return Kept;
        }
    }

    /// <summary>The file holds <c>null</c>, so the settings are the defaults.</summary>
    public sealed record Empty : SettingsProblem;

    /// <summary>The saved layout lacks a node, a window or a list of panels, so the panels are where
    /// they start out.</summary>
    public sealed record LayoutPartsMissing : SettingsProblem;

    /// <summary>Two parts of the saved layout have one id, so the panels are where they start out.</summary>
    public sealed record LayoutPartsRepeated : SettingsProblem;
}
