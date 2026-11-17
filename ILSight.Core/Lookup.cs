namespace ILSight.Core;

/// <summary>What looking something up in the open assemblies came to: found, not there, or in a module whose type
/// system the library could not read, with why, so not looked for.</summary>
public union Lookup<T>(Lookup<T>.Found, Lookup<T>.NotFound, Lookup<T>.Unreadable) where T : notnull
{
    public sealed record Found
    {
        public required T Value { get; init; }
    }

    public sealed record NotFound;

    public sealed record Unreadable
    {
        public required UnreadableModule Module { get; init; }
    }

    /// <summary>What <paramref name="selector"/> makes of the value found; not found or unreadable as this is.</summary>
    public Lookup<TNext> Select<TNext>(Func<T, TNext> selector) where TNext : notnull
    {
        return this switch
        {
            Found found => new Lookup<TNext>.Found { Value = selector(found.Value) },
            NotFound => new Lookup<TNext>.NotFound(),
            Unreadable unreadable => new Lookup<TNext>.Unreadable { Module = unreadable.Module },
        };
    }

    /// <summary>What <paramref name="next"/> finds from the value found; not found or unreadable as this is, with
    /// nothing more looked up.</summary>
    public Task<Lookup<TNext>> ThenFindAsync<TNext>(Func<T, CancellationToken, Task<Lookup<TNext>>> next, CancellationToken ct) where TNext : notnull
    {
        return this switch
        {
            Found found => next(found.Value, ct),
            NotFound => Task.FromResult<Lookup<TNext>>(new Lookup<TNext>.NotFound()),
            Unreadable unreadable => Task.FromResult<Lookup<TNext>>(new Lookup<TNext>.Unreadable { Module = unreadable.Module }),
        };
    }

    /// <summary>What <paramref name="next"/> finds from the value found, or why it failed; not found or unreadable as
    /// this is, with nothing more looked up.</summary>
    public Task<Outcome<Lookup<TNext>>> ThenTryFindAsync<TNext>(Func<T, CancellationToken, Task<Outcome<Lookup<TNext>>>> next, CancellationToken ct)
        where TNext : notnull
    {
        return this switch
        {
            Found found => next(found.Value, ct),
            NotFound => Task.FromResult<Outcome<Lookup<TNext>>>(new Outcome<Lookup<TNext>>.Ok(new Lookup<TNext>.NotFound())),
            Unreadable unreadable => Task.FromResult<Outcome<Lookup<TNext>>>(
                new Outcome<Lookup<TNext>>.Ok(new Lookup<TNext>.Unreadable { Module = unreadable.Module })),
        };
    }
}

/// <summary>Lookups made from what a search answered, and what a list of lookups came to.</summary>
public static class Lookup
{
    /// <summary><paramref name="value"/> found, or not found when it is null.</summary>
    public static Lookup<T> FromNullable<T>(T? value) where T : class
    {
        if (value is null)
        {
            return new Lookup<T>.NotFound();
        }

        return new Lookup<T>.Found { Value = value };
    }

    /// <inheritdoc cref="FromNullable{T}(T)"/>
    public static Lookup<T> FromNullable<T>(T? value) where T : struct
    {
        if (value is null)
        {
            return new Lookup<T>.NotFound();
        }

        return new Lookup<T>.Found { Value = value.Value };
    }

    /// <summary>The values <paramref name="lookups"/> found, in order.</summary>
    public static List<T> ListFound<T>(IEnumerable<Lookup<T>> lookups) where T : notnull
    {
        var found = new List<T>();
        foreach (var lookup in lookups)
        {
            if (lookup is Lookup<T>.Found hit)
            {
                found.Add(hit.Value);
            }
        }
        return found;
    }

    /// <summary>The modules <paramref name="lookups"/> could not read, each once, in the order first met.</summary>
    public static List<UnreadableModule> ListUnreadable<T>(IEnumerable<Lookup<T>> lookups) where T : notnull
    {
        var unreadable = new List<UnreadableModule>();
        foreach (var lookup in lookups)
        {
            if (lookup is Lookup<T>.Unreadable missed && !unreadable.Exists(u => u.Module == missed.Module.Module))
            {
                unreadable.Add(missed.Module);
            }
        }
        return unreadable;
    }

    /// <summary>Not looked up: <paramref name="module"/>'s type system, which the lookup reads, the library could not
    /// read, as <paramref name="read"/> says.</summary>
    public static Lookup<T> FromUnreadable<T>(LoadedModule module, TypeSystemRead.Unreadable read) where T : notnull
    {
        return new Lookup<T>.Unreadable { Module = new UnreadableModule { Module = module, Why = read.Thrown.SourceException } };
    }
}
