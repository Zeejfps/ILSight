using ILSight.Core;
using ILSight.SettingsFormat;
using ILSight.AppSettingsFormat;

namespace ILSight;

/// <summary>Reads the settings file the app starts with through <see cref="IPorts"/>: each value read on its own, so a
/// value the file holds wrong leaves out only that one, for its default to stand in. A file that could not be read
/// whole is kept beside it first, so that saving the settings does not lose what it holds; one that can't be kept is
/// not saved over.</summary>
public sealed class SettingsReading(SettingsReading.IPorts ports)
{
    public interface IPorts
    {
        /// <summary>The text of the settings file at <paramref name="file"/>; none when there is no file.</summary>
        SettingsFileText ReadSettingsText(FullFilePath file);

        /// <summary>Copies the settings file at <paramref name="file"/> beside it, saying where; or says it could not.</summary>
        SettingsKept KeepUnreadable(FullFilePath file);
    }

    /// <summary>The settings in the file at <paramref name="file"/>, and what was wrong with it.</summary>
    public SettingsLoadResult ReadFile(FullFilePath file)
    {
        var (settings, problem) = ReadSettings(file);
        settings.FilePath = file;
        settings.CanSaveOverFile = CanSaveOverFile(problem);
        return new SettingsLoadResult(settings, problem);
    }

    /// <summary>Whether the settings may be saved over the file: not when it could not be read whole and could not
    /// be kept either.</summary>
    private static bool CanSaveOverFile(SettingsProblem problem)
    {
        var kept = problem.FindKept();
        return kept is not SettingsKept.NotSaved;
    }

    private SettingsLoadResult ReadSettings(FullFilePath file)
    {
        var read = ports.ReadSettingsText(file);
        return read.Match(
            none: _ => new SettingsLoadResult(new AppSettings(), new SettingsProblem.None()),
            text: text => ReadText(file, text.Content),
            notRead: notRead => new SettingsLoadResult(new AppSettings(), new SettingsProblem.NotRead(notRead.Detail, ports.KeepUnreadable(file))));
    }

    private SettingsLoadResult ReadText(FullFilePath file, string text)
    {
        var content = SettingsJson.ParseSettingsText(text);
        return content.Match(
            notJson: notJson => new SettingsLoadResult(new AppSettings(), new SettingsProblem.NotJson(notJson.Detail, ports.KeepUnreadable(file))),
            empty: _ => new SettingsLoadResult(new AppSettings(), new SettingsProblem.Empty()),
            values: values => ReadValues(file, values));
    }

    private SettingsLoadResult ReadValues(FullFilePath file, SettingsTextContent.Values values)
    {
        if (values.LeftOut.Count == 0)
        {
            return new SettingsLoadResult(values.Settings, values.LayoutProblem);
        }

        return new SettingsLoadResult(values.Settings, new SettingsProblem.ValuesLeftOut(values.LeftOut, ports.KeepUnreadable(file)));
    }
}
