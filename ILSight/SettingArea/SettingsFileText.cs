namespace ILSight.SettingArea;

/// <summary>What reading the settings file gave: no file, its text, or why it could not be read, as the system
/// said it.</summary>
public union SettingsFileText(SettingsFileText.None, SettingsFileText.Text, SettingsFileText.NotRead)
{
    public sealed record None;

    public sealed record Text(string Content);

    public sealed record NotRead(string Detail);
}
