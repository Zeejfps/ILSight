using System.Diagnostics.CodeAnalysis;
using ILSight.Core;

namespace ILSight.CodeTooltipArea;

/// <summary>
/// The tooltips of references hovered in code: each is asked for while the code draws, looked up once, in the
/// background, by a lookup the edge starts (<see cref="LookUpTooltipHandler"/>), and kept while the open assemblies stay
/// the same. A lookup that finishes after they changed is dropped.
/// </summary>
public sealed class CodeTooltips
{
    private readonly Dictionary<CodeReference.Entity, Lookup<CodeTooltip>> _found = new();
    private readonly HashSet<CodeReference.Entity> _pending = new();
    private readonly List<TooltipLookup> _asked = [];
    private int _version;

    public required AssemblyList Assemblies { get; init; }

    /// <summary>The version of <see cref="Assemblies"/> the tooltips kept are of.</summary>
    public required int Version
    {
        get => _version;
        init => _version = value;
    }

    /// <summary>Whether lookups were asked for that have not been started yet.</summary>
    public bool HasAsked => _asked.Count > 0;

    /// <summary>Whether the lookup of <paramref name="reference"/> is done, with what it found,
    /// <paramref name="tooltip"/>: the tooltip, none, or the module it is in that could not be read.</summary>
    public bool TryGetOrAskTooltip(CodeReference.Entity reference, [NotNullWhen(true)] out Lookup<CodeTooltip>? tooltip)
    {
        ForgetIfStale();
        if (_found.TryGetValue(reference, out tooltip))
        {
            return true;
        }

        if (_pending.Add(reference))
        {
            _asked.Add(new TooltipLookup { Reference = reference, Version = _version });
        }

        return false;
    }

    /// <summary>What the tooltip of <paramref name="reference"/> shows once its lookup is done: the tooltip, or the
    /// module it is in that could not be read; null while it is looked up, and when it has none. Asks for the lookup
    /// the first time.</summary>
    public Lookup<CodeTooltip>? FindShown(CodeReference.Entity reference)
    {
        if (!TryGetOrAskTooltip(reference, out var tooltip))
        {
            return null;
        }

        return tooltip.Match<Lookup<CodeTooltip>?>(static found => found, static _ => null, static unreadable => unreadable);
    }

    private void ForgetIfStale()
    {
        if (_version == Assemblies.Version)
        {
            return;
        }

        _version = Assemblies.Version;
        _found.Clear();
        _pending.Clear();
        _asked.Clear();
    }

    /// <summary>The lookups asked for since the last time, for the edge to start.</summary>
    public List<TooltipLookup> TakeAsked()
    {
        var asked = _asked.ToList();
        _asked.Clear();
        return asked;
    }

    /// <summary>Keeps <paramref name="tooltip"/>, what <paramref name="lookup"/> found, unless the open assemblies
    /// changed since it was asked for.</summary>
    public void Finish(TooltipLookup lookup, Lookup<CodeTooltip> tooltip)
    {
        if (lookup.Version != _version)
        {
            return;
        }

        _pending.Remove(lookup.Reference);
        _found[lookup.Reference] = tooltip;
    }
}
