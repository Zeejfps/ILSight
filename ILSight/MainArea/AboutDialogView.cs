using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.CommandArea;

namespace ILSight.MainArea;

/// <summary>The app's icon beside its name, its version and what can be done about updates, over the
/// versions of the decompiler, the runtime and the system; then the licenses folder where the app ships
/// one, and Close.</summary>
internal static class AboutDialogView
{
    internal const string AppName = "ILSight";
    private const float IconScale = 4f;
    private const float NameScale = 1.25f;

    public static void Render(Ui ui, AboutDialog aboutDialog, Appearance appearance, IAppCommandWriter commands, string? shippedLicenses, AppState appState)
    {
        if (!aboutDialog.IsOpen)
        {
            return;
        }

        var t = appearance.Strings;
        var box = new Box { Width = Sizing.Grow(0f, AppLayout.AboutDialogWidth) };
        using var dialog = ui.Dialog("about", t.AboutTitle, ref aboutDialog.IsOpen, box);
        if (!dialog.IsOpen)
        {
            return;
        }

        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = ui.Theme.DialogGap }))
        {
            Identity(ui, appearance, commands, appState.Updates, appState.IsUpdateCheckAutomatic);
            Versions(ui, appearance);
        }
        Actions(ui, appearance, shippedLicenses, aboutDialog);
    }

    private static void Identity(Ui ui, Appearance appearance, IAppCommandWriter commands, AppUpdates updates, bool isCheckAutomatic)
    {
        var theme = ui.Theme;
        using var row = ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.DialogGap, AlignY = Align.Center });
        ui.AppIcon(theme.IconSize * IconScale);
        using var text = ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.Gap });
        NameAndVersion(ui, appearance);
        UpdateRow(ui, appearance, commands, updates);
        CheckAutomatically(ui, appearance, commands, isCheckAutomatic);
    }

    private static void NameAndVersion(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        var version = ui.Memo("about-version", t, static s => s.AboutVersion(AppVersions.App));
        using var lines = ui.Column(new Box { Width = Sizing.Grow(), Gap = theme.GapTight });
        ui.Text(AppName, size: theme.FontSizeTitle * NameScale, bold: true);
        ui.Text(version, color: theme.TextMuted, wrap: true);
    }

    // What came of a check goes beside the button, so that the result moves nothing.
    private static void UpdateRow(Ui ui, Appearance appearance, IAppCommandWriter commands, AppUpdates updates)
    {
        using var row = ui.Row(new Box { Width = Sizing.Grow(), Gap = ui.Theme.Gap, AlignY = Align.Center });
        UpdateButton(ui, appearance, commands, updates);
        UpdateResult(ui, appearance, updates.Status);
    }

    private static void UpdateButton(Ui ui, Appearance appearance, IAppCommandWriter commands, AppUpdates updates)
    {
        var t = appearance.Strings;
        if (updates.Status is UpdateStatus.Ready ready)
        {
            var label = ui.Memo("about-restart", (t, Version: AppUpdates.GetVersion(ready.Update)), static a => a.t.AboutRestartToUpdate(a.Version));
            if (ui.Button(label, id: "about-updates"))
            {
                var restart = updates.StartRestart();
                Restart(ui, commands, restart);
            }

            return;
        }
        var text = ButtonText(ui, appearance, updates.Status);
        if (ui.Button(text, enabled: updates.CanCheck(), id: "about-updates", variant: ButtonVariant.Neutral))
        {
            var check = updates.StartCheck(asked: true);
            Check(ui, commands, check);
        }
    }

    // The status shows the check from this frame on; it looks for a newer version once the frame is drawn.
    private static void Check(Ui ui, IAppCommandWriter commands, UpdateCheck? check)
    {
        if (check is null)
        {
            return;
        }

        Commands.StartSettling(commands, nameof(AppUpdates.CheckAsync), check, static (a, c, ct) => a.Updates.CheckAsync(c, ct),
            static (a, c, _) => a.Updates.FailCheck(c));
    }

    /// <summary>Asks for <paramref name="restart"/>, which the status shows from this frame on, to hand the update
    /// over once the frame is drawn; nothing when none is ready.</summary>
    internal static void Restart(Ui ui, IAppCommandWriter commands, UpdateRestart? restart)
    {
        if (restart is null)
        {
            return;
        }

        Commands.StartSettling(commands, nameof(AppUpdates.RestartToUpdateAsync), restart,
            static (a, r, ct) => a.Updates.RestartToUpdateAsync(r, a.ViewCommands, ct), static (a, r, _) => a.Updates.FailRestart(r));
    }

    private static string ButtonText(Ui ui, Appearance appearance, UpdateStatus status)
    {
        var t = appearance.Strings;
        if (status is UpdateStatus.Checking)
        {
            return t.AboutCheckingForUpdates;
        }

        if (status is UpdateStatus.Downloading downloading)
        {
            return ui.Memo("about-downloading", (t, downloading.Version), static a => a.t.AboutDownloadingUpdate(a.Version));
        }

        if (status is UpdateStatus.Restarting)
        {
            return t.AboutRestarting;
        }

        // Every other status, now and later, leaves the button asking to check.
        return t.AboutCheckForUpdates;
    }

    private static void UpdateResult(Ui ui, Appearance appearance, UpdateStatus status)
    {
        var t = appearance.Strings;
        if (status is UpdateStatus.UpToDate)
        {
            ui.Text(t.AboutUpToDate, color: ui.Theme.TextMuted, wrap: true);
            return;
        }

        if (status is UpdateStatus.Failed)
        {
            ui.Text(t.AboutUpdateFailed, color: ui.Theme.Danger, wrap: true);
            return;
        }

        // Every other status, now and later, is no check's result: nothing shows.
    }

    private static void CheckAutomatically(Ui ui, Appearance appearance, IAppCommandWriter commands, bool isCheckAutomatic)
    {
        var t = appearance.Strings;
        var check = isCheckAutomatic;
        if (ui.Checkbox(t.AboutCheckAutomatically, ref check, id: "about-check-automatically", compact: true))
        {
            Commands.Later(commands, check, static (a, on) => a.IsUpdateCheckAutomatic = on);
        }
    }

    private static void Versions(Ui ui, Appearance appearance)
    {
        var t = appearance.Strings;
        var labelWidth = MeasureLabels(ui, t);
        using var rows = ui.Column(new Box { Width = Sizing.Grow(), Gap = ui.Theme.GapTight });
        VersionRow(ui, t.AboutDecompiler, AppVersions.Decompiler, labelWidth);
        VersionRow(ui, t.AboutRuntime, AppVersions.Runtime, labelWidth);
        VersionRow(ui, t.AboutSystem, AppVersions.System, labelWidth);
    }

    private static float MeasureLabels(Ui ui, Strings t)
    {
        var size = ui.Theme.FontSizeSmall;
        var decompiler = ui.MeasureText(t.AboutDecompiler, size);
        var runtime = ui.MeasureText(t.AboutRuntime, size);
        var system = ui.MeasureText(t.AboutSystem, size);
        var widest = MathF.Max(decompiler, MathF.Max(runtime, system));
        return MathF.Ceiling(widest);
    }

    private static void VersionRow(Ui ui, string label, string version, float labelWidth)
    {
        var theme = ui.Theme;
        var size = theme.FontSizeSmall;
        using (ui.Row(new Box { Width = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            ui.Text(label, new Box { Width = Sizing.Fixed(labelWidth) }, color: theme.TextMuted, size: size);
            ui.Text(version, size: size, wrap: true);
        }
    }

    private static void Actions(Ui ui, Appearance appearance, string? shippedLicenses, AboutDialog aboutDialog)
    {
        var t = appearance.Strings;
        using (ui.DialogActions())
        {
            LicensesButton(ui, appearance, shippedLicenses);
            ui.Spacer();
            if (ui.Button(t.AboutClose, id: "about-close", variant: ButtonVariant.Neutral))
            {
                aboutDialog.Close();
            }
        }
    }

    private static void LicensesButton(Ui ui, Appearance appearance, string? shippedLicenses)
    {
        var t = appearance.Strings;
        var licenses = shippedLicenses;
        if (licenses is null)
        {
            return;
        }

        var icon = new UiIcon(appearance.Icons.AboutOpenLicensesFolder);
        if (ui.Button(t.AboutLicenses, id: "about-licenses", variant: ButtonVariant.Neutral, icon: icon))
        {
            ui.OpenFolder(licenses);
        }
    }
}
