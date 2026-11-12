namespace ILSight.SettingArea;

/// <summary>How a settings file that could not be read whole is kept from being saved over.</summary>
public abstract record SettingsKept
{
    private SettingsKept()
    {
    }

    /// <summary>The file was copied to <c>Path</c>, and the settings are saved in its place.</summary>
    public sealed record CopiedTo(string Path) : SettingsKept;

    /// <summary>The file could not be copied either, so the settings are not saved this time.</summary>
    public sealed record NotSaved : SettingsKept;
}
