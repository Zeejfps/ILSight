namespace ILSight;

/// <summary>What the settings last kept of the screen, by what it was made from, so that it is handed over again only
/// once something in it changed.</summary>
public sealed class KeptViewSources
{
    private ViewSources _sources;

    public void Keep(ViewSources sources)
    {
        _sources = sources;
    }

    public bool HasChanged(ViewSources now)
    {
        return now != _sources;
    }
}
