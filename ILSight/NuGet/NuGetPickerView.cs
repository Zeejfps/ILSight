using ZGF.Desktop;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ZGF.KeyboardModule;
using ILSight.Commanding;
using ILSight.UserState;
using ILSight.AppHosting;

namespace ILSight.NuGet;

/// <summary>
/// The "open from NuGet" dialog, in a window of its own: the query, whether prereleases count and the feed on top; a page of
/// the packages found on the left, each with its icon, authors, latest version and description, a
/// thin bar sliding over them while the next page or search loads, and the pages to step through
/// under them; the selected package's details on the right, where its version is picked; at the
/// bottom, Open, which downloads that version unless it is on disk, then opens it and closes.
/// </summary>
internal static class NuGetPickerView
{
    private const string QueryId = "nuget-query";
    private const string ListId = "nuget-list";
    private const float RowIconSide = 32f;
    private const float IconInset = 2f;
    private const float LoadingStripHeight = 3f;
    private const int DescriptionLines = 2;

    /// <summary>Opens the dialog, drawn from this frame on, and asks for the user's NuGet.Config to be read for
    /// it once the frame is drawn, unless it was.</summary>
    public static void Open(UiState uiState)
    {
        var picker = uiState.NuGetPicker;
        picker.Open();
        Commands.Start(uiState, nameof(NuGetPicker.ReadSetupAsync), picker, static (_, p, ct) => p.ReadSetupAsync(ct));
    }

    /// <summary>Draws the dialog, over the search for what was typed, which starts before it draws (see
    /// <see cref="AppFrames.FollowNuGetPicker"/>); then tells what it found whether it is still open, which stops
    /// what it started once it is not, however it was closed, freeing the images of the icons it showed. The icons
    /// and details it showed for the first time are fetched from the next frame on, which it asks for.</summary>
    public static void Render(Ui<UiState> ui)
    {
        var picker = ui.State.NuGetPicker;
        Dialog(ui, picker);
        FreeImages(ui, picker.Browser.StopUnlessOpen(picker.IsOpen));
        AskForFetchingFrame(ui, picker.Browser);
        DialogWindows.Settle(picker.Window, ui.Input.IsDown(PointerButton.Left));
    }

    /// <summary>Begins a request in <paramref name="navigation"/> to open the package the dialog downloaded, asks for it to
    /// be opened once the frame is drawn, and closes the dialog; nothing until a download is in.</summary>
    internal static void OpenDownloaded(Ui<UiState> ui, Navigation navigation, NuGetPicker picker)
    {
        var path = picker.Browser.TakeDownloaded();
        if (path is null)
        {
            return;
        }

        var file = AssemblyPath.Parse(path);
        if (file is not null)
        {
            Commands.OpenFilesLater(ui.State, navigation.BeginOpening([file]));
        }

        picker.IsOpen = false;
    }

    /// <summary>Has the next frame come at once while icons or details shown for the first time wait to be
    /// fetched, which the frame edge starts (see <see cref="AppFrames.StartFetching"/>).</summary>
    private static void AskForFetchingFrame(Ui<UiState> ui, NuGetBrowser browser)
    {
        if (!browser.HasDownloadsToStart)
        {
            return;
        }

        ui.RequestUpdateIn(0);
    }

    /// <summary>Frees the images of the icons the dialog showed. Goes through them by index, as it runs every
    /// frame: a foreach over the list would allocate its enumerator.</summary>
    private static void FreeImages(Ui<UiState> ui, IReadOnlyList<string> images)
    {
        for (var i = 0; i < images.Count; i++)
        {
            ui.RemoveImage(images[i]);
        }
    }

    private static void Dialog(Ui<UiState> ui, NuGetPicker picker)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using var dialog = ui.WindowDialog("nuget", t.NugetTitle, ref picker.IsOpen, AppLayout.NuGetDialogWidth, AppLayout.NuGetDialogHeight,
            ref picker.Window.Bounds, minWidth: AppLayout.NuGetDialogMinWidth, minHeight: AppLayout.NuGetDialogMinHeight);
        if (!dialog.IsOpen)
        {
            return;
        }

