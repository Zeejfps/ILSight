using System.Diagnostics;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;
using ILSight.CommandArea;

namespace ILSight.DiagramArea;

/// <summary>The faces of a diagram's cards as the screen's fonts draw them, measured again whenever the
/// cards, their open parts, the member filter, the font or the language change, with a new layout of the
/// diagram asked for each time.</summary>
internal static class DiagramFaces
{
    /// <summary>How wide the chevron of a fold row is.</summary>
    public const float ChevronWidth = 14f;

    /// <summary>The space between a row's icon and its text.</summary>
    public const float IconGap = 6f;

    private const float Padding = 8f;
    private const float RowPadding = 6f;
    private const float NameRowExtra = 8f;
    private const float BottomPadding = 4f;
    private const float RightPadding = 16f;
    private const float MinWidth = 120f;
    private const float LabelPadding = 4f;
    private const string ManyMark = " *";
    private const string Abstract = "«abstract»";
    private const string Static = "«static»";
    private const string Interface = "«interface»";

    /// <summary>Measures the faces of <paramref name="diagram"/>'s cards when what they were measured for
    /// changed, and asks the app state to lay the diagram out at their new sizes.</summary>
    public static void MeasureIfChanged(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram)
    {
        var key = KeyOf(ui, view, diagram);
        if (view.MeasuredFor == key)
        {
            return;
        }

        var faces = Measure(ui, view, diagram);
        view.KeepFaces(key, faces, diagram.Version);
        LayOutLater(ui.State, diagram, MeasuresOf(ui, diagram, faces));
    }

    /// <summary>Asks for <paramref name="diagram"/> to be laid out once the frame is drawn, its cards and labels as
    /// large as <paramref name="measures"/> says, cancelling the layout in flight (<see cref="TypeDiagram.StartLayout"/>),
    /// on a token a newer layout and closing the diagram cancel.</summary>
    internal static void LayOutLater(UiState uiState, TypeDiagram diagram, DiagramMeasures measures)
    {
        Commands.StartLater(uiState, nameof(LayOutDiagramHandler), (diagram, measures), static (_, s) => s.diagram.StartLayout(),
            static (a, s, layout, ct) => a.LayOutDiagramHandler.HandleAsync(s.diagram, layout, s.measures, ct), static layout => layout.Token);
    }

    private static FaceKey KeyOf(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram)
    {
        var theme = ui.Theme;
        return new FaceKey
        {
            DiagramVersion = diagram.Version,
            FoldsVersion = view.FoldsVersion,
            ShowsHiddenMembers = view.ShowsHiddenMembers,
            FontSize = theme.FontSize,
            FontFamily = theme.FontFamily,
            Strings = ui.State.Strings,
        };
    }

    private static List<CardFace> Measure(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram)
    {
        return diagram.Cards
            .Select(c => MeasureCard(ui, view, diagram, c))
            .ToList();
    }

    private static CardFace MeasureCard(Ui<UiState> ui, DiagramViewState view, TypeDiagram diagram, TypeCard card)
    {
        var stack = new RowStack { Ui = ui, RowHeight = MathF.Ceiling(ui.LineHeight()) + RowPadding };
        AddHeader(stack, card);
        var headerHeight = stack.Top;
        foreach (var section in card.Sections)
        {
            AddSection(stack, view, diagram, card, section);
        }

        foreach (var group in card.Inherited)
        {
            AddInherited(stack, view, diagram, card, group);
        }

        var width = MathF.Max(MinWidth, stack.Width + RightPadding);
        return new CardFace
        {
            Card = card,
            Rows = stack.Rows,
            Width = width,
            Height = stack.Top + BottomPadding,
            HeaderHeight = headerHeight,
        };
    }

    private static void AddHeader(RowStack stack, TypeCard card)
    {
        AddRow(stack, new CardRow
        {
            Content = new CardRowContent.Name
            {
                Icon = NodeIcons.Of(card.Shape),
                Accessibility = card.Accessibility,
                Type = card.Type,
            },
            Height = stack.RowHeight + NameRowExtra,
            Indent = Padding,
            Text = card.Name,
        });
        var stereotype = StereotypeOf(card.Stereotype);
        if (stereotype is null)
        {
            return;
        }

        var indent = Padding + stack.IconSize + IconGap;
        AddRow(stack, new CardRow
        {
            Content = new CardRowContent.Stereotype(),
            Height = stack.RowHeight,
            Indent = indent,
            Text = stereotype,
        });
    }

    private static string? StereotypeOf(TypeStereotype stereotype)
    {
        return stereotype switch
        {
            TypeStereotype.Abstract => Abstract,
            TypeStereotype.Static => Static,
            TypeStereotype.Interface => Interface,
            TypeStereotype.None => null,
            _ => throw new UnreachableException(),
        };
    }

    private static void AddSection(RowStack stack, DiagramViewState view, TypeDiagram diagram, TypeCard card, CardSection section)
    {
        var members = ListShown(view, diagram, card.AssemblyPath, section.Members);
        var title = TitleOf(stack.Strings, section.Kind);
        AddFold(stack, view, new CardFold.Own { Card = card.Anchor, Section = section.Kind }, title, members);
    }

    private static string TitleOf(Strings t, MemberSection section)
    {
        return section switch
        {
            MemberSection.Fields => t.DiagramFields,
            MemberSection.Properties => t.DiagramProperties,
            MemberSection.Methods => t.DiagramMethods,
            MemberSection.Events => t.DiagramEvents,
            _ => throw new UnreachableException(),
        };
    }

