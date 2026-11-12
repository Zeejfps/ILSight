using ILSight.Core;

namespace ILSight.SettingsKeeping;

/// <summary>Reaches the app's error reports and the settings file, for <see cref="SettingsSaving"/>.</summary>
public sealed class SettingsSavingAdapter(ErrorLog errors) : SettingsSaving.IPorts
{
    public void ReportError(AppProblem problem)
    {
        Errors.Report(errors, problem);
    }

    /// <summary>Writes a file beside it first, then moves it over the file.</summary>
    public SettingsWriteResult WriteSettingsFile(FullFilePath file, string text)
    {
        var temporary = file.Text + ".tmp";
        try
        {
            Directory.CreateDirectory(file.Folder);
            File.WriteAllText(temporary, text);
            File.Move(temporary, file.Text, overwrite: true);
            return new SettingsWriteResult.Written();
        }
        catch (IOException e)
        {
            return Failed(file, e);
        }
        catch (UnauthorizedAccessException e)
        {
            return Failed(file, e);
        }
    }

    private static SettingsWriteResult Failed(FullFilePath file, Exception e)
    {
        return new SettingsWriteResult.Failed($"{file}{Environment.NewLine}{e.Message}");
    }
}
