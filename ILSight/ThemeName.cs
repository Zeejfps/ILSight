using System.Diagnostics;

namespace ILSight;

/// <summary>What a theme is called: one name, or a name per language with one to fall back on.</summary>
public abstract record ThemeName
{
    private ThemeName()
    {
    }

    public sealed record Plain(string Name) : ThemeName;

    public sealed record PerLanguage : ThemeName
    {
        /// <summary>Takes <paramref name="byTag"/> as case-insensitive, the first of each tag standing.</summary>
        public PerLanguage(IReadOnlyDictionary<string, string> byTag, string fallback)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (tag, name) in byTag)
            {
                tags.TryAdd(tag, name);
            }

            ByTag = tags;
            Fallback = fallback;
        }

        public IReadOnlyDictionary<string, string> ByTag { get; }

        public string Fallback { get; }
    }

    /// <summary>What the handler of whichever case this is makes of it and <paramref name="state"/>, which it is
    /// handed, so that a handler that needs the caller's values can be static and allocate nothing.</summary>
    public TResult Match<TState, TResult>(TState state, Func<TState, Plain, TResult> plain,
        Func<TState, PerLanguage, TResult> perLanguage)
    {
        return this switch
        {
            Plain c => plain(state, c),
            PerLanguage c => perLanguage(state, c),
            _ => throw new UnreachableException($"ThemeName.Match does not handle {GetType().Name}."),
        };
    }
}
