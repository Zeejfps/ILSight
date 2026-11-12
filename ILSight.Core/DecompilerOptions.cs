namespace ILSight.Core;

/// <summary>The choices that change how C# is decompiled. It holds only the toggles set away from
/// their defaults, and keeps them while <see cref="LanguageVersion"/> is too old for them, so they come
/// back with a newer version.</summary>
public sealed record DecompilerOptions
{
    public static DecompilerOptions Default { get; } = new();

    /// <summary>The C# version written.</summary>
    public CSharpVersion LanguageVersion { get; init; } = CSharpVersion.CSharp14;

    /// <summary>The toggles set away from their defaults, by name, and what each is set to.</summary>
    public OnOffChanges Changes { get; internal init; }

    /// <summary>Whether any toggle is set away from its default.</summary>
    public bool HasChanges => Changes.Count > 0;

    /// <summary>Whether <paramref name="other"/> makes the same choices; the same options are equal at once,
    /// without walking the toggles.</summary>
    public bool Equals(DecompilerOptions? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        if (LanguageVersion != other.LanguageVersion)
        {
            return false;
        }

        if (Changes.Count != other.Changes.Count)
        {
            return false;
        }

        foreach (var (toggle, value) in Changes)
        {
            if (!other.Changes.TryGetValue(toggle, out var otherValue) || otherValue != value)
            {
                return false;
            }
        }
        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LanguageVersion, Changes.Count);
    }
}
