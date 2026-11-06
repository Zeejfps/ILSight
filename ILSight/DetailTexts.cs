using ILSight.Core;

namespace ILSight;

/// <summary>A decoded text under a metadata grid's row, as a code view shows it.</summary>
internal sealed record DetailText(DecompiledDocument Document, CodeViewState View);

/// <summary>How many lines a text has, and how many characters its longest one.</summary>
internal readonly record struct TextSize(int Lines, int Longest);

/// <summary>The decoded texts under the rows of the metadata grid shown, each measured and turned into
/// a document once.</summary>
internal sealed class DetailTexts
{
    public readonly Dictionary<int, DetailText> Texts = new();
    public readonly Dictionary<int, TextSize> Sizes = new();
}
