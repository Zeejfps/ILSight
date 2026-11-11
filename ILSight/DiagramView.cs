using System.Diagnostics;
using ZGF.Desktop;
using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ZGF.Gui.Immediate.Widgets;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ZGF.KeyboardModule;
using static ILSight.Diagrams;
using static ILSight.Navigation;

namespace ILSight;

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

    /// <summary>How far below the middle of its row a link's underline runs, in font sizes.</summary>
    private const float UnderlineDrop = 0.5f;

    private const string CanvasId = "diagram";
    private const string MenuId = "diagram-menu";
    private const float PanThreshold = 4f;
    private const float WheelZoomStep = 1.1f;

    public static void Render(Ui<UiState> ui, CodeTooltips tooltips, DocumentTab tab, TypeDiagram diagram)
    {
        var view = DocumentPanels.GetView(ui.State.Documents, tab).Diagram;
        using var pane = ui.Column(new Box { Height = Sizing.Grow(), Gap = 0f, Background = ui.Theme.PaneBackground });
        if (!ui.State.Display.ShowOmnibar)
        {
            OwnToolbar(ui, tab);
        }

        if (diagram.Cards.Count == 0)
        {
            EmptyHint(ui, diagram);
            return;
        }
        DiagramFaces.MeasureIfChanged(ui, view, diagram);
        Canvas(ui, tooltips, view, diagram);
        CardMenu(ui, view, diagram);
    }

    /// <summary>The tools in a bar of the tab's own, while the bar over documents, where they sit otherwise, is hidden.</summary>
    private static void OwnToolbar(Ui<UiState> ui, DocumentTab tab)
    {
        using (ui.PaneToolbar())
        {
            Tools(ui, tab);
        }

        ui.Separator();
    }

    /// <summary>The diagram's tools, for the bar over the document: zoom, fit and the member filter.</summary>
    public static void Tools(Ui<UiState> ui, DocumentTab tab)
    {
        var t = ui.State.Strings;
        var icons = ui.State.Icons;
        var view = DocumentPanels.GetView(ui.State.Documents, tab).Diagram;
        var camera = view.Camera;
        var area = FindCanvasRect(ui);
        using (ui.Row(new Box { Gap = ui.Theme.Gap, AlignY = Align.Center }))
        {
            if (ui.IconButton("zoom-out", icons.ResourceZoomOut, t.ResourceZoomOut, camera.CanZoomOut))
            {
                DiagramCameras.ZoomOut(camera, area);
            }

            ui.Text(t.ResourceZoomPercent(camera.Percent), color: ui.Theme.TextMuted);
            if (ui.IconButton("zoom-in", icons.ResourceZoomIn, t.ResourceZoomIn, camera.CanZoomIn))
            {
                DiagramCameras.ZoomIn(camera, area);
            }

            if (ui.Button(t.DiagramFit, id: "fit"))
            {
                FitToView(view, area);
            }

            ui.Separator(ui.Theme.IconSize);
            ShowHiddenMembersCheckbox(ui, view);
        }
    }

    private static void ShowHiddenMembersCheckbox(Ui<UiState> ui, DiagramViewState view)
    {
        var shows = view.ShowsHiddenMembers;
        if (ui.Checkbox(ui.State.Strings.DiagramShowHiddenMembers, ref shows, id: "show-hidden-members"))
        {
            Diagrams.ShowHiddenMembers(view, shows);
        }
    }

    /// <summary>Where the cards were drawn on the last frame.</summary>
    private static UiRect FindCanvasRect(Ui<UiState> ui)
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

        DiagramCameras.Fit(view.Camera, layout.Width, layout.Height, area);
    }

    private static void EmptyHint(Ui<UiState> ui, TypeDiagram diagram)
    {
        var t = ui.State.Strings;
        var text = diagram.IsBusy ? t.DiagramReading : t.DiagramEmpty;
        DocumentsView.Hint(ui, text);
    }

    private static void Canvas(Ui<UiState> ui, CodeTooltips tooltips, DiagramViewState view, TypeDiagram diagram)
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

        var hit = FindHit(ui, view, diagram, i, hasRect, rect);
        Scroll(ui, view, rect, i.Hovered);
        Press(view, i, hit);
        Pan(ui, view, i);
        SettleCamera(view, i);
        Click(ui, view, diagram, i, hit);
        ShowHandOverClickable(ui, view, i, hit);
        FillFrame(ui, view, diagram, hit);
        using (ui.Box(id, new Box { Width = Sizing.Grow(), Height = Sizing.Grow(), Background = ui.Theme.EditorBackground, Clip = true }))
        {
            ui.Paint(view.Frame, Paint);
        }

        if (hasRect)
        {
            MemberTooltip(ui, tooltips, view, diagram, id, IsPanning(view, i), hit);
        }
    }

    /// <summary>Where the selected card is drawn now, before the layout moves on; null when none is.</summary>
    private static PointF? FindSelectedPlace(DiagramViewState view)
    {
        var selected = view.Selected;
        if (selected == null)
        {
            return null;
        }

        return Diagrams.GetCardPlace(view.Transition, selected);
    }

    /// <summary>Moves toward the diagram's newest layout, drawing again until the move ends; a new layout
    /// is of the diagram as it was last measured.</summary>
    private static void FollowLayout(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram)
    {
        if (Diagrams.FollowLayout(view.Transition, diagram.Layout))
        {
            view.LaidOutVersion = view.MeasuredVersion;
        }

        Diagrams.AdvanceTransition(view.Transition, ui.DeltaTime);
        if (view.Transition.IsMoving)
        {
            ui.RequestAnimationFrame();
        }
    }

    /// <summary>Moves the view along with the selected card, which was drawn at <paramref name="before"/>, so
    /// that it stays where it is on the screen while the others move around it.</summary>
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
        DiagramCameras.LookFrom(view.Camera, new PointF(corner.X + after.Value.X - before.Value.X, corner.Y + after.Value.Y - before.Value.Y));
    }

    /// <summary>Fits the first layout that places cards to <paramref name="rect"/>, then brings the card
    /// focused into it once a layout of the diagram as it is now places it.</summary>
    private static void BringIntoView(DiagramViewState view, TypeDiagram diagram, UiRect rect)
    {
        var layout = view.Transition.Layout;
        if (layout is null)
        {
            return;
        }

        FitFirstLayout(view, layout, rect);
        if (view.LaidOutVersion != diagram.Version)
        {
            return;
        }

        FocusPendingCard(view, layout, rect);
    }

    private static void FitFirstLayout(DiagramViewState view, TypeLayout layout, UiRect rect)
    {
        if (!view.IsFitPending)
        {
            return;
        }

        if (layout.Places.Count == 0)
        {
            return;
        }

        DiagramCameras.Fit(view.Camera, layout.Width, layout.Height, rect);
        view.IsFitPending = false;
    }

    private static void FocusPendingCard(DiagramViewState view, TypeLayout layout, UiRect rect)
    {
        var type = view.PendingFocus;
        if (type == null)
        {
            return;
        }

        view.PendingFocus = null;
        if (!layout.Places.TryGetValue(type, out var place))
        {
            return;
        }

        var face = FindFace(view, type);
        if (face is null)
        {
            return;
        }

        if (DiagramCameras.Shows(view.Camera, place, face.Width, face.Height, rect))
        {
            return;
        }

        DiagramCameras.CenterOn(view.Camera, new PointF(place.X + face.Width / 2, place.Y + face.Height / 2), rect);
    }

    /// <summary>The card and row under the pointer, while it is over the cards or holds a press on them.</summary>
    private static DiagramHit FindHit(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, Interaction i, bool hasRect, UiRect rect)
    {
        if (!hasRect)
        {
            return new DiagramHit.Nothing();
        }

        if (!i.Hovered && !i.Held)
        {
            return new DiagramHit.Nothing();
        }

        var point = DiagramCameras.ToDiagram(view.Camera, ui.Input.Pointer, rect);
        return HitAt(view, diagram, point);
    }

    /// <summary>The topmost card drawn at <paramref name="point"/> of the diagram, and its row there.</summary>
    private static DiagramHit HitAt(DiagramViewState view, TypeDiagram diagram, PointF point)
    {
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
        var face = FindFace(view, card.Type);
        if (face is null)
        {
            return new DiagramHit.Nothing();
        }

        var place = Diagrams.GetCardPlace(view.Transition, card.Type);
        if (place is null)
        {
            return new DiagramHit.Nothing();
        }

        if (!Covers(place.Value, face, point))
        {
            return new DiagramHit.Nothing();
        }

        var row = FindRowAt(face, point.Y - place.Value.Y);
        if (row < 0)
        {
            return new DiagramHit.Card(face);
        }

        return new DiagramHit.Row(face, row);
    }

    private static CardFace? FaceOf(DiagramHit hit)
    {
        return hit switch
        {
            DiagramHit.Nothing => null,
            DiagramHit.Card card => card.Face,
            DiagramHit.Row row => row.Face,
            _ => throw new UnreachableException(),
        };
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

    /// <summary>The wheel scrolls the diagram while the pointer is over it, and zooms it about the pointer
    /// with Ctrl.</summary>
    private static void Scroll(Ui<UiState> ui, DiagramViewState view, UiRect rect, bool hovered)
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
            DiagramCameras.ZoomAt(view.Camera, input.Pointer, rect, MathF.Pow(WheelZoomStep, input.ScrollY));
            return;
        }
        var step = ui.Theme.ScrollWheelStep;
        DiagramCameras.Pan(view.Camera, input.ScrollX * step, input.ScrollY * step);
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

    /// <summary>Keeps what a press landed on.</summary>
    private static void Press(DiagramViewState view, Interaction i, DiagramHit hit)
    {
        if (!i.Pressed)
        {
            return;
        }

        view.PressedOn = hit;
        view.PressMoved = false;
    }

    private static void OpenCode(UiState screen, TypeDiagram diagram, CardRow? row, bool inNewTab)
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

        Commands.Later(screen, (diagram, target, inNewTab), static (a, s) => OpenFromDiagram(a, s.diagram, s.target, s.inNewTab));
    }

    /// <summary>A press dragged further than a click pans the diagram along with the pointer.</summary>
    private static void Pan(Ui<UiState> ui, DiagramViewState view, Interaction i)
    {
        if (!ui.TryGetDragStart(i, view.Camera.Corner, out var start))
        {
            return;
        }

        var pointer = ui.Input.Pointer;
        var dx = pointer.X - ui.PressPosition.X;
        var dy = pointer.Y - ui.PressPosition.Y;
        if (!view.PressMoved)
        {
            view.PressMoved = dx * dx + dy * dy >= PanThreshold * PanThreshold;
        }

        if (!view.PressMoved)
        {
            return;
        }

        var zoom = view.Camera.Zoom;
        DiagramCameras.LookFrom(view.Camera, new PointF(start.X - dx / zoom, start.Y - dy / zoom));
    }

    /// <summary>Counts the camera's move as done once no press drags it and the cards stopped moving it along.</summary>
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

        DiagramCameras.FinishMoving(view.Camera);
    }

    private static bool IsPanning(DiagramViewState view, Interaction i)
    {
        if (!i.Held)
        {
            return false;
        }

        return view.PressMoved;
    }

    /// <summary>A right click opens the menu of the card under the pointer, a middle click opens its code in
    /// a new tab, and a left click that did not pan picks what it was pressed on and opens the code of the
    /// type or member there, in a new tab with Ctrl held.</summary>
    private static void Click(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, Interaction i, DiagramHit hit)
    {
        if (i.ClickedWith(PointerButton.Right))
        {
            OpenMenu(ui, view, hit);
            return;
        }
        if (i.ClickedWith(PointerButton.Middle))
        {
            OpenCode(ui.State, diagram, RowOf(hit), inNewTab: true);
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
        OpenCode(ui.State, diagram, RowOf(view.PressedOn), IsCommandHeld(ui.Input.Modifiers));
    }

    private static void OpenMenu(Ui<UiState> ui, DiagramViewState view, DiagramHit hit)
    {
        var face = FaceOf(hit);
        if (face is null)
        {
            return;
        }

        view.Selected = face.Card.Type;
        view.MenuCard = face.Card;
        ui.OpenContextMenu(MenuId);
    }

    /// <summary>Selects the card <paramref name="hit"/> is on, or none off the cards, and folds or unfolds the
    /// part whose row it is on.</summary>
    private static void Pick(DiagramViewState view, DiagramHit hit)
    {
        var face = FaceOf(hit);
        if (face is null)
        {
            view.Selected = null;
            return;
        }
        view.Selected = face.Card.Type;
        var row = RowOf(hit);
        if (row is null)
        {
            return;
        }

        if (row.Value.Content is not CardRowContent.Fold fold)
        {
            return;
        }

        ToggleFold(view, fold.Part);
    }

    /// <summary>A hand over a row a click acts on: one that opens code or folds a part; none while panning.</summary>
    private static void ShowHandOverClickable(Ui<UiState> ui, DiagramViewState view, Interaction i, DiagramHit hit)
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

    private static void FillFrame(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, DiagramHit hit)
    {
        var frame = view.Frame;
        frame.Corner = view.Camera.Corner;
        frame.Zoom = view.Camera.Zoom;
        frame.Theme = ui.Theme;
        frame.Palette = ui.State.NodeIconPalette;
        frame.Icons = ui.State.Icons;
        FillLinks(ui, view, diagram, frame);
        FillCards(view, diagram, hit, frame);
    }

    /// <summary>The links of the layout drawn whose cards the diagram still shows, each as it is drawn now.</summary>
    private static void FillLinks(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, DiagramFrame frame)
    {
        frame.Links.Clear();
        var layout = view.Transition.Layout;
        if (layout is null)
        {
            return;
        }

        foreach (var route in layout.Routes)
        {
            if (!ConnectsShownCards(diagram, route.Link))
            {
                continue;
            }

            frame.Links.Add(DrawnLinkOf(ui, view, route));
        }
    }

    private static bool ConnectsShownCards(TypeDiagram diagram, TypeLink link)
    {
        if (!Diagrams.HasCard(diagram, link.From))
        {
            return false;
        }

        return Diagrams.HasCard(diagram, link.To);
    }

    private static DrawnLink DrawnLinkOf(Ui<UiState> ui, DiagramViewState view, LinkRoute route)
    {
        var shape = Diagrams.ShapeRoute(view.Transition, route);
        var label = DiagramFaces.LabelOf(route.Link);
        if (label is null)
        {
            return new DrawnLink(route.Link.Kind, shape, null, 0f, 0f);
        }

        var size = DiagramFaces.MeasureLabel(ui, label);
        return new DrawnLink(route.Link.Kind, shape, label, size.Width, size.Height);
    }

    /// <summary>The cards the layout drawn places, each where it is drawn now.</summary>
    private static void FillCards(DiagramViewState view, TypeDiagram diagram, DiagramHit hit, DiagramFrame frame)
    {
        frame.Cards.Clear();
        foreach (var card in diagram.Cards)
        {
            var face = FindFace(view, card.Type);
            if (face is null)
            {
                continue;
            }

            var place = Diagrams.GetCardPlace(view.Transition, card.Type);
            if (place is null)
            {
                continue;
            }

            var isSelected = card.Type == view.Selected;
            frame.Cards.Add(new DrawnCard(face, place.Value, isSelected, HoveredRowOf(face, hit)));
        }
    }

    /// <summary>The row of <paramref name="face"/> the pointer is on when it does something, else -1.</summary>
    private static int HoveredRowOf(CardFace face, DiagramHit hit)
    {
        if (hit is not DiagramHit.Row row)
        {
            return -1;
        }

        if (row.Face != face)
        {
            return -1;
        }

        if (row.Value.Content is CardRowContent.Stereotype)
        {
            return -1;
        }

        return row.Index;
    }

    /// <summary>The declaration and documentation of the type or member under the pointer, once it has
    /// rested there, under the pointer; none while the diagram pans.</summary>
    private static void MemberTooltip(Ui<UiState> ui, CodeTooltips tooltips, DiagramViewState view, TypeDiagram diagram, int canvasId,
        bool isPanning, DiagramHit hit)
    {
        var tooltip = FindTooltip(tooltips, isPanning, hit);
        var part = PartOf(tooltip, hit);
        using var popup = ui.TooltipContentAtPointer(canvasId, part, out var shownPart, CodeTooltipView.Look);
        KeptTooltips.Keep(view.Tooltip, tooltip, part, shownPart);
        if (!popup.IsOpen)
        {
            return;
        }

        var shown = KeptTooltips.Find(view.Tooltip, shownPart);
        if (shown == null)
        {
            return;
        }

        var click = CodeTooltipView.Render(ui, shown);
        FollowLink(ui, diagram, click);
    }

    private static CodeTooltip? FindTooltip(CodeTooltips tooltips, bool isPanning, DiagramHit hit)
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

        TooltipLookups.TryGetTooltip(tooltips, target, out var tooltip);
        return tooltip;
    }

    private static int PartOf(CodeTooltip? tooltip, DiagramHit hit)
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

    /// <summary>A link clicked in the tooltip opens its code, in a new tab with a middle click or Ctrl, and
    /// closes the tooltip.</summary>
    private static void FollowLink(Ui<UiState> ui, TypeDiagram diagram, RichTextClick click)
    {
        if (click.Link is not CodeReference.Entity reference)
        {
            return;
        }

        ui.HideTooltip();
        var inNewTab = click.Button == PointerButton.Middle || IsCommandHeld(ui.Input.Modifiers);
        Commands.Later(ui.State, (diagram, reference, inNewTab), static (a, s) => OpenFromDiagram(a, s.diagram, s.reference, s.inNewTab));
    }

    private static void CardMenu(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram)
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

        if (!Diagrams.HasCard(diagram, card.Type))
        {
            return;
        }

        CardMenuItems(ui, view, diagram, card);
    }

    private static void CardMenuItems(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, TypeCard card)
    {
        var t = ui.State.Strings;
        if (ui.MenuItem(t.TreeOpen, id: "open"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => OpenFromDiagram(a, s.diagram, s.card.Type, inNewTab: false));
        }

        if (ui.MenuItem(t.TreeOpenInNewTab, id: "open-in-new-tab"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => OpenFromDiagram(a, s.diagram, s.card.Type, inNewTab: true));
        }

        ui.MenuSeparator();
        GrowItems(ui, diagram, card);
        ui.MenuSeparator();
        if (ui.MenuItem(t.DiagramRemove, id: "remove"))
        {
            Commands.Later(ui.State, (view, diagram, card), static (a, s) => Remove(a, s.view, s.diagram, s.card));
        }
    }

    /// <summary>The items that add the types <paramref name="card"/> is related to, each offered when it has
    /// such relations, and enabled while the diagram lacks some of them.</summary>
    private static void GrowItems(Ui<UiState> ui, TypeDiagram diagram, TypeCard card)
    {
        var t = ui.State.Strings;
        if (HasLinks(card, IsBaseLink) && ui.MenuItem(t.DiagramAddBases, enabled: HasMissingBases(diagram, card), id: "add-bases"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => ExpandBases(a, s.diagram, s.card));
        }

        if (HasLinks(card, IsInterfaceLink) && ui.MenuItem(t.DiagramAddInterfaces, enabled: HasMissingInterfaces(diagram, card), id: "add-interfaces"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => ExpandInterfaces(a, s.diagram, s.card));
        }

        if (card.CanHaveDerived && ui.MenuItem(DerivedItemOf(ui, card), id: "add-derived"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => ExpandDerived(a, s.diagram, s.card));
        }

        if (HasLinks(card, IsHeldLink) && ui.MenuItem(t.DiagramAddHeld, enabled: HasMissingHeld(diagram, card), id: "add-held"))
        {
            Commands.Later(ui.State, (diagram, card), static (a, s) => ExpandHeld(a, s.diagram, s.card));
        }
    }

    private static bool HasLinks(TypeCard card, Func<TypeLink, bool> isKind)
    {
        return card.Links.Any(isKind);
    }

    /// <summary>Implementations for an interface, derived types for a class.</summary>
    private static string DerivedItemOf(Ui<UiState> ui, TypeCard card)
    {
        var t = ui.State.Strings;
        if (card.Stereotype == TypeStereotype.Interface)
        {
            return t.DiagramAddImplementations;
        }

        return t.DiagramAddDerived;
    }

    private static void Remove(AppState appState, DiagramViewState view, TypeDiagram diagram, TypeCard card)
    {
        RemoveFromDiagram(appState, diagram, card.Type);
        view.MenuCard = null;
        if (view.Selected == card.Type)
        {
            view.Selected = null;
        }
    }

    private static void Paint(UiPainter p, DiagramFrame frame)
    {
        foreach (var link in frame.Links)
        {
            PaintLink(p, frame, link);
        }

        foreach (var card in frame.Cards)
        {
            PaintCard(p, frame, card);
        }
    }

    private static PointF ToScreen(UiPainter p, DiagramFrame frame, PointF point)
    {
        return new PointF(p.Rect.X + (point.X - frame.Corner.X) * frame.Zoom, p.Rect.Y + (point.Y - frame.Corner.Y) * frame.Zoom);
    }

    /// <summary>Links that say what a type is, derived from or implemented, in the color of types; links
    /// that say what it has, the types its properties hold, in the color of data.</summary>
    private static void PaintLink(UiPainter p, DiagramFrame frame, DrawnLink link)
    {
        var isHeld = IsHeld(link.Kind);
        var color = isHeld ? frame.Palette.Data : frame.Palette.Type;
        var width = isHeld ? HasALineWidth : IsALineWidth;
        var stroke = MathF.Max(1f, width * frame.Zoom);
        var isDashed = link.Kind == TypeLinkKind.Implements;
        var dash = isDashed ? DashLength * frame.Zoom : 0f;
        var gap = isDashed ? GapLength * frame.Zoom : 0f;
        foreach (var piece in link.Route.Pieces)
        {
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

    /// <summary>A hollow triangle at the end of a link to a type derived from or implemented, an open
    /// arrowhead at the end of one to a type held.</summary>
    private static void PaintArrowhead(UiPainter p, DiagramFrame frame, DrawnLink link, uint color, float stroke)
    {
        var tip = ToScreen(p, frame, link.Route.Tip);
        var tail = ToScreen(p, frame, GetArrowTail(link.Route));
        var length = MathF.Sqrt(tip.DistanceSqTo(tail));
        if (IsHeld(link.Kind))
        {
            p.OpenArrowhead(tip, tail, length, OpenArrowWidth * frame.Zoom, stroke, color);
            return;
        }
        p.HollowArrowhead(tip, tail, length, HollowArrowWidth * frame.Zoom, stroke, color);
    }

    /// <summary>A hollow diamond on the card of a type holding the other, where the route leaves room for one.</summary>
    private static void PaintDiamond(UiPainter p, DiagramFrame frame, LinkRoute route, uint color, float stroke)
    {
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

    /// <summary>Where the route's arrowhead starts: where its pieces end, or at its tip when it has none.</summary>
    private static PointF GetArrowTail(LinkRoute route)
    {
        if (route.Pieces.Count == 0)
        {
            return route.Tip;
        }

        return route.Pieces[^1].End;
    }

    private static bool IsHeld(TypeLinkKind kind)
    {
        return kind is TypeLinkKind.HoldsOne or TypeLinkKind.HoldsMany;
    }

    /// <summary>The label of a link as a tag bordered in its color.</summary>
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
        switch (row.Content)
        {
            case CardRowContent.Name name:
                PaintIcon(p, frame, rect, left, name.Icon, name.Accessibility);
                PaintTextAfterIcon(p, frame, rect, left, row, frame.Theme.Text, bold: true, underlined);
                break;
            case CardRowContent.Stereotype:
                PaintText(p, frame, rect, left, row.Text, frame.Theme.TextMuted, family: TextFonts.Italic);
                break;
            case CardRowContent.Fold fold:
                PaintFold(p, frame, rect, left, row, fold.IsOpen);
                break;
            case CardRowContent.Member member:
                PaintIcon(p, frame, rect, left, member.Icon, member.Accessibility);
                PaintTextAfterIcon(p, frame, rect, left, row, frame.Theme.Text, bold: false, underlined);
                break;
            default:
                throw new UnreachableException();
        }
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

    /// <summary>The icon of a node of <paramref name="kind"/> as the tree draws it, with the overlay of its
    /// <paramref name="accessibility"/>.</summary>
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
