using ILSight.Core;
using ILSight.Core.Diagrams;

namespace ILSight;

/// <summary>The types a diagram tab shows, as cards in the order added, the links among them, the
/// assemblies of the types the user put on it, and where its newest layout put them. A layout started for
/// it cancels the one before, whose result is then dropped; the layout it holds is the newest that
/// finished, which may not place the cards added since. Once closed, what is in flight for it is
/// cancelled and its results dropped. Mutated on the UI thread only.</summary>
public sealed class TypeDiagram
{
    public readonly List<TypeCard> Cards = new();
    public readonly HashSet<CodeReference.Entity> Types = new();

    /// <summary>The links of the cards to one another.</summary>
    public readonly List<TypeLink> Links = new();

    /// <summary>The files of the diagram's own assemblies.</summary>
    public readonly HashSet<AssemblyPath> HomeAssemblies = new();
    public readonly CancellationTokenSource Open = new();
    public CancellationTokenSource? LayoutWork;
    public int Reading;

    /// <summary>The newest layout that finished, or null before the first.</summary>
    public TypeLayout? Layout { get; set; }

    /// <summary>Changes whenever a card is added or removed.</summary>
    public int Version { get; set; }

    /// <summary>True while cards are read or laid out in the background.</summary>
    public bool IsBusy => Reading > 0 || LayoutWork is not null;

    /// <summary>Cancelled once the diagram is closed.</summary>
    public CancellationToken Token => Open.Token;

    public bool IsClosed => Open.IsCancellationRequested;
}
