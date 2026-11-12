namespace ILSight.MetadataArea;

/// <summary>The decoded texts under the rows of the metadata grid shown, each measured and turned into
/// a document once.</summary>
internal sealed class DetailTexts
{
    public readonly Dictionary<int, DetailText> Texts = new();
    public readonly Dictionary<int, TextSize> Sizes = new();
}
