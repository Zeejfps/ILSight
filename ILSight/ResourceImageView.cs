using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Resources;
using ILSight.BookmarkArea;
using ILSight.CodeTooltipArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight;

/// <summary>A picture resource's tab: what it is, zoom buttons, a switch to its markup when it is
/// stored as such, and an Export button above the picture, zoomed on a framed surface, scrolling
/// when larger than the view.</summary>
internal static class ResourceImageView
{
    private sealed class Uploaded
    {
        public ResourceImage? Image;
        public float Scale;
    }

    public static void Render(Ui<UiState> ui, Navigation navigation, BookmarkList bookmarks, CodeTooltips tooltips, DocumentTab tab, TabContent.Picture shown)
    {
        var theme = ui.Theme;
        var view = DocumentPanels.GetView(ui.State.Documents, tab);
        var image = shown.Image;
        var hasSource = shown.Source is not null;
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = theme.PaneBackground });
        Toolbar(ui, ui.State.Appearance, ui.State.AppCommands, tab, view.Picture, image, hasSource);
        ui.Separator();
        if (view.Picture.ShowsSource)
        {
            DocumentsView.RenderCode(ui, navigation, bookmarks, tooltips, tab);
            return;
        }
        Picture(ui, view, image);
    }

    private static void Toolbar(Ui ui, Appearance appearance, IAppCommandWriter commands, DocumentTab tab, PictureView picture, ResourceImage image, bool hasSource)
    {
        var t = appearance.Strings;
        var theme = ui.Theme;
        using (ui.PaneToolbar(new Box { Gap = theme.Gap }))
        {
            ui.Text(t.ResourceImageInfo(image.Format, image.Width, image.Height), new Box { Width = Sizing.Grow() }, color: theme.TextMuted,
                overflow: TextOverflow.Ellipsis);
            if (!picture.ShowsSource)
            {
                ZoomButtons(ui, appearance, picture);
            }

            if (hasSource)
            {
                SourceButton(ui, appearance, picture);
            }

            var node = tab.Node;
            if (node is not null && node.HasResource && ui.Button(t.ResourceExport, id: "export", icon: new UiIcon(appearance.Icons.ResourceExport)))
            {
                PickExport(ui, appearance, commands, node);
            }
        }
    }

    private static void ZoomButtons(Ui ui, Appearance appearance, PictureView picture)
    {
        var zoom = picture.Zoom;
        var t = appearance.Strings;
        var theme = ui.Theme;
        if (ui.IconButton("zoom-out", appearance.Icons.ResourceZoomOut, t.ResourceZoomOut, zoom.CanZoomOut))
        {
            picture.ZoomOut();
        }

        ui.Text(t.ResourceZoomPercent(zoom.Percent), color: theme.TextMuted);
        if (ui.IconButton("zoom-in", appearance.Icons.ResourceZoomIn, t.ResourceZoomIn, zoom.CanZoomIn))
        {
            picture.ZoomIn();
        }

        if (ui.IconButton("zoom-reset", appearance.Icons.ResourceZoomReset, t.ResourceZoomReset, !zoom.IsOwnSize))
        {
            picture.ResetZoom();
        }

        ui.Separator(theme.IconSize);
    }

    private static void SourceButton(Ui ui, Appearance appearance, PictureView picture)
    {
        var t = appearance.Strings;
        var label = picture.ShowsSource ? t.ResourceShowPicture : t.ResourceShowSource;
        var icons = appearance.Icons;
        var glyph = picture.ShowsSource ? icons.ResourceShowPicture : icons.ResourceShowSource;
        if (ui.Button(label, id: "source", icon: new UiIcon(glyph)))
        {
            picture.ToggleSource();
        }
    }

    private static void Picture(Ui ui, TabView view, ResourceImage image)
    {
        var theme = ui.Theme;
        var scale = view.Picture.Zoom.Scale;
        Upload(ui, view.ImageId, image, scale);
        using (ui.Scroll("image", ScrollAxes.Both, new Box { Height = Sizing.Grow(), Padding = theme.ShellPaddingX }))
        {
            using (ui.Box(new Box
            {
                Padding = theme.GapTight,
                Background = theme.Surface,
                BorderColor = theme.Border,
                BorderWidth = theme.BorderWidth,
            }))
            {
                ui.Image(view.ImageId, image.Width * scale, image.Height * scale);
            }
        }
    }

    // A vector picture is drawn again, sharp, when the zoom changed; a raster one is stretched instead.
    private static void Upload(Ui ui, string imageId, ResourceImage image, float scale)
    {
        var uploaded = ui.GetState<Uploaded>(ui.GetId(imageId));
        if (!NeedsUpload(ui, imageId, uploaded, image, scale))
        {
            return;
        }

        var drawn = ResourceImages.DrawAt(image, scale);
        ui.SetImage(imageId, drawn.Width, drawn.Height, drawn.Rgba);
        uploaded.Image = image;
        uploaded.Scale = scale;
    }

    private static bool NeedsUpload(Ui ui, string imageId, Uploaded uploaded, ResourceImage image, float scale)
    {
        if (!ReferenceEquals(uploaded.Image, image))
        {
            return true;
        }

        if (!ui.HasImage(imageId))
        {
            return true;
        }

        return image.IsVector && uploaded.Scale != scale;
    }

    /// <summary>Asks where to export the resource <paramref name="node"/> stands for, then writes it there.</summary>
    public static void PickExport(Ui ui, Appearance appearance, IAppCommandWriter commands, BrowseNode node)
    {
        var t = appearance.Strings;
        var fileName = node.ResourceFileName ?? throw new InvalidOperationException($"{node.Text} is no resource.");
        ui.PickSaveFile(t.ResourceExportTitle, null, fileName, ExportFilters(t, fileName),
            path => Commands.Later(commands, (node, path), static (a, s) => ExportArea.ExportCommands.SaveResource(a, s.node, s.path)));
    }

    private const string XamlExtension = ".xaml";

    private static FileFilter[] ExportFilters(Strings t, string fileName)
    {
        var all = new FileFilter(t.FileFilterAll, ["*.*"]);
        if (!IsXamlName(fileName))
        {
            return [all];
        }

        return [new FileFilter(t.FileFilterXaml, ["*" + XamlExtension]), all];
    }

    private static bool IsXamlName(string fileName)
    {
        return Path.GetExtension(fileName).Equals(XamlExtension, StringComparison.OrdinalIgnoreCase);
    }
}
