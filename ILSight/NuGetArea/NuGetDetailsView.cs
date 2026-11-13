using System.Globalization;
using ZGF.Gui;
using ZGF.Gui.Immediate;
using TextOverflow = ZGF.Gui.Immediate.TextOverflow;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;

namespace ILSight.NuGetArea;

/// <summary>
/// The selected package's details beside the "open from NuGet" dialog's list: its icon, id and
/// authors, the version to open, warnings when that version is deprecated or vulnerable, its whole
/// description, facts about it (downloads, owners, when published, license, project, size, tags) and
/// what it depends on for each framework, the parts only the feed's catalog knows read for the
/// version picked.
/// </summary>
internal static class NuGetDetailsView
{
    private const float IconSide = 48f;
    private const float LabelWidth = 96f;
    private const float TitleScale = 1.25f;
    private const float VersionMinWidth = 96f;
    private const float VersionMaxWidth = 200f;
    private const double Kilobyte = 1024.0;

    public static void Render(Ui<UiState> ui, NuGetPicker picker)
    {
        var theme = ui.Theme;
        var frame = new Box
        {
            Width = AppLayout.NuGetDetailsWidth,
            Height = Sizing.Grow(),
            Background = theme.Surface,
            BorderColor = theme.Border,
            BorderWidth = theme.BorderWidth,
            Radius = theme.RadiusSmall,
            Clip = true,
        };
        using (ui.Panel(frame))
        {
            var package = picker.Browser.Selected;
            if (package == null)
            {
                NoSelection(ui);
                return;
            }
            Details(ui, picker, package);
        }
    }

