using ILSight.Core;
using ILSight.AppSettingArea;

namespace ILSight.SettingArea;

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
        var read = ReadSettings(file);
        var problem = read.Problem;
        var settings = read.Settings with { FilePath = file, CanSaveOverFile = CanSaveOverFile(problem) };
        return new SettingsLoadResult { Settings = settings, Problem = problem };
    }

    private static bool CanSaveOverFile(SettingsProblem problem)
    {
        var kept = problem.FindKept();
        return kept is not SettingsKept.NotSaved;
    }

    private SettingsLoadResult ReadSettings(FullFilePath file)
    {
        var read = ports.ReadSettingsText(file);
        return read.Match(
            none: _ => new SettingsLoadResult { Settings = new AppSettings(), Problem = new SettingsProblem.None() },
            text: text => ReadText(file, text.Content),
            notRead: notRead => new SettingsLoadResult
            {
                Settings = new AppSettings(),
                Problem = new SettingsProblem.NotRead { Detail = notRead.Detail, Kept = ports.KeepUnreadable(file) },
            });
    }

    private SettingsLoadResult ReadText(FullFilePath file, string text)
    {
        var content = SettingsJson.ParseSettingsText(text);
        return content.Match(
            notJson: notJson => new SettingsLoadResult
            {
                Settings = new AppSettings(),
                Problem = new SettingsProblem.NotJson { Detail = notJson.Detail, Kept = ports.KeepUnreadable(file) },
            },
            empty: _ => new SettingsLoadResult { Settings = new AppSettings(), Problem = new SettingsProblem.Empty() },
            values: values => ReadValues(file, values));
    }

    private SettingsLoadResult ReadValues(FullFilePath file, SettingsTextContent.Values values)
    {
        if (values.LeftOut.Count == 0)
        {
            return new SettingsLoadResult { Settings = values.Settings, Problem = values.LayoutProblem };
        }

        return new SettingsLoadResult
        {
            Settings = values.Settings,
            Problem = new SettingsProblem.ValuesLeftOut { Fields = values.LeftOut, Kept = ports.KeepUnreadable(file) },
        };
    }
}
