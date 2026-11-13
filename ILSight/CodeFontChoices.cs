using ZGF.Fonts;

namespace ILSight;

/// <summary>The fonts the display options offer to draw code in: the bundled one, then the installed
/// monospaced families once they are listed, which they are only once.</summary>
public sealed class CodeFontChoices
{
    public const string BundledName = "JetBrains Mono";

    private bool _listingAsked;
    private string[] _labels = [BundledName];

    /// <summary>The fonts in the order offered: null for the bundled one, then the installed ones.</summary>
    public IReadOnlyList<CodeFont?> Fonts { get; private set; } = [null];

    /// <summary>The name of each of <see cref="Fonts"/>.</summary>
    public ReadOnlySpan<string> Labels => _labels;

    public bool IsListed { get; private set; }

    /// <summary>True the first time only: the installed fonts should be listed.</summary>
    public bool TryTakeListingRequest()
    {
        if (_listingAsked)
        {
            return false;
        }

        _listingAsked = true;
        return true;
    }

    /// <summary>Offers the families of <paramref name="families"/> code can be drawn in after the bundled font.</summary>
    public void KeepFonts(IReadOnlyList<SystemFontFamily> families)
    {
        var installed = families
            .Where(IsOfferedFont)
            .Select(CodeDisplay.CreateFont);
        Fonts = new CodeFont?[] { null }
            .Concat(installed)
            .ToArray();
        _labels = Fonts
            .Select(CodeDisplay.GetFontName)
            .ToArray();
        IsListed = true;
    }

    private static bool IsOfferedFont(SystemFontFamily family)
    {
        if (!family.IsMonospaced)
        {
            return false;
        }

        if (family.IsSymbol)
        {
            return false;
        }

        var regular = family.FindRegular();
        if (regular is null)
        {
            return false;
        }

        return family.Name != BundledName;
    }
}
