namespace ILSight.Core;

/// <summary>A NuGet feed's identity: its service index URL, compared ignoring case, as NuGet and
/// <see cref="NuGetClient"/> key their feeds. Every comparison of it, or of a value holding it (a
/// <see cref="NuGetSource"/>, a search of one), agrees that the same URL typed in other letters is the same
/// feed.</summary>
public sealed record FeedUrl
{
    /// <summary>The URL as it was given.</summary>
    public required WebUrl Address { get; init; }

#pragma warning disable RL0408 // A one-part wrapper hiding nothing: the URL is the whole feed identity, and its text is what equality, hashing and display compare.
    public string Text => Address.Text;
#pragma warning restore RL0408

    public bool Equals(FeedUrl? other)
    {
        if (other is null)
        {
            return false;
        }

        return string.Equals(Text, other.Text, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Text);
    }

    public override string ToString()
    {
        return Text;
    }
}
