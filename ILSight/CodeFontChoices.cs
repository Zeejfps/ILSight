using ZGF.Fonts;

namespace ILSight;

/// <summary>The fonts the display options offer to draw code in: the bundled one, then the installed
/// monospaced families once they are listed, which they are only once.</summary>
public sealed class CodeFontChoices
{    public const string BundledName = "JetBrains Mono";

    public bool ListingAsked;

    /// <summary>The fonts in the order offered: null for the bundled one, then the installed ones.</summary>
    public IReadOnlyList<CodeFont?> Fonts { get; set; } = [null];

    /// <summary>The name of each of <see cref="Fonts"/>.</summary>
    public string[] Labels { get; set; } = [BundledName];

    public bool IsListed { get; set; }
}