        TopRow(ui, picker);
        var open = false;
        using (ui.Row(new Box { Height = Sizing.Grow(), Gap = theme.Gap, AlignY = Align.Start }))
        {
            open |= PackagesColumn(ui, picker);
            picker.Browser.FollowSelection();
            NuGetDetailsView.Render(ui, picker);
        }
        open |= Footer(ui, picker);
        if (open)
        {
            Download(ui, picker.Browser, picker.Browser.StartDownload());
        }
    }

    /// <summary>Asks for <paramref name="download"/>, which the dialog draws from this frame on, to be fetched
    /// once the frame is drawn; nothing when no download started. Closing the dialog cancels it.</summary>
    private static void Download(Ui<UiState> ui, NuGetBrowser browser, PackageDownloadTicket? download)
    {
        if (download is null)
        {
            return;
        }

        Commands.Start(ui.State, nameof(NuGetBrowser.DownloadAsync), (Browser: browser, Download: download),
            static (_, d, ct) => d.Browser.DownloadAsync(d.Download, ct), download.Work.Token);
    }

    /// <summary>The query, whether prereleases count, and the feed. Enter in the query opens the
    /// selected package, or searches what was typed first; Enter in the feed searches it at once.</summary>
    private static void TopRow(Ui<UiState> ui, NuGetPicker picker)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            var queryId = ui.GetId(QueryId);
            if (ui.HasFocus(queryId))
            {
                MoveSelectionFromQuery(ui, picker);
            }

            if (picker.Browser.TryTakeQueryFocus())
            {
                ui.Focus(ui.GetId(QueryId));
            }

            var queryWidth = Sizing.Grow();
            if (ui.TextField(QueryId, ref picker.Query, t.NugetQuery, queryWidth, ui.State.Icons.SearchField, clearable: true))
            {
                Submit(ui, picker);
            }

            ui.Checkbox(t.NugetPrerelease, ref picker.IncludePrerelease, id: "nuget-prerelease");
            ui.Leaf(new Box { Width = theme.Gap });
            ui.Text(t.NugetSource, color: theme.TextMuted);
            var feedWidth = Sizing.Fixed(AppLayout.NuGetFeedWidth);
            if (ui.TextField("nuget-feed", ref picker.FeedUrl, t.NugetFeed, feedWidth))
            {
                picker.Browser.SearchNow();
            }

            KnownFeeds(ui, picker);
        }
    }

    /// <summary>Downloads the package picked, or searches what was typed first; either shows from this frame on.</summary>
    private static void Submit(Ui<UiState> ui, NuGetPicker picker)
    {
        Download(ui, picker.Browser, picker.Browser.Submit());
        ui.RequestUpdateIn(0);
    }

    private static void KnownFeeds(Ui<UiState> ui, NuGetPicker picker)
    {
        var sources = picker.Browser.Sources;
        using var menu = ui.DropdownButton("nuget-sources", ui.State.Strings.NugetKnownFeeds, enabled: sources.Count > 0, revision: sources.Count);
        if (!menu.IsOpen)
        {
            return;
        }

        for (var sourceIndex = 0; sourceIndex < sources.Count; sourceIndex++)
        {
            var source = sources[sourceIndex];
            var current = source.Url.Text == picker.FeedUrl;
            if (ui.MenuRadioItem(source.Name, current, id: source.Url.Text))
            {
                picker.PickFeed(source);
            }
        }
    }

    /// <summary>Walks the packages from the query, leaving Home and End to its text.</summary>
    private static void MoveSelectionFromQuery(Ui<UiState> ui, NuGetPicker picker)
    {
        var results = picker.Browser.Results;
        var page = ui.MeasureListPage(ui.GetId(ListId), RowHeight(ui));
        foreach (var stroke in ui.Input.Keys)
        {
            var movesCaret = stroke.Key is KeyboardKey.Home or KeyboardKey.End;
            if (!movesCaret)
            {
                MoveSelection(picker, stroke, results, page);
            }
        }
    }

    private static void MoveSelection(NuGetPicker picker, KeyStroke stroke, IReadOnlyList<FeedPackage> results, int page)
    {
        var selection = picker.Browser.Selection;
        // ApplyListKey takes -1 for "nothing selected".
        var index = ItemSelections.FindIndexIn(selection, results) ?? -1;
        if (!UiWidgets.ApplyListKey(stroke, ref index, results.Count, page))
        {
            return;
        }

        ItemSelections.Select(selection, results[index], results);
    }

    /// <summary>A package's row: its name line and the lines of its description.</summary>
    private static float RowHeight(Ui<UiState> ui)
    {
        var theme = ui.Theme;
        var name = ui.LineHeight(theme.FontSize);
        var description = DescriptionLines * ui.LineHeight(theme.FontSizeSmall);
        return name + description + theme.Gap * 2;
    }

    /// <summary>The packages, and the pager under them. True when a package was activated.</summary>
    private static bool PackagesColumn(Ui<UiState> ui, NuGetPicker picker)
    {
        var theme = ui.Theme;
        using (ui.Column(new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Gap = theme.GapTight }))
        {
            var activated = Packages(ui, picker);
            Pager(ui, picker);
            return activated;
        }
    }

    /// <summary>The packages found, under the strip that slides while another page or search loads
    /// over them and a note when another page failed to load; or why there are none. True when one
    /// was activated.</summary>
    private static bool Packages(Ui<UiState> ui, NuGetPicker picker)
    {
        var theme = ui.Theme;
        var frame = new Box
        {
            Height = Sizing.Grow(),
            Padding = theme.BorderWidth,
            Background = theme.Field,
            BorderColor = theme.Border,
            BorderWidth = theme.BorderWidth,
            Radius = theme.RadiusSmall,
            Clip = true,
        };
        using (ui.Panel(frame))
        {
            LoadingStrip(ui, picker);
            if (picker.Browser.Results.Count == 0)
            {
                NuGetPlaceholderView.Render(ui, picker);
                return false;
            }
            if (picker.Browser.SearchError is not null)
            {
                NuGetPlaceholderView.PageFailed(ui, picker);
            }

            return Rows(ui, picker);
        }
    }

    /// <summary>A thin sliding bar while a search runs over packages already shown; the same room,
    /// empty, otherwise, so the list never moves.</summary>
    private static void LoadingStrip(Ui<UiState> ui, NuGetPicker picker)
    {
        var refreshing = picker.Browser.IsSearching && picker.Browser.Results.Count > 0;
        if (refreshing)
        {
            ui.SlidingBar(height: LoadingStripHeight);
            return;
        }
        ui.Leaf(new Box { Width = Sizing.Grow(), Height = LoadingStripHeight });
    }

    private static bool Rows(Ui<UiState> ui, NuGetPicker picker)
    {
        var results = picker.Browser.Results;
        var selection = picker.Browser.Selection;
        var listId = ui.GetId(ListId);
        ItemSelections.ShowIn(selection, results);
        using var list = ui.SelectableList(listId, results.Count, selection.Rows, RowHeight(ui));
        for (var i = list.First; i < list.End; i++)
        {
            Row(ui, picker, list, results[i], i);
        }

        ItemSelections.TakeFrom(selection, results);
        return list.Activated;
    }

    /// <summary>A package's icon beside its name, authors and latest version over its description.</summary>
    private static void Row(Ui<UiState> ui, NuGetPicker picker, ListScope list, FeedPackage package, int index)
    {
        var theme = ui.Theme;
        var box = new Box { Gap = theme.Gap, AlignY = Align.Start, Padding = new Spacing(theme.ShellPaddingX, theme.Gap) };
        using (ui.Id(package.Id.Text))
        using (var row = list.Row(index, ui.GetId("row"), box))
        {
            DrawIcon(ui, picker, package, RowIconSide);
            using (ui.Column(new Box { Width = Sizing.Grow(), Gap = 0f }))
            {
                NameLine(ui, package, row.Selected);
                Description(ui, package, row.Selected);
            }
        }
    }

    /// <summary>The package's own icon once it is in, on a light tile when it is dark marks a dark
    /// theme would hide; else the stand-in every package shares.</summary>
    internal static void DrawIcon(Ui<UiState> ui, NuGetPicker picker, FeedPackage package, float side)
    {
        var icon = ShownIconOf(picker, package);
        if (icon is null)
        {
            StandIn(ui, side);
            return;
        }
        Upload(ui, icon);
        var backdrop = NeedsBackdrop(ui, icon);
        var tile = new Box
        {
            Width = side,
            Height = side,
            Padding = backdrop ? IconInset : 0f,
            Background = backdrop ? ui.Theme.Text : 0u,
            Radius = ui.Theme.RadiusSmall,
        };
        using (ui.Box(tile))
        {
            ui.Image(icon.ImageId, side, side, new Box { Width = Sizing.Grow(), Height = Sizing.Grow() });
        }
    }

    private static ShownIcon? ShownIconOf(NuGetPicker picker, FeedPackage package)
    {
        var icon = picker.Browser.EnsureIcon(package);
        if (icon is null)
        {
            return null;
        }

        return icon.Shown;
    }

    private static bool NeedsBackdrop(Ui<UiState> ui, ShownIcon icon)
    {
        if (!ui.Theme.IsDark)
        {
            return false;
        }

        return icon.IsDarkOnClear;
    }

    /// <summary>Gives the canvas the icon's pixels unless it holds them already.</summary>
    private static void Upload(Ui<UiState> ui, ShownIcon icon)
    {
        if (ui.HasImage(icon.ImageId))
        {
            return;
        }

        var pixels = icon.Pixels;
        ui.SetImage(icon.ImageId, pixels.Width, pixels.Height, pixels.Rgba);
    }

    /// <summary>A tile with the package glyph, for a package with no icon, or whose icon is loading.</summary>
    private static void StandIn(Ui<UiState> ui, float side)
    {
        var theme = ui.Theme;
        var tile = new Box
        {
            Width = side,
            Height = side,
            AlignX = Align.Center,
            AlignY = Align.Center,
            Background = theme.Control,
            Radius = theme.RadiusSmall,
        };
        using (ui.Box(tile))
        {
            ui.Icon(new UiIcon(ui.State.Icons.NugetPackage, theme.TextMuted));
        }
    }

    /// <summary>The package's id in bold, a mark when its owners are verified, who made it, and its latest version.</summary>
    private static void NameLine(Ui<UiState> ui, FeedPackage package, bool selected)
    {
        var theme = ui.Theme;
        var nameColor = selected ? theme.SelectionText : theme.Text;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            ui.Text(package.Id.Text, color: nameColor, bold: true, overflow: TextOverflow.Ellipsis);
            if (package.Verified)
            {
                ui.Icon(new UiIcon(ui.State.Icons.NugetVerified, theme.AccentText));
            }

            Authors(ui, package);
            ui.Text(package.Version.Text, color: theme.TextMuted, size: theme.FontSizeSmall);
        }
    }

    /// <summary>"by" the package's authors, taking what room the line has left; nothing when it names none.</summary>
    private static void Authors(Ui<UiState> ui, FeedPackage package)
    {
        var theme = ui.Theme;
        var box = new Box { Width = Sizing.Grow() };
        if (package.Authors.Length == 0)
        {
            ui.Leaf(box);
            return;
        }
        var authors = ui.Memo("authors", (package.Authors, ui.State.Strings), static a => a.Strings.NugetAuthors(a.Authors));
        ui.Text(authors, box, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
    }

    /// <summary>What the package says it is, over as many lines as its row has.</summary>
    private static void Description(Ui<UiState> ui, FeedPackage package, bool selected)
    {
        var theme = ui.Theme;
        var color = selected ? theme.SelectionText : theme.TextMuted;
        ui.Text(package.Description, color: color, size: theme.FontSizeSmall, wrap: true, maxLines: DescriptionLines);
    }

    /// <summary>How many packages matched, and how many of them the pages reach when fewer; then the
    /// page shown, or the one loading, between the buttons that step through them. The row keeps its
    /// height while there is nothing to page through.</summary>
    private static void Pager(Ui<UiState> ui, NuGetPicker picker)
    {
        var theme = ui.Theme;
        using (ui.Row(new Box { Height = theme.ControlHeight, Gap = theme.GapTight, AlignY = Align.Center }))
        {
            if (picker.Browser.TotalHits == 0)
            {
                return;
            }

            Count(ui, picker);
            PageButtons(ui, picker.Browser);
        }
    }

    private static void Count(Ui<UiState> ui, NuGetPicker picker)
    {
        var theme = ui.Theme;
        var args = (picker.Browser.TotalHits, picker.Browser.BrowsableHits, ui.State.Strings);
        var count = ui.Memo("nuget-count", args, static a => CountText(a.Strings, a.TotalHits, a.BrowsableHits));
        ui.Text(count, new Box { Width = Sizing.Grow() }, color: theme.TextMuted, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
    }

    private static string CountText(Strings t, int total, int browsable)
    {
        var count = t.NugetCount(total);
        if (browsable >= total)
        {
            return count;
        }

        return $"{count} · {t.NugetCapped(browsable)}";
    }

    private static void PageButtons(Ui<UiState> ui, NuGetBrowser browser)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        var icons = ui.State.Icons;
        var idle = !browser.IsSearching;
        if (ui.IconButton("nuget-previous", icons.NugetPreviousPage, t.NugetPreviousPage, browser.HasPreviousPage && idle, ButtonVariant.Neutral))
        {
            browser.ShowPreviousPage();
        }

        var args = (browser.Page, browser.RequestedPage, browser.PageCount, browser.IsLoadingPage, t);
        var label = ui.Memo("nuget-page", args, static a => PageText(a.t, a.Page, a.RequestedPage, a.PageCount, a.IsLoadingPage));
        ui.Text(label, new Box { Width = PageLabelWidth(ui, browser.PageCount) }, color: theme.TextMuted, size: theme.FontSizeSmall, align: Align.Center);
        if (ui.IconButton("nuget-next", icons.NugetNextPage, t.NugetNextPage, browser.HasNextPage && idle, ButtonVariant.Neutral))
        {
            browser.ShowNextPage();
        }
    }

    private static string PageText(Strings t, int page, int requested, int count, bool loading)
    {
        if (loading)
        {
            return t.NugetLoadingPage(requested + 1);
        }

        return t.NugetPage(page + 1, count);
    }

    /// <summary>As wide as the widest label the pager shows for <paramref name="pageCount"/> pages, the
    /// last page shown or loading, so the buttons beside it never move.</summary>
    private static float PageLabelWidth(Ui<UiState> ui, int pageCount)
    {
        var t = ui.State.Strings;
        var size = ui.Theme.FontSizeSmall;
        var shown = ui.Memo("nuget-page-widest", (pageCount, t), static a => a.t.NugetPage(a.pageCount, a.pageCount));
        var loading = ui.Memo("nuget-loading-widest", (pageCount, t), static a => a.t.NugetLoadingPage(a.pageCount));
        return MathF.Max(ui.MeasureText(shown, size), ui.MeasureText(loading, size));
    }

    /// <summary>What is being downloaded, Cancel and Open. True when Open was pressed.</summary>
    private static bool Footer(Ui<UiState> ui, NuGetPicker picker)
    {
        var t = ui.State.Strings;
        var theme = ui.Theme;
        using (ui.Row(new Box { Gap = theme.GapTight, AlignY = Align.Center }))
        {
            if (picker.Browser.IsDownloading)
            {
                Downloading(ui, picker.Browser);
            }

            ui.Spacer();
            if (ui.Button(t.NugetCancel, id: "nuget-cancel"))
            {
                picker.IsOpen = false;
            }

            return ui.Button(t.NugetOpen, enabled: picker.Browser.CanOpen, id: "nuget-open", variant: ButtonVariant.Primary, busy: picker.Browser.IsDownloading);
        }
    }

    private static void Downloading(Ui<UiState> ui, NuGetBrowser browser)
    {
        var theme = ui.Theme;
        var text = ui.Memo("nuget-downloading", (browser.DownloadingName, ui.State.Strings), static a => a.Strings.NugetDownloading(a.DownloadingName));
        ui.Text(text, color: theme.AccentText, size: theme.FontSizeSmall, overflow: TextOverflow.Ellipsis);
    }
}
