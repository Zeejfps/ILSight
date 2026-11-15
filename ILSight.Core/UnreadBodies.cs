namespace ILSight.Core;

/// <summary>The method bodies of one module a search couldn't read: how many, and why the first couldn't.</summary>
internal sealed class UnreadBodies
{
    public int Count { get; private set; }

    public string? FirstWhy { get; private set; }

    public void Note(BadImageFormatException why)
    {
        Count++;
        FirstWhy ??= why.Message;
    }
}