    private static void AddInherited(RowStack stack, DiagramViewState view, TypeDiagram diagram, TypeCard card, InheritedGroup group)
    {
        var members = ListShown(view, diagram, group.AssemblyPath, group.Members);
        var title = stack.Strings.DiagramInheritedFrom(group.From);
        AddFold(stack, view, new CardFold.Inherited { Card = card.Anchor, From = group.From }, title, members);
    }

    /// <summary>The members of <paramref name="members"/>, of the assembly at <paramref name="assemblyPath"/>,
    /// that the card shows: all of them for one of the diagram's own assemblies or while the filter shows
    /// hidden ones, else those other assemblies can reach.</summary>
    private static List<CardMember> ListShown(DiagramViewState view, TypeDiagram diagram, AssemblyPath? assemblyPath, IReadOnlyList<CardMember> members)
    {
        if (view.ShowsHiddenMembers)
        {
            return members.ToList();
        }

        if (diagram.IsHome(assemblyPath))
        {
            return members.ToList();
        }

        return members
            .Where(static m => m.IsVisibleOutside)
            .ToList();
    }

    /// <summary>Adds the row of <paramref name="fold"/>, titled <paramref name="title"/> with how many members
    /// it holds, and while it is open a row for each member; nothing when it holds none.</summary>
    private static void AddFold(RowStack stack, DiagramViewState view, CardFold fold, string title, List<CardMember> members)
    {
        if (members.Count == 0)
        {
            return;
        }

        var isOpen = Diagrams.IsFoldOpen(view, fold);
        var text = $"{title} ({members.Count})";
        AddRow(stack, new CardRow
        {
            Content = new CardRowContent.Fold
            {
                Part = fold,
                IsOpen = isOpen,
            },
            Height = stack.RowHeight,
            Indent = Padding,
            Text = text,
        });
        if (!isOpen)
        {
            return;
        }

        foreach (var member in members)
        {
            AddRow(stack, MemberRow(stack, member));
        }
    }

    private static CardRow MemberRow(RowStack stack, CardMember member)
    {
        return new CardRow
        {
            Content = new CardRowContent.Member
            {
                Icon = NodeIcons.Of(member.Shape),
                Accessibility = member.Accessibility,
                Reference = member.Reference,
            },
            Height = stack.RowHeight,
            Indent = Padding + ChevronWidth,
            Text = member.Text,
        };
    }

    private static DiagramMeasures MeasuresOf(Ui<UiState> ui, TypeDiagram diagram, List<CardFace> faces)
    {
        var cards = faces
            .Select(static f => new MeasuredCard { Type = f.Card.Type, Width = f.Width, Height = f.Height })
            .ToList();
        var links = diagram.Links
            .Select(l => MeasureLink(ui, l))
            .ToList();
        return new DiagramMeasures { Cards = cards, Links = links };
    }

    private static MeasuredLink MeasureLink(Ui<UiState> ui, TypeLink link)
    {
        var label = LabelOf(link);
        if (label is null)
        {
            return new MeasuredLink { Link = link, LabelWidth = 0f, LabelHeight = 0f };
        }

        var size = MeasureLabel(ui, label);
        return new MeasuredLink { Link = link, LabelWidth = size.Width, LabelHeight = size.Height };
    }

    /// <summary>How large <paramref name="label"/> is drawn on a link, with room around it.</summary>
    public static (float Width, float Height) MeasureLabel(Ui<UiState> ui, string label)
    {
        var size = ui.Theme.FontSizeSmall;
        var width = ui.MeasureText(label, size) + LabelPadding * 2;
        return (width, ui.LineHeight(size));
    }

    /// <summary>The label <paramref name="link"/> is drawn with: the property holding the type, marked
    /// when it holds many; null for a link that holds nothing.</summary>
    public static string? LabelOf(TypeLink link)
    {
        return link.Match<string?>(
            inherits: static _ => null,
            implements: static _ => null,
            holdsOne: static one => one.Property,
            holdsMany: static many => many.Property + ManyMark);
    }

    private static void AddRow(RowStack stack, CardRow row)
    {
        var textWidth = MeasureRowText(stack.Ui, row);
        stack.Rows.Add(row with { Top = stack.Top, TextWidth = textWidth });
        stack.Top += row.Height;
        stack.Width = MathF.Max(stack.Width, row.Indent + GetLead(stack, row) + textWidth);
    }

    private static float MeasureRowText(Ui<UiState> ui, CardRow row)
    {
        return row.Content.Match((ui, text: row.Text),
            name: static (s, _) => s.ui.MeasureText(s.text, bold: true),
            stereotype: static (s, _) => s.ui.MeasureText(s.text, family: TextFonts.Italic),
            fold: static (s, _) => s.ui.MeasureText(s.text),
            member: static (s, _) => s.ui.MeasureText(s.text));
    }

    // What a row draws before its text: a chevron, an icon, or nothing.
    private static float GetLead(RowStack stack, CardRow row)
    {
        return row.Content.Match(stack,
            name: static (stack, _) => stack.IconSize + IconGap,
            stereotype: static (_, _) => 0f,
            fold: static (_, _) => ChevronWidth,
            member: static (stack, _) => stack.IconSize + IconGap);
    }

    /// <summary>The rows of a card stacked from its top, and how wide the widest of them is, measured in
    /// the screen's fonts.</summary>
    private sealed class RowStack
    {
        public required Ui<UiState> Ui { get; init; }

        public List<CardRow> Rows { get; } = new();

        public float Top;

        public float Width;

        public required float RowHeight { get; init; }

        public float IconSize => Ui.Theme.IconSize;

        public Strings Strings => Ui.State.Strings;
    }
}
