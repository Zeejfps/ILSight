using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ZGF.KeyboardModule;
using ILSight.CodeTooltipArea;
using ILSight.DiagramCameraArea;
using ILSight.CommandArea;
using ILSight.DocumentViewArea;

namespace ILSight.DiagramArea;

/// <summary>A diagram's tab: the cards and the links between them, under the bar over the document, which
/// holds its zoom, fit and member filter.
/// Dragging pans, the wheel scrolls and with Ctrl zooms; a click selects a card or folds a part of it, a
/// double-click opens the code of the type or member under the pointer, and resting on one shows its
/// documentation. A card's menu grows the diagram along its relations or takes it off.</summary>
internal static class DiagramView
{
    private const float IsALineWidth = 1.5f;
    private const float HasALineWidth = 1.25f;
    private const float DashLength = 6f;
    private const float GapLength = 4f;
    private const float HollowArrowWidth = 14f;
    private const float OpenArrowWidth = 11f;
    private const float DiamondWidth = 9f;
    private const float LabelBorderWidth = 1f;
    private const float SelectedBorderWidth = 2f;
    private const float ChevronHalf = 3.5f;
    private const float OverlayInset = 0.75f;
    private const float MinTextSize = 3f;
    private const float MinIconSize = 4f;
    private const float TextRightPadding = 8f;

    // In font sizes.
    private const float UnderlineDrop = 0.5f;

    private const string CanvasId = "diagram";
    private const string MenuId = "diagram-menu";
    private const float PanThreshold = 4f;
    private const float WheelZoomStep = 1.1f;

