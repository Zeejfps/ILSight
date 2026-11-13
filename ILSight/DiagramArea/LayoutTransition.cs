using ZGF.Geometry;
using ZGF.Gui.Immediate;
using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight.DiagramArea;

/// <summary>The layout a diagram is drawn in, and while a newer one comes in, the move from the one
/// before: cards glide from their old places to their new ones, and links bend from their old routes to
/// their new ones, over a fixed time. Cards and links the old layout had not appear in place.</summary>
public sealed class LayoutTransition
{
    public const float Seconds = 0.3f;

    private TypeLayout? _from;
    private TypeLayout? _to;
    private Dictionary<TypeLink, LinkRoute> _fromRoutes = new();
    private float _elapsed = Seconds;

    /// <summary>The layout drawn, or moved to; null before the first.</summary>
    public TypeLayout? Layout => _to;

    public bool IsMoving => _elapsed < Seconds;

    public float Progress => Ease.OutCubic(_elapsed / Seconds);

    /// <summary>Starts moving to <paramref name="layout"/> when it is another than <see cref="Layout"/>;
    /// the first layout is drawn at once. True when it was another.</summary>
    public bool TryFollowLayout(TypeLayout? layout)
    {
        if (ReferenceEquals(layout, _to))
        {
            return false;
        }

        _from = _to;
        _to = layout;
        _fromRoutes = ListRoutesByLink(_from);
        _elapsed = GetTransitionStart(_from);
        return true;
    }

    private static Dictionary<TypeLink, LinkRoute> ListRoutesByLink(TypeLayout? layout)
    {
        var routes = new Dictionary<TypeLink, LinkRoute>();
        if (layout is null)
        {
            return routes;
        }

        foreach (var route in layout.Routes)
        {
            routes.TryAdd(route.Link, route);
        }

        return routes;
    }

    /// <summary>How far into the move a move from <paramref name="from"/> starts: at its end when there is
    /// nothing to move from.</summary>
    private static float GetTransitionStart(TypeLayout? from)
    {
        if (from is null)
        {
            return Seconds;
        }

        return 0f;
    }

    public void Advance(float seconds)
    {
        _elapsed = MathF.Min(Seconds, _elapsed + seconds);
    }

    /// <summary>Where the top-left of the card of <paramref name="type"/> is drawn now; null when the
    /// layout does not place it.</summary>
    public PointF? FindCardPlace(CodeReference.Entity type)
    {
        var layout = _to;
        if (layout is null)
        {
            return null;
        }

        if (!layout.Places.TryGetValue(type, out var to))
        {
            return null;
        }

        var from = FindOldCardPlace(type);
        if (from is null)
        {
            return to;
        }

        return DiagramLayouts.Lerp(from.Value, to, Progress);
    }

    private PointF? FindOldCardPlace(CodeReference.Entity type)
    {
        if (!IsMoving)
        {
            return null;
        }

        var old = _from;
        if (old is null)
        {
            return null;
        }

        if (!old.Places.TryGetValue(type, out var from))
        {
            return null;
        }

        return from;
    }

    /// <summary>How <paramref name="route"/>, one of the layout's, is drawn now: bent from the route its
    /// link had before while moving, else as it is.</summary>
    public LinkRoute ShapeRoute(LinkRoute route)
    {
        if (!IsMoving)
        {
            return route;
        }

        if (!_fromRoutes.TryGetValue(route.Link, out var from))
        {
            return route;
        }

        if (!CanBlendRoutes(from, route))
        {
            return route;
        }

        return BlendRoutes(from, route, Progress);
    }

    private static bool CanBlendRoutes(LinkRoute from, LinkRoute to)
    {
        if (from.Pieces.Count == 0)
        {
            return false;
        }

        return to.Pieces.Count > 0;
    }

    private static LinkRoute BlendRoutes(LinkRoute from, LinkRoute to, float t)
    {
        var fromPieces = SubdivideRoute(from.Pieces, to.Pieces.Count);
        var toPieces = SubdivideRoute(to.Pieces, from.Pieces.Count);
        var pieces = new List<RoutePiece>(toPieces.Count);
        for (var i = 0; i < toPieces.Count; i++)
        {
            pieces.Add(DiagramLayouts.Blend(fromPieces[i], toPieces[i], t));
        }

        var tip = DiagramLayouts.Lerp(from.Tip, to.Tip, t);
        var labelCenter = BlendPoints(from.LabelCenter, to.LabelCenter, t);
        var sourceTip = BlendPoints(from.SourceTip, to.SourceTip, t);
        return to with { Pieces = pieces, Tip = tip, LabelCenter = labelCenter, SourceTip = sourceTip };
    }

    /// <summary><paramref name="pieces"/>, halved one after another, until there are at least
    /// <paramref name="count"/> of them.</summary>
    private static List<RoutePiece> SubdivideRoute(IReadOnlyList<RoutePiece> pieces, int count)
    {
        var split = pieces.ToList();
        var next = 0;
        while (split.Count < count)
        {
            var (first, second) = DiagramLayouts.Split(split[next]);
            split[next] = first;
            split.Insert(next + 1, second);
            next = (next + 2) % split.Count;
        }
        return split;
    }

    /// <summary>The point <paramref name="t"/> of the way between two a route may have: the one it moves
    /// to when it had none, and none when it moves to none.</summary>
    private static PointF? BlendPoints(PointF? from, PointF? to, float t)
    {
        if (from is null)
        {
            return to;
        }

        if (to is null)
        {
            return null;
        }

        return DiagramLayouts.Lerp(from.Value, to.Value, t);
    }
}
