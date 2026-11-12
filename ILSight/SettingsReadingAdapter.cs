using ILSight.Core;

namespace ILSight;

/// <summary>Reaches the settings file on disk, for <see cref="SettingsReading"/>.</summary>
public sealed class SettingsReadingAdapter : SettingsReading.IPorts
{
    private const string UnreadableExtension = ".unreadable.json";

    public SettingsFileText ReadSettingsText(FullFilePath file)
    {
        if (!File.Exists(file.Text))
        {
            return new SettingsFileText.None();
        }

        try
        {
            return new SettingsFileText.Text(File.ReadAllText(file.Text));
        }
        catch (IOException e)
        {
            return new SettingsFileText.NotRead(e.Message);
        }
        catch (UnauthorizedAccessException e)
        {
            return new SettingsFileText.NotRead(e.Message);
        }
    }

    /// <summary>Copies it to the same name with <c>.unreadable.json</c> for its extension, over any copy before it.</summary>
    public SettingsKept KeepUnreadable(FullFilePath file)
    {
        var copy = Path.ChangeExtension(file.Text, UnreadableExtension);
        try
        {
            File.Copy(file.Text, copy, overwrite: true);
            return new SettingsKept.CopiedTo(copy);
        }
        catch (IOException)
        {
            return new SettingsKept.NotSaved();
        }
        catch (UnauthorizedAccessException)
        {
            return new SettingsKept.NotSaved();
        }
    }
}
