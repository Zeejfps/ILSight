using ILSight.Core;

namespace ILSight;

/// <summary>The nodes whose children a tree asked to be built while it drew, and those being built: each node's are
/// built once at a time, however many frames ask for them meanwhile, and the edge starts the builds asked for since it
/// last looked (<see cref="BuildChildrenHandler"/>).</summary>
public sealed class ChildBuilds
{
    private readonly HashSet<BrowseNode> _building = new(ReferenceEqualityComparer.Instance);
    private readonly List<BrowseNode> _asked = [];

    /// <summary>Whether builds were asked for that have not been started yet.</summary>
    public bool HasAsked => _asked.Count > 0;

    /// <summary>Asks for the children of <paramref name="node"/> to be built, unless they were asked for already and
    /// have not been built yet.</summary>
    public void Ask(BrowseNode node)
    {
        if (!_building.Add(node))
        {
            return;
        }

        _asked.Add(node);
    }

    /// <summary>The nodes whose children were asked to be built since the last time, for the edge to start; they
    /// count as being built until <see cref="Finish"/>.</summary>
    public List<BrowseNode> TakeAsked()
    {
        var asked = _asked.ToList();
        _asked.Clear();
        return asked;
    }

    /// <summary>Ends the build of the children of <paramref name="node"/>, which may be asked for again.</summary>
    public void Finish(BrowseNode node)
    {
        _building.Remove(node);
    }
}
