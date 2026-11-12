using ILSight.Core;

namespace ILSight.CodeTooltipping;

/// <summary>
/// The tooltips of references hovered in code: each is asked for while the code draws, looked up once, in the
/// background, by a lookup the edge starts (<see cref="LookUpTooltipHandler"/>), and kept while the open assemblies stay
/// the same. A lookup that finishes after they changed is dropped.
/// </summary>
public sealed class CodeTooltips
{
    private readonly Dictionary<CodeReference.Entity, CodeTooltip?> _found = new();
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

    /// <summary>Whether a lookup is still running in the background, or asked for and not started yet.</summary>
    public bool IsLookingUp => _pending.Count > 0;

    /// <summary>Whether lookups were asked for that have not been started yet.</summary>
    public bool HasAsked => _asked.Count > 0;

    /// <summary>Whether the lookup of <paramref name="reference"/> is done, with its <paramref name="tooltip"/>, null
    /// when it has none. Asks for the lookup the first time.</summary>
    public bool TryGetTooltip(CodeReference.Entity reference, out CodeTooltip? tooltip)
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
    public void Finish(TooltipLookup lookup, CodeTooltip? tooltip)
    {
        if (lookup.Version != _version)
        {
            return;
        }

        _pending.Remove(lookup.Reference);
        _found[lookup.Reference] = tooltip;
    }
}