    /// <summary>The diagram <paramref name="tab"/> shows. Opening a type's or a member's code from it begins following
    /// it in <paramref name="navigation"/> in this frame, queuing only its lookup.</summary>
    public static void Render(Ui ui, DocumentViews documents, ScreenDisplay display, Appearance appearance, IAppCommandWriter commands, Navigation navigation, CodeTooltips tooltips, DocumentTab tab, TypeDiagram diagram)
    {
        var view = documents.GetView(tab).Diagram;
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = ui.Theme.PaneBackground });
        if (!display.Options.ShowOmnibar)
        {
            OwnToolbar(ui, appearance, documents, tab);
        }

        if (diagram.Cards.Count == 0)
        {
            EmptyHint(ui, appearance, diagram);
            return;
        }
        ReadFailure(ui, appearance, diagram.ReadFailure);
        DiagramFaces.MeasureIfChanged(ui, appearance, commands, view, diagram);
        Canvas(ui, commands, appearance, navigation, tooltips, view, diagram);
        CardMenu(ui, appearance, commands, navigation, view, diagram, tab.ClosedAssembly);
    }

    private static void OwnToolbar(Ui ui, Appearance appearance, DocumentViews documents, DocumentTab tab)
    {
        using (ui.PaneToolbar())
        {
            Tools(ui, appearance, documents, tab);
        }

        ui.Separator();
    }

    /// <summary>The diagram's tools, for the bar over the document: zoom, fit and the member filter.</summary>
    public static void Tools(Ui ui, Appearance appearance, DocumentViews documents, DocumentTab tab)
    {
        var t = appearance.Strings;
        var icons = appearance.Icons;
        var view = documents.GetView(tab).Diagram;
        var camera = view.Camera;
        var area = GetCanvasRect(ui);
        using (ui.Row(new Box { Gap = ui.Theme.Gap, AlignY = Align.Center }))
        {
            if (ui.IconButton("zoom-out", icons.ResourceZoomOut, t.ResourceZoomOut, camera.CanZoomOut))
            {
                camera.ZoomOut(area);
            }

            ui.Text(t.ResourceZoomPercent(camera.Percent), color: ui.Theme.TextMuted);
            if (ui.IconButton("zoom-in", icons.ResourceZoomIn, t.ResourceZoomIn, camera.CanZoomIn))
            {
                camera.ZoomIn(area);
            }

            if (ui.Button(t.DiagramFit, id: "fit"))
            {
                FitToView(view, area);
            }

            ui.Separator(ui.Theme.IconSize);
            ShowHiddenMembersCheckbox(ui, appearance, view);
        }
    }

    private static void ShowHiddenMembersCheckbox(Ui ui, Appearance appearance, DiagramViewState view)
    {
        var shows = view.ShowsHiddenMembers;
        if (ui.Checkbox(appearance.Strings.DiagramShowHiddenMembers, ref shows, id: "show-hidden-members"))
        {
            Diagrams.ShowHiddenMembers(view, shows);
        }
    }

    // Where the cards were drawn on the last frame: this one hasn't laid them out yet.
    private static UiRect GetCanvasRect(Ui ui)
    {
        ui.TryGetRect(ui.GetId(CanvasId), out var rect);
        return rect;
    }

    private static void FitToView(DiagramViewState view, UiRect area)
    {
        var layout = view.Transition.Layout;
        if (layout is null)
        {
            return;
        }

        view.Camera.Fit(layout.Width, layout.Height, area);
    }

    private static void EmptyHint(Ui ui, Appearance appearance, TypeDiagram diagram)
    {
        var t = appearance.Strings;
        var failure = diagram.ReadFailure;
        if (failure is not null && !diagram.IsBusy)
        {
            DocumentsView.Hint(ui, ui.Memo("diagram-read-failed", (failure, t), static a => a.t.DiagramReadFailed(a.failure)));
            return;
        }

        var text = diagram.IsBusy ? t.DiagramReading : t.DiagramEmpty;
        DocumentsView.Hint(ui, text);
    }

    // Above the cards it shows, the last read that failed, until a read is started again.
    private static void ReadFailure(Ui ui, Appearance appearance, string? failure)
    {
        if (failure is null)
        {
            return;
        }

        var theme = ui.Theme;
        var text = ui.Memo("diagram-read-failed", (failure, t: appearance.Strings), static a => a.t.DiagramReadFailed(a.failure));
        ui.Text(text, new Box { Width = Sizing.Grow(), Padding = new Spacing(theme.Gap, theme.GapTight) }, color: theme.Warning, size: theme.FontSizeSmall,
            wrap: true);
    }

    private static void Canvas(Ui ui, IAppCommandWriter commands, Appearance appearance, Navigation navigation, CodeTooltips tooltips, DiagramViewState view, TypeDiagram diagram)
    {
        var id = ui.GetId(CanvasId);
        var i = ui.Interact(id);
        var hasRect = ui.TryGetRect(id, out var rect);
        var selectedBefore = FindSelectedPlace(view);
        FollowLayout(ui, view, diagram);
        HoldSelectedCard(view, selectedBefore);
        if (hasRect)
        {
            BringIntoView(view, diagram, rect);
        }

        var hit = GetHit(ui, view, diagram, i, hasRect, rect);
        Scroll(ui, view, rect, i.Hovered);
        Press(view, i, hit);
        Pan(ui, view, i);
        SettleCamera(view, i);
        Click(ui, commands, navigation, view, diagram, i, hit);
        ShowHandOverClickable(ui, view, i, hit);
        FillFrame(ui, appearance, view, diagram, hit);
        using (ui.Box(id, new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Background = ui.Theme.EditorBackground, Clip = true }))
        {
            ui.Paint(view.Frame, Paint);
        }

        if (hasRect)
        {
            MemberTooltip(ui, appearance, commands, navigation, tooltips, view, diagram, id, IsPanning(view, i), hit);
        }
    }

    private static PointF? FindSelectedPlace(DiagramViewState view)
    {
        var selected = view.Selected;
        if (selected == null)
        {
            return null;
        }

        return view.Transition.FindCardPlace(selected);
    }

    private static void FollowLayout(Ui ui, DiagramViewState view, TypeDiagram diagram)
    {
        view.FollowLayout(diagram.Layout);
        view.Transition.Advance(ui.DeltaTime);
        if (view.Transition.IsMoving)
        {
            ui.RequestAnimationFrame();
        }
    }

    // So that the selected card stays where it is on the screen while the others move around it.
    private static void HoldSelectedCard(DiagramViewState view, PointF? before)
    {
        if (before is null)
        {
            return;
        }

        var after = FindSelectedPlace(view);
        if (after is null)
        {
            return;
        }

        var corner = view.Camera.Corner;
        view.Camera.LookFrom(new PointF(corner.X + after.Value.X - before.Value.X, corner.Y + after.Value.Y - before.Value.Y));
    }

    private static void BringIntoView(DiagramViewState view, TypeDiagram diagram, UiRect rect)
    {
        var layout = view.Transition.Layout;
        if (layout is null)
        {
            return;
        }

        view.FitFirstLayout(layout, rect);
        // Only a layout of the diagram as it is now places the card to focus.
        if (view.LaidOutVersion != diagram.Version)
        {
            return;
        }

        view.FocusPendingCard(layout, rect);
    }

    private static DiagramHit GetHit(Ui ui, DiagramViewState view, TypeDiagram diagram, Interaction i, bool hasRect, UiRect rect)
    {
        if (!hasRect)
        {
            return new DiagramHit.Nothing();
        }

        if (!i.Hovered && !i.Held)
        {
            return new DiagramHit.Nothing();
        }

        var point = view.Camera.ToDiagram(ui.Input.Pointer, rect);
        return HitAt(view, diagram, point);
    }

    private static DiagramHit HitAt(DiagramViewState view, TypeDiagram diagram, PointF point)
    {
        // From the last: the cards drawn last are on top.
        for (var i = diagram.Cards.Count - 1; i >= 0; i--)
        {
            var hit = HitCard(view, diagram.Cards[i], point);
            if (hit is not DiagramHit.Nothing)
            {
                return hit;
            }
        }
        return new DiagramHit.Nothing();
    }

    private static DiagramHit HitCard(DiagramViewState view, TypeCard card, PointF point)
    {
        var face = Diagrams.FindFace(view, card.Type);
        if (face is null)
        {
            return new DiagramHit.Nothing();
        }

        var place = view.Transition.FindCardPlace(card.Type);
        if (place is null)
        {
            return new DiagramHit.Nothing();
        }

        if (!Covers(place.Value, face, point))
        {
            return new DiagramHit.Nothing();
        }

        var row = Diagrams.FindRowAt(face, point.Y - place.Value.Y);
        if (row is null)
        {
            return new DiagramHit.Card(face);
        }

        return new DiagramHit.Row { Face = face, Index = row.Value };
    }

    private static CardFace? FaceOf(DiagramHit hit)
    {
        return hit.Match<CardFace?>(
            static _ => null,
            static card => card.Face,
            static row => row.Face);
    }

    private static CardRow? RowOf(DiagramHit hit)
    {
        if (hit is not DiagramHit.Row row)
        {
            return null;
        }

        return row.Value;
    }

    private static bool Covers(PointF place, CardFace face, PointF point)
    {
        var across = point.X >= place.X && point.X < place.X + face.Width;
        var down = point.Y >= place.Y && point.Y < place.Y + face.Height;
        return across && down;
    }

    private static void Scroll(Ui ui, DiagramViewState view, UiRect rect, bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        var input = ui.Input;
        if (!HasScrolled(input))
        {
            return;
        }

        if (IsCommandHeld(input.Modifiers))
        {
            view.Camera.ZoomAt(input.Pointer, rect, MathF.Pow(WheelZoomStep, input.ScrollY));
            return;
        }
        var step = ui.Theme.ScrollWheelStep;
        view.Camera.Pan(input.ScrollX * step, input.ScrollY * step);
    }

    private static bool HasScrolled(UiInput input)
    {
        if (input.ScrollY != 0f)
        {
            return true;
        }

        return input.ScrollX != 0f;
    }

    private static bool IsCommandHeld(KeyModifiers modifiers)
    {
        return (modifiers & (KeyModifiers.Control | KeyModifiers.Super)) != 0;
    }

    private static void Press(DiagramViewState view, Interaction i, DiagramHit hit)
    {
        if (!i.Pressed)
        {
            return;
        }

        view.PressOn(hit);
    }

    private static void OpenCode(IAppCommandWriter commands, Navigation navigation, TypeDiagram diagram, CardRow? row, bool inNewTab)
    {
        if (row is null)
        {
            return;
        }

        var target = row.Value.Target;
        if (target == null)
        {
            return;
        }

        Commands.FollowLater(commands, target, navigation.BeginResolvingFromDiagram(diagram, inNewTab));
    }

    private static void Pan(Ui ui, DiagramViewState view, Interaction i)
    {
        if (!ui.TryGetDragStart(i, view.Camera.Corner, out var start))
        {
            return;
        }

        var pointer = ui.Input.Pointer;
        var dx = pointer.X - ui.PressPosition.X;
        var dy = pointer.Y - ui.PressPosition.Y;
        view.TrackPress(dx, dy, PanThreshold);
        if (!view.PressMoved)
        {
            return;
        }

        var zoom = view.Camera.Zoom;
        view.Camera.LookFrom(new PointF(start.X - dx / zoom, start.Y - dy / zoom));
    }

    private static void SettleCamera(DiagramViewState view, Interaction i)
    {
        if (i.Held)
        {
            return;
        }

        if (view.Transition.IsMoving)
        {
            return;
        }

        view.Camera.FinishMoving();
    }

    private static bool IsPanning(DiagramViewState view, Interaction i)
    {
        if (!i.Held)
        {
            return false;
        }

        return view.PressMoved;
    }

    private static void Click(Ui ui, IAppCommandWriter commands, Navigation navigation, DiagramViewState view, TypeDiagram diagram, Interaction i, DiagramHit hit)
    {
        if (i.ClickedWith(PointerButton.Right))
        {
            OpenMenu(ui, view, hit);
            return;
        }
        if (i.ClickedWith(PointerButton.Middle))
        {
            OpenCode(commands, navigation, diagram, RowOf(hit), inNewTab: true);
            return;
        }
        if (!i.Clicked)
        {
            return;
        }

        if (view.PressMoved)
        {
            return;
        }

        Pick(view, view.PressedOn);
        OpenCode(commands, navigation, diagram, RowOf(view.PressedOn), IsCommandHeld(ui.Input.Modifiers));
    }

    private static void OpenMenu(Ui ui, DiagramViewState view, DiagramHit hit)
    {
        var face = FaceOf(hit);
        if (face is null)
        {
            return;
        }

        view.OpenMenuOn(face.Card);
        ui.OpenContextMenu(MenuId);
    }

    private static void Pick(DiagramViewState view, DiagramHit hit)
    {
        var face = FaceOf(hit);
        if (face is null)
        {
            view.Select(null);
            return;
        }
        view.Select(face.Card.Type);
        var row = RowOf(hit);
        if (row is null)
        {
            return;
        }

        if (row.Value.Content is not CardRowContent.Fold fold)
        {
            return;
        }

        Diagrams.ToggleFold(view, fold.Part);
    }

    private static void ShowHandOverClickable(Ui ui, DiagramViewState view, Interaction i, DiagramHit hit)
    {
        if (IsPanning(view, i))
        {
            return;
        }

        var row = RowOf(hit);
        if (row is null)
        {
            return;
        }

        if (!IsClickable(row.Value))
        {
            return;
        }

        ui.SetCursor(MouseCursor.Hand);
    }

    private static bool IsClickable(CardRow row)
    {
        if (row.IsLink)
        {
            return true;
        }

        return row.Content is CardRowContent.Fold;
    }

    private static void FillFrame(Ui ui, Appearance appearance, DiagramViewState view, TypeDiagram diagram, DiagramHit hit)
    {
        var frame = view.Frame;
        frame.Lay(view.Camera.Corner, view.Camera.Zoom, ui.Theme, appearance.NodeIconPalette, appearance.Icons);
        FillLinks(ui, view, diagram, frame);
        FillCards(view, diagram, hit, frame);
    }

    private static void FillLinks(Ui ui, DiagramViewState view, TypeDiagram diagram, DiagramFrame frame)
    {
        var layout = view.Transition.Layout;
        if (layout is null)
        {
            return;
        }

        var routes = layout.Routes;
        for (var routeIndex = 0; routeIndex < routes.Count; routeIndex++)
        {
            var route = routes[routeIndex];
            if (!ConnectsShownCards(diagram, route.Link))
            {
                continue;
            }

            frame.AddLink(DrawnLinkOf(ui, view, route));
        }
    }

    private static bool ConnectsShownCards(TypeDiagram diagram, TypeLink link)
    {
        if (!diagram.HasCard(link.From))
        {
            return false;
        }

        return diagram.HasCard(link.To);
    }

    private static DrawnLink DrawnLinkOf(Ui ui, DiagramViewState view, LinkRoute route)
    {
        var shape = view.Transition.ShapeRoute(route);
        var label = DiagramFaces.LabelOf(route.Link);
        if (label is null)
        {
            return new DrawnLink { Link = route.Link, Route = shape, Label = null, LabelWidth = 0f, LabelHeight = 0f };
        }

        var size = DiagramFaces.MeasureLabel(ui, label);
        return new DrawnLink
        {
            Link = route.Link,
            Route = shape,
            Label = label,
            LabelWidth = size.Width,
            LabelHeight = size.Height,
        };
    }

    private static void FillCards(DiagramViewState view, TypeDiagram diagram, DiagramHit hit, DiagramFrame frame)
    {
        for (var i = 0; i < diagram.Cards.Count; i++)
        {
            var card = diagram.Cards[i];
            var face = Diagrams.FindFace(view, card.Type);
            if (face is null)
            {
                continue;
            }

            var place = view.Transition.FindCardPlace(card.Type);
            if (place is null)
            {
                continue;
            }

            var isSelected = card.Type == view.Selected;
            frame.AddCard(new DrawnCard
            {
                Face = face,
                Place = place.Value,
                IsSelected = isSelected,
                HoveredRow = FindHoveredRow(face, hit),
            });
        }
    }

    private static int? FindHoveredRow(CardFace face, DiagramHit hit)
    {
        if (hit is not DiagramHit.Row row)
        {
            return null;
        }

        if (row.Face != face)
        {
            return null;
        }

        // A stereotype row does nothing, so it shows no hover.
        if (row.Value.Content is CardRowContent.Stereotype)
        {
            return null;
        }

        return row.Index;
    }

    private static void MemberTooltip(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, CodeTooltips tooltips, DiagramViewState view, TypeDiagram diagram, int canvasId,
        bool isPanning, DiagramHit hit)
    {
        var tooltip = FindTooltip(tooltips, isPanning, hit);
        var part = PartOf(tooltip, hit);
        using var popup = ui.TooltipContentAtPointer(canvasId, part, out var shownPart, CodeTooltipView.Look);
        view.Tooltip.Keep(tooltip, part, shownPart);
        if (!popup.IsOpen)
        {
            return;
        }

        var shown = view.Tooltip.Find(shownPart);
        if (shown == null)
        {
            return;
        }

        var click = CodeTooltipView.Render(ui, appearance, shown);
        FollowLink(ui, commands, navigation, diagram, click);
    }

    private static Lookup<CodeTooltip>? FindTooltip(CodeTooltips tooltips, bool isPanning, DiagramHit hit)
    {
        if (isPanning)
        {
            return null;
        }

        var row = RowOf(hit);
        if (row is null)
        {
            return null;
        }

        var target = row.Value.Target;
        if (target == null)
        {
            return null;
        }

        return tooltips.FindShown(target);
    }

    private static int PartOf(Lookup<CodeTooltip>? tooltip, DiagramHit hit)
    {
        if (tooltip == null)
        {
            return 0;
        }

        if (hit is not DiagramHit.Row row)
        {
            return 0;
        }

        return HashCode.Combine(row.Face.Card.Type, row.Index) | 1;
    }

    private static void FollowLink(Ui ui, IAppCommandWriter commands, Navigation navigation, TypeDiagram diagram, RichTextClick click)
    {
        if (click.Link is not CodeReference.Entity reference)
        {
            return;
        }

        ui.HideTooltip();
        var inNewTab = click.Button == PointerButton.Middle || IsCommandHeld(ui.Input.Modifiers);
        Commands.FollowLater(commands, reference, navigation.BeginResolvingFromDiagram(diagram, inNewTab));
    }

    /// <summary>Asks for the type <paramref name="type"/> points at to be shown on the diagram the user last worked
    /// in once the frame is drawn, on a new one titled <paramref name="newTitle"/> when none is open
    /// (<see cref="Diagrams.ShowInDiagram"/>), and for its card and those of its neighbours to be read then, on a
    /// token closing the diagram cancels.</summary>
    internal static void ShowInDiagramLater(IAppCommandWriter commands, CodeReference.Entity type, TabTitle newTitle)
    {
        Commands.StartSettlingLater(commands, nameof(DiagramGrowing.AddWithNeighboursAsync), (type, newTitle), static (a, s) => Diagrams.ShowInDiagram(a.ListSession.Navigation, s.type, s.newTitle),
            static (a, _, read, ct) => a.DiagramGrowing.AddWithNeighboursAsync(a.ViewCommands, read, ct), static read => read.Diagram.Token,
            static (_, read, error) => read.Diagram.FailReading(read.Read, error.Message));
    }

    /// <summary>As <see cref="ShowInDiagramLater"/>, on a new diagram titled <paramref name="title"/>
    /// (<see cref="Diagrams.ShowInNewDiagram"/>).</summary>
    internal static void ShowInNewDiagramLater(IAppCommandWriter commands, CodeReference.Entity type, TabTitle title)
    {
        Commands.StartSettlingLater(commands, nameof(DiagramGrowing.AddWithNeighboursAsync), (type, title), static (a, s) => Diagrams.ShowInNewDiagram(a.ListSession.Navigation, s.type, s.title),
            static (a, _, read, ct) => a.DiagramGrowing.AddWithNeighboursAsync(a.ViewCommands, read, ct), static read => read.Diagram.Token,
            static (_, read, error) => read.Diagram.FailReading(read.Read, error.Message));
    }

    // A read the card's menu started: settled failed on the diagram when its workflow fails.
    private static void StartExpanding(IAppCommandWriter commands, string name, TypeDiagram diagram, TypeCard card,
        Func<AppState, (TypeDiagram Diagram, TypeCard Card), ExpansionRead, CancellationToken, Task> run)
    {
        Commands.StartSettlingLater<(TypeDiagram Diagram, TypeCard Card), ExpansionRead>(commands, name, (diagram, card), static (_, s) => ExpansionRead.Start(s.Diagram, s.Card), run,
            static read => read.Diagram.Token, static (_, read, error) => read.Diagram.FailReading(read.Read, error.Message));
    }

    private static void CardMenu(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, DiagramViewState view, TypeDiagram diagram, string? closedAssembly)
    {
        using var menu = ui.ContextMenu(MenuId);
        if (!menu.IsOpen)
        {
            return;
        }

        var card = view.MenuCard;
        if (card == null)
        {
            return;
        }

        if (!diagram.HasCard(card.Type))
        {
            return;
        }

        CardMenuItems(ui, appearance, commands, navigation, view, diagram, card, closedAssembly);
    }

    private static void CardMenuItems(Ui ui, Appearance appearance, IAppCommandWriter commands, Navigation navigation, DiagramViewState view, TypeDiagram diagram, TypeCard card,
        string? closedAssembly)
    {
        var t = appearance.Strings;
        if (ui.MenuItem(t.TreeOpen, id: "open"))
        {
            Commands.FollowLater(commands, card.Type, navigation.BeginResolvingFromDiagram(diagram, inNewTab: false));
        }

        if (ui.MenuItem(t.TreeOpenInNewTab, id: "open-in-new-tab"))
        {
            Commands.FollowLater(commands, card.Type, navigation.BeginResolvingFromDiagram(diagram, inNewTab: true));
        }

        ui.MenuSeparator();
        GrowItems(ui, appearance, commands, diagram, card, DocumentsView.FindClosedText(ui, appearance, closedAssembly));
        ui.MenuSeparator();
        if (ui.MenuItem(t.DiagramRemove, id: "remove"))
        {
            Commands.Later(commands, (view, diagram, card), static (_, s) => Remove(s.view, s.diagram, s.card));
        }
    }

    private static void GrowItems(Ui ui, Appearance appearance, IAppCommandWriter commands, TypeDiagram diagram, TypeCard card, string? why)
    {
        var t = appearance.Strings;
        var open = why is null;
        if (HasLinks(card, Diagrams.IsBaseLink) && ui.MenuItem(t.DiagramAddBases, enabled: open && Diagrams.HasMissingBases(diagram, card), id: "add-bases", detail: why))
        {
            StartExpanding(commands, nameof(DiagramGrowing.ExpandBasesAsync), diagram, card, static (a, _, read, ct) => a.DiagramGrowing.ExpandBasesAsync(read, ct));
        }

        if (HasLinks(card, Diagrams.IsInterfaceLink)
            && ui.MenuItem(t.DiagramAddInterfaces, enabled: open && Diagrams.HasMissingInterfaces(diagram, card), id: "add-interfaces", detail: why))
        {
            StartExpanding(commands, nameof(DiagramGrowing.ExpandInterfacesAsync), diagram, card,
                static (a, _, read, ct) => a.DiagramGrowing.ExpandInterfacesAsync(read, ct));
        }

        if (card.CanHaveDerived)
        {
            var derivedItem = DerivedItemOf(ui, appearance, card);
            if (ui.MenuItem(derivedItem, enabled: open, id: "add-derived", detail: why))
            {
                StartExpanding(commands, nameof(DiagramGrowing.ExpandDerivedAsync), diagram, card,
                    static (a, _, read, ct) => a.DiagramGrowing.ExpandDerivedAsync(read, ct));
            }
        }

        if (HasLinks(card, Diagrams.IsHeldLink) && ui.MenuItem(t.DiagramAddHeld, enabled: open && Diagrams.HasMissingHeld(diagram, card), id: "add-held", detail: why))
        {
            StartExpanding(commands, nameof(DiagramGrowing.ExpandHeldAsync), diagram, card, static (a, _, read, ct) => a.DiagramGrowing.ExpandHeldAsync(read, ct));
        }
    }

    private static bool HasLinks(TypeCard card, Func<TypeLink, bool> isKind)
    {
        return card.Links.Any(isKind);
    }

    private static string DerivedItemOf(Ui ui, Appearance appearance, TypeCard card)
    {
        var t = appearance.Strings;
        if (card.Stereotype == TypeStereotype.Interface)
        {
            return t.DiagramAddImplementations;
        }

        return t.DiagramAddDerived;
    }

    private static void Remove(DiagramViewState view, TypeDiagram diagram, TypeCard card)
    {
        diagram.RemoveCard(card.Type);
        view.ForgetRemoved(card.Type);
    }

    private static void Paint(UiPainter p, DiagramFrame frame)
    {
        var links = frame.Links;
        for (var i = 0; i < links.Count; i++)
        {
            PaintLink(p, frame, links[i]);
        }

        var cards = frame.Cards;
        for (var i = 0; i < cards.Count; i++)
        {
            PaintCard(p, frame, cards[i]);
        }
    }

    private static PointF ToScreen(UiPainter p, DiagramFrame frame, PointF point)
    {
        return new PointF(p.Rect.X + (point.X - frame.Corner.X) * frame.Zoom, p.Rect.Y + (point.Y - frame.Corner.Y) * frame.Zoom);
    }

    private static void PaintLink(UiPainter p, DiagramFrame frame, DrawnLink link)
    {
        var isHeld = link.Link.IsHeld;
        var color = isHeld ? frame.Palette.Data : frame.Palette.Type;
        var width = isHeld ? HasALineWidth : IsALineWidth;
        var stroke = MathF.Max(1f, width * frame.Zoom);
        var isDashed = link.Link is TypeLink.Implements;
        var dash = isDashed ? DashLength * frame.Zoom : 0f;
        var gap = isDashed ? GapLength * frame.Zoom : 0f;
        var pieces = link.Route.Pieces;
        for (var pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
        {
            var piece = pieces[pieceIndex];
            PaintPiece(p, frame, piece, color, stroke, dash, gap);
        }

        PaintArrowhead(p, frame, link, color, stroke);
        PaintDiamond(p, frame, link.Route, color, stroke);
        PaintLabel(p, frame, link, color);
    }

    private static void PaintPiece(UiPainter p, DiagramFrame frame, RoutePiece piece, uint color, float stroke, float dash, float gap)
    {
        var start = ToScreen(p, frame, piece.Start);
        var control1 = ToScreen(p, frame, piece.Control1);
        var control2 = ToScreen(p, frame, piece.Control2);
        var end = ToScreen(p, frame, piece.End);
        p.CubicBezier(start, control1, control2, end, stroke, color, dash, gap);
    }

    private static void PaintArrowhead(UiPainter p, DiagramFrame frame, DrawnLink link, uint color, float stroke)
    {
        var tip = ToScreen(p, frame, link.Route.Tip);
        var tail = ToScreen(p, frame, GetArrowTail(link.Route));
        var length = MathF.Sqrt(tip.DistanceSqTo(tail));
        if (link.Link.IsHeld)
        {
            p.OpenArrowhead(tip, tail, length, OpenArrowWidth * frame.Zoom, stroke, color);
            return;
        }
        p.HollowArrowhead(tip, tail, length, HollowArrowWidth * frame.Zoom, stroke, color);
    }

    private static void PaintDiamond(UiPainter p, DiagramFrame frame, LinkRoute route, uint color, float stroke)
    {
        // No source tip: the route leaves no room for a diamond.
        if (route.SourceTip is null)
        {
            return;
        }

        if (route.Pieces.Count == 0)
        {
            return;
        }

        var tip = ToScreen(p, frame, route.SourceTip.Value);
        var back = ToScreen(p, frame, route.Pieces[0].Start);
        var length = MathF.Sqrt(tip.DistanceSqTo(back));
        p.HollowDiamond(tip, back, length, DiamondWidth * frame.Zoom, stroke, color);
    }

    private static PointF GetArrowTail(LinkRoute route)
    {
        if (route.Pieces.Count == 0)
        {
            return route.Tip;
        }

        return route.Pieces[^1].End;
    }

    private static void PaintLabel(UiPainter p, DiagramFrame frame, DrawnLink link, uint color)
    {
        if (link.Label is null)
        {
            return;
        }

        var center = link.Route.LabelCenter;
        if (center is null)
        {
            return;
        }

        var size = frame.Theme.FontSizeSmall * frame.Zoom;
        if (size < MinTextSize)
        {
            return;
        }

        var middle = ToScreen(p, frame, center.Value);
        var width = link.LabelWidth * frame.Zoom;
        var height = link.LabelHeight * frame.Zoom;
        var rect = new UiRect(middle.X - width / 2, middle.Y - height / 2, width, height);
        p.FillRect(rect, frame.Theme.SurfaceRaised, frame.Theme.RadiusSmall * frame.Zoom, color, MathF.Max(1f, LabelBorderWidth * frame.Zoom));
        p.Text(rect, link.Label, frame.Theme.Text, size, align: Align.Center);
    }

    private static void PaintCard(UiPainter p, DiagramFrame frame, DrawnCard card)
    {
        var face = card.Face;
        var topLeft = ToScreen(p, frame, card.Place);
        var rect = new UiRect(topLeft.X, topLeft.Y, face.Width * frame.Zoom, face.Height * frame.Zoom);
        if (!rect.Intersects(p.Clip))
        {
            return;
        }

        var radius = frame.Theme.CornerRadius * frame.Zoom;
        p.FillRect(rect, frame.Theme.SurfaceRaised, radius);
        p.FillRect(rect with { Height = face.HeaderHeight * frame.Zoom }, frame.Theme.Control, new Corners(radius, radius, 0f, 0f));
        for (var i = 0; i < face.Rows.Count; i++)
        {
            PaintRow(p, frame, rect, face.Rows[i], i == card.HoveredRow);
        }

        PaintBorder(p, frame, rect, card.IsSelected, radius);
    }

    private static void PaintBorder(UiPainter p, DiagramFrame frame, UiRect rect, bool isSelected, float radius)
    {
        if (isSelected)
        {
            p.FillRect(rect, 0, radius, frame.Theme.Accent, SelectedBorderWidth);
            return;
        }
        p.FillRect(rect, 0, radius, frame.Theme.BorderStrong, frame.Theme.BorderWidth);
    }

    private static void PaintRow(UiPainter p, DiagramFrame frame, UiRect card, CardRow row, bool isHovered)
    {
        var rect = new UiRect(card.X, card.Y + row.Top * frame.Zoom, card.Width, row.Height * frame.Zoom);
        if (!rect.Intersects(p.Clip))
        {
            return;
        }

        if (isHovered)
        {
            p.FillRect(rect, frame.Theme.RowHover);
        }

        var left = card.X + row.Indent * frame.Zoom;
        var underlined = isHovered && row.IsLink;
        row.Content.Switch((p, frame, rect, left, row, underlined),
            name: static (s, name) => PaintIconRow(s.p, s.frame, s.rect, s.left, s.row, name.Icon, name.Accessibility, bold: true, s.underlined),
            stereotype: static (s, _) => PaintText(s.p, s.frame, s.rect, s.left, s.row.Text, s.frame.Theme.TextMuted, family: TextFonts.Italic),
            fold: static (s, fold) => PaintFold(s.p, s.frame, s.rect, s.left, s.row, fold.IsOpen),
            member: static (s, member) => PaintIconRow(s.p, s.frame, s.rect, s.left, s.row, member.Icon, member.Accessibility, bold: false, s.underlined));
    }

    private static void PaintIconRow(UiPainter p, DiagramFrame frame, UiRect rect, float left, CardRow row, NodeIcon icon,
        NodeAccessibility accessibility, bool bold, bool underlined)
    {
        PaintIcon(p, frame, rect, left, icon, accessibility);
        PaintTextAfterIcon(p, frame, rect, left, row, frame.Theme.Text, bold, underlined);
    }

    private static void PaintTextAfterIcon(UiPainter p, DiagramFrame frame, UiRect rect, float left, CardRow row, uint color, bool bold, bool underlined)
    {
        var textLeft = left + (frame.Theme.IconSize + DiagramFaces.IconGap) * frame.Zoom;
        PaintText(p, frame, rect, textLeft, row.Text, color, bold: bold);
        if (underlined)
        {
            PaintUnderline(p, frame, rect, textLeft, row.TextWidth * frame.Zoom, color);
        }
    }

    private static void PaintUnderline(UiPainter p, DiagramFrame frame, UiRect rect, float left, float width, uint color)
    {
        var y = MathF.Round(rect.Y + rect.Height / 2 + frame.Theme.FontSize * frame.Zoom * UnderlineDrop);
        var thickness = MathF.Max(1f, frame.Zoom);
        p.Line(new PointF(left, y), new PointF(left + width, y), thickness, color);
    }

    private static void PaintIcon(UiPainter p, DiagramFrame frame, UiRect row, float left, NodeIcon kind, NodeAccessibility accessibility)
    {
        var size = MathF.Round(frame.Theme.IconSize * frame.Zoom);
        if (size < MinIconSize)
        {
            return;
        }

        var box = new UiRect(left, row.Y + (row.Height - size) / 2, size, size);
        p.Glyph(box, NodeIcons.Glyph(kind, frame.Icons), NodeIcons.GetColor(frame.Palette, kind), size);
        var overlay = NodeIcons.Overlay(accessibility, frame.Icons);
        if (overlay is null)
        {
            return;
        }

        var overlaySize = MathF.Round(frame.Theme.IconOverlaySize * frame.Zoom);
        var corner = new UiRect(box.Right - overlaySize * OverlayInset, box.Bottom - overlaySize * OverlayInset, overlaySize, overlaySize);
        p.Glyph(corner, overlay.Value, frame.Palette.Restricted, overlaySize);
    }

    private static void PaintFold(UiPainter p, DiagramFrame frame, UiRect rect, float left, CardRow row, bool isOpen)
    {
        var chevronWidth = DiagramFaces.ChevronWidth * frame.Zoom;
        var center = new PointF(left + chevronWidth / 2, rect.Y + rect.Height / 2);
        p.DrawDisclosure(center, ChevronHalf * frame.Zoom, isOpen, frame.Theme.TextMuted, frame.Theme.IconStrokeWidth * frame.Zoom);
        PaintText(p, frame, rect, left + chevronWidth, row.Text, frame.Theme.TextMuted);
    }

    private static void PaintText(UiPainter p, DiagramFrame frame, UiRect rect, float left, string text, uint color, bool bold = false, string? family = null)
    {
        var size = frame.Theme.FontSize * frame.Zoom;
        if (size < MinTextSize)
        {
            return;
        }

        var width = rect.Right - TextRightPadding * frame.Zoom - left;
        p.Text(new UiRect(left, rect.Y, width, rect.Height), text, color, size, bold, family: family);
    }
}
