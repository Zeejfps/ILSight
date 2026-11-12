using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Localization;
using static ILSight.Dialogs;

namespace ILSight;

/// <summary>The app's icon beside its name, its version and what can be done about updates, over the
/// versions of the decompiler, the runtime and the system; then the licenses folder where the app ships
/// one, and Close.</summary>
internal static class AboutDialogView
{
    internal const string AppName = "ILSight";
    private const string LicensesFolder = "licenses";
    private const float IconScale = 4f;
    private const float NameScale = 1.25f;

    private static readonly string? ShippedLicenses = FindShippedLicenses();

    public static void Render(Ui<UiState> ui, AppState appState)
    {
        var screen = ui.State;
        if (!screen.IsAboutOpen)
        {
            return;
        }

        var t = screen.Strings;
        var box = new Box { Width = Sizing.Grow(0f, AppLayout.AboutDialogWidth) };
        using var dialog = ui.Dialog("about", t.AboutTitle, ref screen.IsAboutOpen, box);
        if (!dialog.IsOpen)
        {
            return;
        }

        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = ui.Theme.DialogGap }))
        {
            Identity(ui, appState.Updates, appState.IsUpdateCheckAutomatic);
            Versions(ui);
        }
        Actions(ui);
    }

    /// <summary>The app's icon beside its name, its version and what can be done about updates.</summary>
    private static void Identity(Ui<UiState> ui, AppUpdates updates, bool isCheckAutomatic)
    {
        var theme = ui.Theme;
        using var row = ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.DialogGap, AlignY = Align.Center });
        ui.AppIcon(theme.IconSize * IconScale);
        using var text = ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.Gap });
        NameAndVersion(ui);
        UpdateRow(ui, updates);
        CheckAutomatically(ui, isCheckAutomatic);
    }

    private static void NameAndVersion(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var version = ui.Memo("about-version", t, static s => s.AboutVersion(AppVersions.App));
        using var lines = ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.GapTight });
        ui.Text(AppName, size: theme.FontSizeTitle * NameScale, bold: true);
        ui.Text(version, color: theme.TextMuted, wrap: true);
    }

    /// <summary>The update button with what came of a check beside it, so that the result moves nothing.</summary>
    private static void UpdateRow(Ui<UiState> ui, AppUpdates updates)
    {
        using var row = ui.Row(new Box { Width = Sizing.Grow(), Gap = ui.Theme.Gap, AlignY = Align.Center });
        UpdateButton(ui, updates);
        UpdateResult(ui, updates.Status);
    }

    /// <summary>Restarts into a downloaded update; else looks for one, in a copy that can update
    /// itself, or says what is under way.</summary>
    private static void UpdateButton(Ui<UiState> ui, AppUpdates updates)
    {
        var t = ui.State.Strings;
        if (updates.Status is UpdateStatus.Ready ready)
        {
            var label = ui.Memo("about-restart", (t, Version: AppUpdates.GetVersion(ready.Update)), static a => a.t.AboutRestartToUpdate(a.Version));
            if (ui.Button(label, id: "about-updates"))
            {
                Restart(ui, updates.StartRestart());
            }

            return;
        }
        var text = ButtonText(ui, updates.Status);
        if (ui.Button(text, enabled: updates.CanCheck(), id: "about-updates", variant: ButtonVariant.Neutral))
        {
            Check(ui, updates.StartCheck(asked: true));
        }
    }

    /// <summary>Asks for <paramref name="check"/>, which the status shows from this frame on, to look for a newer
    /// version once the frame is drawn; nothing when no check started.</summary>
    private static void Check(Ui<UiState> ui, UpdateCheck? check)
    {
        if (check is null)
        {
            return;
        }

        Commands.Start(ui.State, nameof(AppUpdates.CheckAsync), check, static (a, c, ct) => a.Updates.CheckAsync(c, ct));
    }

    /// <summary>Asks for <paramref name="restart"/>, which the status shows from this frame on, to hand the update
    /// over once the frame is drawn; nothing when none is ready.</summary>
    internal static void Restart(Ui<UiState> ui, UpdateRestart? restart)
    {
        if (restart is null)
        {
            return;
        }

        Commands.Start(ui.State, nameof(AppUpdates.RestartToUpdateAsync), restart, static (a, r, ct) => a.Updates.RestartToUpdateAsync(r, a.ViewCommands, ct));
    }

    private static string ButtonText(Ui<UiState> ui, UpdateStatus status)
    {
        var t = ui.State.Strings;
        switch (status)
        {
            case UpdateStatus.Checking:
                return t.AboutCheckingForUpdates;
            case UpdateStatus.Downloading downloading:
                return ui.Memo("about-downloading", (t, downloading.Version), static a => a.t.AboutDownloadingUpdate(a.Version));
            case UpdateStatus.Restarting:
                return t.AboutRestarting;
            default:
                return t.AboutCheckForUpdates;
        }
    }

    /// <summary>What a check the user asked for came to.</summary>
    private static void UpdateResult(Ui<UiState> ui, UpdateStatus status)
    {
        var t = ui.State.Strings;
        if (status is UpdateStatus.UpToDate)
        {
            ui.Text(t.AboutUpToDate, color: ui.Theme.TextMuted, wrap: true);
        }
        else if (status is UpdateStatus.Failed)
        {
            ui.Text(t.AboutUpdateFailed, color: ui.Theme.Danger, wrap: true);
        }
    }

    private static void CheckAutomatically(Ui<UiState> ui, bool isCheckAutomatic)
    {
        var t = ui.State.Strings;
        var check = isCheckAutomatic;
        if (ui.Checkbox(t.AboutCheckAutomatically, ref check, id: "about-check-automatically", compact: true))
        {
            Commands.Later(ui.State, check, static (a, on) => a.IsUpdateCheckAutomatic = on);
        }
    }

    /// <summary>A small row for each version, its name in a column as wide as the widest name.</summary>
    private static void Versions(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        var labelWidth = MeasureLabels(ui, t);
        using var rows = ui.Column(new Box { Width = Sizing.Grow(), Gap = ui.Theme.GapTight });
        VersionRow(ui, t.AboutDecompiler, AppVersions.Decompiler, labelWidth);
        VersionRow(ui, t.AboutRuntime, AppVersions.Runtime, labelWidth);
        VersionRow(ui, t.AboutSystem, AppVersions.System, labelWidth);
    }

    private static float MeasureLabels(Ui<UiState> ui, Strings t)
    {
        var size = ui.Theme.FontSizeSmall;
        var decompiler = ui.MeasureText(t.AboutDecompiler, size);
        var runtime = ui.MeasureText(t.AboutRuntime, size);
        var system = ui.MeasureText(t.AboutSystem, size);
        var widest = MathF.Max(decompiler, MathF.Max(runtime, system));
        return MathF.Ceiling(widest);
    }

    private static void VersionRow(Ui<UiState> ui, string label, string version, float labelWidth)
    {
        var theme = ui.Theme;
        var size = theme.FontSizeSmall;
        using (ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            ui.Text(label, new Box { Width = Sizing.Fixed(labelWidth) }, color: theme.TextMuted, size: size);
            ui.Text(version, size: size, wrap: true);
        }
    }

    /// <summary>The licenses folder at the left and Close at the right.</summary>
    private static void Actions(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        using (ui.DialogActions())
        {
            LicensesButton(ui);
            ui.Spacer();
            if (ui.Button(t.AboutClose, id: "about-close", variant: ButtonVariant.Neutral))
            {
                CloseAbout(ui.State);
            }
        }
    }

    /// <summary>Opens the folder of the licenses the app ships beside it, when it has one.</summary>
    private static void LicensesButton(Ui<UiState> ui)
    {
        var t = ui.State.Strings;
        if (ShippedLicenses is null)
        {
            return;
        }

        var icon = new UiIcon(ui.State.Icons.AboutOpenLicensesFolder);
        if (ui.Button(t.AboutLicenses, id: "about-licenses", variant: ButtonVariant.Neutral, icon: icon))
        {
            ui.OpenFolder(ShippedLicenses);
        }
    }

    private static string? FindShippedLicenses()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, LicensesFolder);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        return folder;
    }
}
