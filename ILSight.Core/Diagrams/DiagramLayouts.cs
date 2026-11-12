using System.Diagnostics;
using Microsoft.Msagl.Core;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using ZGF.Geometry;
using MsaglPoint = Microsoft.Msagl.Core.Geometry.Point;
using MsaglRectangle = Microsoft.Msagl.Core.Geometry.Rectangle;

namespace ILSight.Core.Diagrams;

/// <summary>Laying out a diagram with MSAGL, and the curves a link's route is made of.</summary>
public static class DiagramLayouts
{
    private const double LayerSeparation = 56;
    private const double NodeSeparation = 32;
    private const int SamplesPerCurve = 8;
    private const float OneThird = 1f / 3f;
    private const float TwoThirds = 2f / 3f;
    private const float Half = 0.5f;

    /// <summary>The straight line from <paramref name="start"/> to <paramref name="end"/>, its controls on it.</summary>
    public static RoutePiece CreateLine(PointF start, PointF end)
    {
        return new RoutePiece(start, Lerp(start, end, OneThird), Lerp(start, end, TwoThirds), end);
    }

    /// <summary>The point <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static PointF Lerp(PointF from, PointF to, float t)
    {
        return new PointF(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
    }

    /// <summary>The two halves of <paramref name="piece"/>, split at its middle.</summary>
    public static (RoutePiece First, RoutePiece Second) Split(RoutePiece piece)
    {
        var startSide = Lerp(piece.Start, piece.Control1, Half);
        var middleSide = Lerp(piece.Control1, piece.Control2, Half);
        var endSide = Lerp(piece.Control2, piece.End, Half);
        var firstControl = Lerp(startSide, middleSide, Half);
        var secondControl = Lerp(middleSide, endSide, Half);
        var middle = Lerp(firstControl, secondControl, Half);
        return (new RoutePiece(piece.Start, startSide, firstControl, middle), new RoutePiece(middle, secondControl, endSide, piece.End));
    }

    /// <summary>The curve <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/>,
    /// each point moved alike.</summary>
    public static RoutePiece Blend(RoutePiece from, RoutePiece to, float t)
    {
        return new RoutePiece(Lerp(from.Start, to.Start, t), Lerp(from.Control1, to.Control1, t), Lerp(from.Control2, to.Control2, t), Lerp(from.End, to.End, t));
    }

    /// <summary>Lays out the cards of <paramref name="measures"/> and routes the links between them; a
    /// link to a card not measured is left out.</summary>
    public static TypeLayout Arrange(DiagramMeasures measures, CancellationToken cancellationToken = default)
    {
        if (measures.Cards.Count == 0)
        {
            return new TypeLayout { Places = new Dictionary<CodeReference.Entity, PointF>(), Routes = [], Width = 0f, Height = 0f };
        }

        var graph = new GeometryGraph();
        var nodes = AddNodes(graph, measures.Cards);
        var edges = AddEdges(graph, nodes, measures.Links);
        Calculate(graph, cancellationToken);
        var box = graph.BoundingBox;
        var origin = new MsaglPoint(box.Left, box.Bottom);
        return new TypeLayout { Places = PlacesOf(nodes, origin), Routes = RoutesOf(edges, origin), Width = (float)box.Width, Height = (float)box.Height };
    }

    private static Dictionary<CodeReference.Entity, Node> AddNodes(GeometryGraph graph, IReadOnlyList<MeasuredCard> cards)
    {
        var nodes = new Dictionary<CodeReference.Entity, Node>();
        foreach (var card in cards)
        {
            var boundary = CurveFactory.CreateRectangle(card.Width, card.Height, new MsaglPoint());
            var node = new Node(boundary, card.Type);
            graph.Nodes.Add(node);
            nodes[card.Type] = node;
        }
        return nodes;
    }

    private static List<Edge> AddEdges(GeometryGraph graph, Dictionary<CodeReference.Entity, Node> nodes, IReadOnlyList<MeasuredLink> links)
    {
        var edges = new List<Edge>();
        foreach (var link in links)
        {
            var edge = CreateEdge(nodes, link);
            if (edge is null)
            {
                continue;
            }

            graph.Edges.Add(edge);
            edges.Add(edge);
        }
        return edges;
    }

    private static Edge? CreateEdge(Dictionary<CodeReference.Entity, Node> nodes, MeasuredLink link)
    {
        if (!nodes.TryGetValue(link.Link.From, out var source))
        {
            return null;
        }

        if (!nodes.TryGetValue(link.Link.To, out var target))
        {
            return null;
        }

        var edge = new Edge(source, target) { UserData = link.Link };
        edge.EdgeGeometry.TargetArrowhead = new Arrowhead { Length = TypeLayout.ArrowLength };
        if (link.Link.IsHeld)
        {
            edge.EdgeGeometry.SourceArrowhead = new Arrowhead { Length = TypeLayout.DiamondLength };
        }

        if (HasLabel(link))
        {
            edge.Label = new Label(link.LabelWidth, link.LabelHeight, edge);
        }

        return edge;
    }

    private static bool HasLabel(MeasuredLink link)
    {
        if (link.LabelWidth <= 0f)
        {
            return false;
        }

        return link.LabelHeight > 0f;
    }

    /// <summary>Lays out <paramref name="graph"/>, stopping with <see cref="OperationCanceledException"/>
    /// once <paramref name="cancellationToken"/> is cancelled.</summary>
    private static void Calculate(GeometryGraph graph, CancellationToken cancellationToken)
    {
        var settings = new SugiyamaLayoutSettings { LayerSeparation = LayerSeparation, NodeSeparation = NodeSeparation };
        var cancel = new CancelToken();
        using var registration = cancellationToken.Register(Cancel, cancel);
        LayoutHelpers.CalculateLayout(graph, settings, cancel);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Cancel(object? cancel)
    {
        if (cancel is not CancelToken token)
        {
            throw new UnreachableException("Calculate registers Cancel with the CancelToken it cancels.");
        }

        token.Canceled = true;
    }

    private static Dictionary<CodeReference.Entity, PointF> PlacesOf(Dictionary<CodeReference.Entity, Node> nodes, MsaglPoint origin)
    {
        var places = new Dictionary<CodeReference.Entity, PointF>();
        foreach (var (type, node) in nodes)
        {
            places[type] = TopLeftOf(node.BoundingBox, origin);
        }

        return places;
    }

    /// <summary>The corner of <paramref name="box"/> nearest <paramref name="origin"/>, which the layout
    /// draws at its top-left: MSAGL's Y runs up, with the types derived from lowest, and the layout is
    /// drawn with it running down.</summary>
    private static PointF TopLeftOf(MsaglRectangle box, MsaglPoint origin)
    {
        return Place(new MsaglPoint(box.Left, box.Bottom), origin);
    }

    private static PointF Place(MsaglPoint point, MsaglPoint origin)
    {
        return new PointF((float)(point.X - origin.X), (float)(point.Y - origin.Y));
    }

    private static List<LinkRoute> RoutesOf(List<Edge> edges, MsaglPoint origin)
    {
        var routes = new List<LinkRoute>();
        foreach (var edge in edges)
        {
            var route = RouteOf(edge, origin);
            if (route is not null)
            {
                routes.Add(route);
            }
        }
        return routes;
    }

    private static LinkRoute? RouteOf(Edge edge, MsaglPoint origin)
    {
        var curve = edge.Curve;
        if (curve is null)
        {
            return null;
        }

        var pieces = new List<RoutePiece>();
        AddPieces(pieces, curve, origin);
        var tip = Place(edge.EdgeGeometry.TargetArrowhead.TipPosition, origin);
        if (edge.UserData is not TypeLink link)
        {
            throw new UnreachableException("Every edge of the graph carries the link it was made for.");
        }

        return new LinkRoute(link, pieces, tip, LabelCenterOf(edge, origin), SourceTipOf(edge, origin));
    }

    private static PointF? SourceTipOf(Edge edge, MsaglPoint origin)
    {
        var diamond = edge.EdgeGeometry.SourceArrowhead;
        if (diamond is null)
        {
            return null;
        }

        return Place(diamond.TipPosition, origin);
    }

    private static PointF? LabelCenterOf(Edge edge, MsaglPoint origin)
    {
        if (edge.Label is null)
        {
            return null;
        }

        return Place(edge.Label.Center, origin);
    }

    /// <summary>Adds <paramref name="curve"/> as pieces: its lines and Bézier segments as they are, and
    /// any other segment as lines through points along it.</summary>
    private static void AddPieces(List<RoutePiece> pieces, ICurve curve, MsaglPoint origin)
    {
        switch (curve)
        {
            case Curve compound:
                foreach (var segment in compound.Segments)
                {
                    AddPieces(pieces, segment, origin);
                }

                break;
            case LineSegment line:
                pieces.Add(CreateLine(Place(line.Start, origin), Place(line.End, origin)));
                break;
            case CubicBezierSegment bezier:
                pieces.Add(BezierPiece(bezier, origin));
                break;
            default:
                AddSampled(pieces, curve, origin);
                break;
        }
    }

    private static RoutePiece BezierPiece(CubicBezierSegment bezier, MsaglPoint origin)
    {
        return new RoutePiece(Place(bezier.B(0), origin), Place(bezier.B(1), origin), Place(bezier.B(2), origin), Place(bezier.B(3), origin));
    }

    private static void AddSampled(List<RoutePiece> pieces, ICurve curve, MsaglPoint origin)
    {
        var step = (curve.ParEnd - curve.ParStart) / SamplesPerCurve;
        var previous = Place(curve.Start, origin);
        for (var i = 1; i <= SamplesPerCurve; i++)
        {
            var next = Place(curve[curve.ParStart + step * i], origin);
            pieces.Add(CreateLine(previous, next));
            previous = next;
        }
    }
}
