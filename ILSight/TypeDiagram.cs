using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight;

/// <summary>The types a diagram tab shows, as cards in the order added, the links among them, the
/// assemblies of the types the user put on it, and where its newest layout put them. The layout it holds
/// is the newest that finished, which may not place the cards added since. Once closed, what is in flight
/// for it is cancelled and its results dropped. Its cards, their links and its version change together, only
/// through its methods. It keeps the types it shows apart from their cards: a type made again whose card couldn't be
/// read (the read failed, or its module couldn't be read) stays one of its types, saved with the rest and read again
/// when the diagram is next made again, rather than lost with the card that never came. Mutated on the UI thread
/// only.</summary>
public sealed class TypeDiagram
{
    private readonly List<TypeCard> _cards = new();
    private readonly HashSet<CodeReference.Entity> _types = new();
    private readonly List<TypeLink> _links = new();
    private readonly HashSet<AssemblyPath> _homeAssemblies = new();

    // In the order added: those with a card, those still read, and those whose card couldn't be read.
    private readonly List<TypeAnchor> _kept = new();

    // Its layout, one started cancelling the one before, whose result is then dropped; closing it closes the diagram,
    // cancelling the reading of cards too.
    private readonly LatestWork _work = new();

    private readonly HashSet<CardRead> _reads = new();

    /// <summary>The cards, in the order added.</summary>
    public IReadOnlyList<TypeCard> Cards => _cards;

    /// <summary>The links of the cards to one another.</summary>
    public IReadOnlyList<TypeLink> Links => _links;

    /// <summary>The files of the diagram's own assemblies.</summary>
    public IReadOnlyCollection<AssemblyPath> HomeAssemblies => _homeAssemblies;

    /// <summary>The newest layout that finished, or null before the first.</summary>
    public TypeLayout? Layout { get; private set; }

    /// <summary>Changes whenever a card is added or removed.</summary>
    public int Version { get; private set; }

    /// <summary>True while cards are read or laid out in the background.</summary>
    public bool IsBusy => _reads.Count > 0 || _work.IsRunning;

    /// <summary>Cancelled once the diagram is closed.</summary>
    public CancellationToken Token => _work.UntilClosed;

    public bool IsClosed => _work.IsClosed;

    /// <summary>Whether the diagram reads <paramref name="file"/>: one of its own assemblies, or the assembly of one of
    /// the types it shows.</summary>
    public bool HasTypesOf(AssemblyPath file)
    {
        return _homeAssemblies.Contains(file) || _kept.Exists(t => t.AssemblyPath.Equals(file));
    }

    public bool HasCard(CodeReference.Entity type)
    {
        return _types.Contains(type);
    }

    /// <summary>Adds the cards of <paramref name="cards"/> the diagram does not show yet, after the others.</summary>
    public void AddCards(IReadOnlyList<TypeCard> cards)
    {
        var shown = _cards.Count;
        foreach (var card in cards)
        {
            AddCardIfNew(card);
        }

        if (_cards.Count == shown)
        {
            return;
        }

        ChangeCards();
    }

    private void AddCardIfNew(TypeCard card)
    {
        if (!_types.Add(card.Type))
        {
            return;
        }

        _cards.Add(card);
        Keep(card.Anchor);
    }

    /// <summary>Takes <paramref name="types"/>, in order, as types it shows before their cards are read, as when it is
    /// made again from what was saved: each stays one of its types until it is taken off, or found not to exist.</summary>
    public void Keep(IReadOnlyList<TypeAnchor> types)
    {
        foreach (var type in types)
        {
            Keep(type);
        }
    }

    private void Keep(TypeAnchor type)
    {
        if (_kept.Contains(type))
        {
            return;
        }

        _kept.Add(type);
    }

    /// <summary>Drops <paramref name="types"/>, kept without a card, which the open assemblies were found not to
    /// have.</summary>
    public void Forget(IReadOnlyList<TypeAnchor> types)
    {
        foreach (var type in types)
        {
            _kept.Remove(type);
        }
    }

    /// <summary>Takes the card of <paramref name="type"/> off the diagram.</summary>
    public void RemoveCard(CodeReference.Entity type)
    {
        if (!_types.Remove(type))
        {
            return;
        }

        var removed = _cards.Find(c => c.Type == type);
        if (removed is not null)
        {
            _kept.Remove(removed.Anchor);
        }

        _cards.RemoveAll(c => c.Type == type);
        ChangeCards();
    }

    private void ChangeCards()
    {
        Version++;
        _links.Clear();
        foreach (var card in _cards)
        {
            AddLinksAmongCards(card);
        }
    }

    private void AddLinksAmongCards(TypeCard card)
    {
        foreach (var link in card.Links)
        {
            if (_types.Contains(link.To))
            {
                _links.Add(link);
            }
        }
    }

    /// <summary>Takes the assembly at <paramref name="assemblyPath"/> as one of the diagram's own, whose members show
    /// even when hidden from other assemblies.</summary>
    public void TakeHome(AssemblyPath assemblyPath)
    {
        _homeAssemblies.Add(assemblyPath);
    }

    /// <summary>Takes each of <paramref name="assemblies"/>, if any, as one of the diagram's own.</summary>
    public void TakeHomes(IReadOnlyList<AssemblyPath>? assemblies)
    {
        if (assemblies is null)
        {
            return;
        }

        foreach (var assembly in assemblies)
        {
            TakeHome(assembly);
        }
    }

    /// <summary>Whether the assembly at <paramref name="assemblyPath"/> is one of the diagram's own; false
    /// for none.</summary>
    public bool IsHome(AssemblyPath? assemblyPath)
    {
        if (assemblyPath is null)
        {
            return false;
        }

        return _homeAssemblies.Contains(assemblyPath);
    }

    /// <summary>The types the diagram shows, in the order added, which make it again: those with a card, and those
    /// kept whose card is still read or couldn't be read.</summary>
    public List<TypeAnchor> ListAnchors()
    {
        return _kept.ToList();
    }

    /// <summary>Counts a read of cards for the diagram as started, until <see cref="FinishReading"/> is handed what this
    /// returns.</summary>
    public CardRead StartReading()
    {
        var read = new CardRead();
        _reads.Add(read);
        return read;
    }

    /// <summary>Counts <paramref name="read"/> as ended; nothing when it already was.</summary>
    public void FinishReading(CardRead read)
    {
        _reads.Remove(read);
    }

    /// <summary>Starts laying out the diagram, cancelling the layout in flight, whose result is then dropped. Returns
    /// the work the layout runs as, for the edge to start (<see cref="DiagramArea.LayOutDiagramHandler"/>) on a token
    /// a newer layout and closing the diagram cancel; null when the diagram is closed.</summary>
    public WorkTicket? StartLayout()
    {
        if (IsClosed)
        {
            return null;
        }

        return _work.Start();
    }

    /// <summary>Ends the layout <paramref name="layout"/> was given for. False when a newer one started since, or
    /// the diagram was closed, and its result should be dropped.</summary>
    public bool TryFinishLayout(WorkTicket layout)
    {
        return _work.TryFinish(layout);
    }

    /// <summary>Holds <paramref name="layout"/>, which the newest layout finished with.</summary>
    public void ShowLayout(TypeLayout layout)
    {
        Layout = layout;
    }

    /// <summary>Closes the diagram: what is in flight for it is cancelled and its results dropped.</summary>
    public void Close()
    {
        _work.Close();
    }
}