    private static void NoSelection(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        var box = new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Padding = theme.ShellPaddingX, AlignX = Align.Center, AlignY = Align.Center };
        using (ui.Column(box))
        {
            ui.Text(ui.State.Strings.NugetNoSelection, color: theme.TextMuted, wrap: true, align: Align.Center);
        }
    }

    private static void Details(Ui<UiState> ui, NuGetPicker picker, FeedPackage package)
    {
        var theme = ui.Theme;
        // Each package scrolls on its own, so another one starts at its top.
        using (ui.Id(package.Id.Text))
        {
            var details = picker.Browser.Version != null ? picker.Browser.EnsureDetails(package, picker.Browser.Version) : null;
            using (ui.Column(new Box { Width = Sizing.Grow(), Padding = theme.ShellPaddingX, Gap = theme.Gap }))
            {
                Header(ui, picker, package);
                VersionRow(ui, picker, package, details);
            }
            ui.Leaf(new Box { Width = Sizing.Grow(), Height = theme.SeparatorWidth, Background = theme.Separator });
            using (ui.Scroll("nuget-details", new Box { Height = Sizing.Grow() }))
            {
                Body(ui, picker, package, details);
            }
        }
    }

    private static void Body(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, VersionDetails? details)
    {
        var theme = ui.Theme;
        using (ui.Column(new Box { Width = Sizing.Grow(), Padding = theme.ShellPaddingX, Gap = theme.Gap }))
        {
            Warnings(ui, details);
            if (package.Description.Length > 0)
            {
                ui.Text(package.Description, wrap: true);
            }

            Facts(ui, picker, package, details);
            Dependencies(ui, details);
        }
    }

    private static void Header(Ui<UiState> ui, NuGetPicker picker, FeedPackage package)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
        {
            NuGetPickerView.DrawIcon(ui, picker, package, IconSide);
            using (ui.Column(new Box { Width = Sizing.Grow(), Gap = 0f }))
            {
                ui.Text(package.Id.Text, size: theme.FontSize * TitleScale, bold: true, wrap: true);
                if (package.Authors.Length > 0)
                {
                    ui.Text(AuthorsText(ui, package), color: theme.TextMuted, wrap: true);
                }

                if (package.Verified)
                {
                    Verified(ui);
                }
            }
        }
    }

    private static string AuthorsText(Ui<UiState> ui, FeedPackage package)
    {
        return ui.Memo("authors", (package.Authors, ui.State.Strings), static a => a.Strings.NugetAuthors(a.Authors));
    }

    private static void Verified(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            ui.Icon(new UiIcon(ui.State.Icons.NugetVerified, theme.AccentText));
            ui.Text(ui.State.Strings.NugetVerified, color: theme.AccentText, size: theme.FontSizeSmall);
        }
    }

    private static void VersionRow(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, VersionDetails? details)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Center }))
        {
            ui.Text(t.NugetVersion, new Box { Width = LabelWidth }, color: theme.TextMuted);
            var choice = picker.Browser.Choice;
            if (choice is not null)
            {
                VersionSelect(ui, picker, choice);
            }

            DetailsStatus(ui, picker, package, details);
        }
    }

    private static void VersionSelect(Ui<UiState> ui, NuGetPicker picker, VersionChoice choice)
    {
        var t = ui.State.Strings;
        var width = Math.Clamp(ui.SelectButtonWidth(choice.Texts), VersionMinWidth, VersionMaxWidth);
        var enabled = !picker.Browser.IsDownloading && picker.Browser.ShowsWhatWasAsked;
        var version = choice.Picked;
        var selectWidth = Sizing.Fixed(width);
        if (ui.Select("version", ref version, choice.Versions, VersionLabel, selectWidth, t.NugetVersion, enabled))
        {
            picker.Browser.PickVersion(version);
        }
    }

    private static string VersionLabel(PackageVersion version)
    {
        return version.Text;
    }

    private static void DetailsStatus(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, VersionDetails? details)
    {
        if (details is null)
        {
            return;
        }

        details.Switch((ui, picker, package),
            loading: static (s, _) => LoadingStatus(s.ui),
            read: static (_, _) => { },
            failed: static (s, _) => RetryStatus(s.ui, s.picker, s.package));
    }

    private static void LoadingStatus(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        ui.Spinner(theme.Surface);
        ui.Text(ui.State.Strings.NugetLoadingDetails, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
    }

    private static void RetryStatus(Ui<UiState> ui, NuGetPicker picker, FeedPackage package)
    {
        if (ui.Button(ui.State.Strings.NugetRetry, id: "retry-details", variant: ButtonVariant.Ghost) && picker.Browser.Version != null)
        {
            picker.Browser.RetryDetails(package, picker.Browser.Version);
        }
    }

    private static void Warnings(Ui<UiState> ui, VersionDetails? details)
    {
        if (details is VersionDetails.Failed failed)
        {
            DetailsFailed(ui, failed.Error);
            return;
        }
        var read = ReadOf(details);
        if (read == null)
        {
            return;
        }

        if (read.Deprecation != null)
        {
            Deprecation(ui, read.Deprecation);
        }

        if (read.Vulnerabilities.Count > 0)
        {
            NuGetPlaceholderView.Warning(ui, ui.State.Strings.NugetVulnerable(read.Vulnerabilities.Count));
        }
    }

    private static void DetailsFailed(Ui<UiState> ui, string error)
    {
        var theme = ui.Theme;
        NuGetPlaceholderView.Warning(ui, ui.State.Strings.NugetDetailsFailed);
        ui.Text(error, color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
    }

    private static void Deprecation(Ui<UiState> ui, PackageDeprecation deprecation)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        NuGetPlaceholderView.Warning(ui, t.NugetDeprecated);
        if (deprecation.Message.Length > 0)
        {
            ui.Text(deprecation.Message, color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
        }

        if (deprecation.AlternatePackage.Length > 0)
        {
            ui.Text(t.NugetUseInstead(deprecation.AlternatePackage), color: theme.TextMuted, size: theme.FontSizeSmall, wrap: true);
        }
    }

    private static void Facts(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, VersionDetails? details)
    {
        var t = ui.State.Strings;
        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = ui.Theme.GapTight }))
        {
            if (package.TotalDownloads > 0)
            {
                Fact(ui, t.NugetFactDownloads, DownloadsText(ui, package.TotalDownloads));
            }

            if (package.Owners.Length > 0)
            {
                Fact(ui, t.NugetFactOwners, package.Owners);
            }

            CatalogFacts(ui, picker, package, details);
            if (package.Tags.Count > 0)
            {
                Fact(ui, t.NugetFactTags, TagsText(ui, package.Tags));
            }
        }
    }

    private static string DownloadsText(Ui<UiState> ui, long downloads)
    {
        return ui.Memo("downloads", (downloads, ui.State.Strings.Culture), static a => a.downloads.ToString("N0", a.Culture));
    }

    private static string TagsText(Ui<UiState> ui, IReadOnlyList<string> tags)
    {
        return ui.Memo("tags", tags, static list => string.Join(", ", list));
    }

    private static void CatalogFacts(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, VersionDetails? details)
    {
        var t = ui.State.Strings;
        var read = ReadOf(details);
        if (read == null)
        {
            // Until the catalog is read, the license and project the search gave are the latest version's.
            if (picker.Browser.Version != package.Version)
            {
                return;
            }

            LinkFact(ui, "project", t.NugetFactProject, TextOf(package.ProjectUrl), package.ProjectUrl);
            LinkFact(ui, "license", t.NugetFactLicense, TextOf(package.LicenseUrl), package.LicenseUrl);
            return;
        }
        if (read.Published is not null)
        {
            Fact(ui, t.NugetFactPublished, PublishedText(ui, read.Published.Value));
        }

        LinkFact(ui, "license", t.NugetFactLicense, LicenseText(read), read.LicenseUrl);
        LinkFact(ui, "project", t.NugetFactProject, TextOf(read.ProjectUrl), read.ProjectUrl);
        if (read.PackageSize > 0)
        {
            Fact(ui, t.NugetFactSize, SizeText(ui, read.PackageSize));
        }
    }

    private static string PublishedText(Ui<UiState> ui, DateTimeOffset published)
    {
        return ui.Memo("published", (published, ui.State.Strings.Culture), static a => FormatDate(a.published, a.Culture));
    }

    private static string SizeText(Ui<UiState> ui, long bytes)
    {
        return ui.Memo("size", (bytes, ui.State.Strings.Culture), static a => FormatSize(a.bytes, a.Culture));
    }

    private static PackageVersionDetails? ReadOf(VersionDetails? details)
    {
        if (details is not VersionDetails.Read read)
        {
            return null;
        }

        return read.Details;
    }

    private static string FormatDate(DateTimeOffset date, CultureInfo culture)
    {
        return date.ToLocalTime().ToString("d", culture);
    }

    private static string FormatSize(long bytes, CultureInfo culture)
    {
        var kilobytes = bytes / Kilobyte;
        if (kilobytes < Kilobyte)
        {
            return string.Format(culture, "{0:N1} KB", kilobytes);
        }

        return string.Format(culture, "{0:N1} MB", kilobytes / Kilobyte);
    }

    private static string LicenseText(PackageVersionDetails details)
    {
        if (details.License.Length > 0)
        {
            return details.License;
        }

        return TextOf(details.LicenseUrl);
    }

    private static string TextOf(WebUrl? url)
    {
        return url == null ? "" : url.Text;
    }

    private static void Fact(Ui<UiState> ui, string label, string value)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Start }))
        {
            ui.Text(label, new Box { Width = LabelWidth }, color: theme.TextMuted, size: theme.FontSizeSmall);
            ui.Text(value, size: theme.FontSizeSmall, wrap: true);
        }
    }

    private static void LinkFact(Ui<UiState> ui, string id, string label, string text, WebUrl? url)
    {
        if (text.Length == 0)
        {
            return;
        }

        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.Gap, AlignY = Align.Start }))
        {
            ui.Text(label, new Box { Width = LabelWidth }, color: theme.TextMuted, size: theme.FontSizeSmall);
            Link(ui, id, text, url);
        }
    }

    private static void Link(Ui<UiState> ui, string id, string text, WebUrl? url)
    {
        var theme = ui.Theme;
        if (url == null)
        {
            ui.Text(text, size: theme.FontSizeSmall, wrap: true);
            return;
        }
        var style = ui.StyleText(theme.AccentText, theme.FontSizeSmall);
        var runs = LinkRunsOf(ui, id, text, url.Text, style);
        var click = ui.RichText(runs, new Box { Width = Sizing.Grow() }, id: ui.GetId(id));
        if (click.Link is string target)
        {
            ui.OpenUrl(target);
        }
    }

    // Rich text asks for the same runs while a link's text, URL and style stay the same.
    private static IReadOnlyList<RichTextRun> LinkRunsOf(Ui<UiState> ui, string id, string text, string url, TextStyle style)
    {
        var kept = ui.GetState<LinkRuns>(ui.GetId(id + "-runs"));
        if (kept.Runs.Count == 0 || kept.Text != text || kept.Url != url || kept.Style != style)
        {
            KeepRuns(kept, text, url, style);
        }

        return kept.Runs;
    }

    private sealed class LinkRuns
    {
        public string Text = "";
        public string Url = "";
        public TextStyle? Style;
        public IReadOnlyList<RichTextRun> Runs = [];
    }

    private static void KeepRuns(LinkRuns kept, string text, string url, TextStyle style)
    {
        kept.Text = text;
        kept.Url = url;
        kept.Style = style;
        kept.Runs = [new RichTextRun(text, style, Link: url)];
    }

    private static void Dependencies(Ui<UiState> ui, VersionDetails? details)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var read = ReadOf(details);
        if (read == null)
        {
            return;
        }

        ui.Text(t.NugetDependencies, bold: true);
        if (read.DependencyGroups.Count == 0)
        {
            ui.Text(t.NugetNoDependencies, color: theme.TextMuted, size: theme.FontSizeSmall);
            return;
        }
        for (var i = 0; i < read.DependencyGroups.Count; i++)
        {
            Group(ui, read.DependencyGroups[i], i);
        }
    }

    private static void Group(Ui<UiState> ui, DependencyGroup group, int index)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var framework = group.TargetFramework.Length > 0 ? group.TargetFramework : t.NugetAnyFramework;
        using (ui.Id(index))
        using (ui.Column(new Box { Width = Sizing.Grow(), Gap = 0f }))
        {
            ui.Text(framework, color: theme.TextMuted, size: theme.FontSizeSmall, bold: true);
            if (group.Dependencies.Count == 0)
            {
                ui.Text(t.NugetNoDependencies, color: theme.TextMuted, size: theme.FontSizeSmall);
            }

            var dependencies = group.Dependencies;
            for (var dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
            {
                var dependency = dependencies[dependencyIndex];
                Dependency(ui, dependency);
            }
        }
    }

    private static void Dependency(Ui<UiState> ui, PackageDependency dependency)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center, Padding = new Spacing(theme.Gap, 0f, 0f, 0f) }))
        {
            ui.Text(dependency.Id, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
            var range = ui.Memo(dependency.Id, dependency.Range, static r => VersionRange.Describe(r));
            ui.Text(range, color: theme.TextMuted, size: theme.FontSizeSmall);
        }
    }
}
