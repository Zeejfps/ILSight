using System.Globalization;
using System.Text;

namespace ILSight.Core;

/// <summary>A value from outside (a name or path read from metadata, the file system or the command line, an
/// exception's message) made to stay on the one line of the app's text it is placed in: a note, a tab's title, a tree
/// row, the status bar. The one place every such value passes through, so each place escapes alike.</summary>
public static class OneLineText
{
    private const char Quote = '"';
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char SoftHyphen = (char)0x00AD;

    /// <summary><paramref name="name"/>, a value that names something, as it is when it holds no character escaped
    /// here and doesn't start with a quote, which is nearly every name and every Windows path; otherwise written as a C#
    /// string literal (in quotes, each line break, line separator, other control character and format character, a bidi
    /// override among them, escaped, <c>\n</c>, <c>\u2028</c>, <c>\u000B</c>, <c>\u202E</c>, and
    /// the backslash and the quote themselves, <c>\\</c>, <c>\"</c>), as Git quotes an unusual path. So a name never
    /// breaks its line, and no two names read alike: one holding a line break reads <c>"Shop\nclass"</c>, one holding a
    /// backslash and an <c>n</c> <c>Shop\nclass</c>, and a path keeps its single backslashes.</summary>
    public static string Name(string name)
    {
        if (!NeedsQuotes(name))
        {
            return name;
        }

        var text = new StringBuilder(name.Length + 8);
        text.Append(Quote);
        foreach (var c in name)
        {
            switch (c)
            {
                case '\\':
                    text.Append(@"\\");
                    break;
                case Quote:
                    text.Append("\\\"");
                    break;
                default:
                    AppendEscaped(text, c, IsEscaped(c));
                    break;
            }
        }

        text.Append(Quote);
        return text.ToString();
    }

    /// <summary><paramref name="message"/>, prose (an exception's message), on one line: each line break a space, as a
    /// sentence runs on, and any other control character and every direction control (the marks and embeddings,
    /// overrides and isolates that reorder what follows: U+200E, U+200F, U+061C, U+202A to U+202E, U+2066 to U+2069)
    /// escaped (<c>\t</c>, <c>\u000B</c>, <c>\u202E</c>); its backslashes and quotes as they are, and every other
    /// format character (a joiner in an emoji, a soft hyphen) drawn as text, as prose names nothing by its spelling
    /// (CF5).</summary>
    public static string Message(string message)
    {
        if (!HoldsEscapedInProse(message))
        {
            return message;
        }

        var text = new StringBuilder(message.Length + 8);
        foreach (var c in message.ReplaceLineEndings(" "))
        {
            AppendEscaped(text, c, IsEscapedInProse(c));
        }


        return text.ToString();
    }

    private static bool NeedsQuotes(string name)
    {
        return (name.Length > 0 && name[0] == Quote) || HoldsEscaped(name);
    }

    // A loop rather than LINQ: the tree and the tabs ask it of every name they draw, every frame.
    private static bool HoldsEscaped(string text)
    {
        foreach (var c in text)
        {
            if (IsEscaped(c))
            {
                return true;
            }
        }

        return false;
    }

    // A control character (line breaks among them), a line or paragraph separator, which a text view may break at, or a
    // format character (a bidi override such as U+202E, a zero-width one), which reorders or hides what the line says:
    // the Unicode categories decide which, not a list kept here.
    private static bool IsEscaped(char c)
    {
        return char.IsControl(c) || c is LineSeparator or ParagraphSeparator || IsFormat(c);
    }

    private static bool HoldsEscapedInProse(string text)
    {
        foreach (var c in text)
        {
            if (IsEscapedInProse(c))
            {
                return true;
            }
        }

        return false;
    }

    // In prose only what breaks the line or reorders it is escaped: a control character, a line or paragraph separator,
    // a direction control. A joiner or a soft hyphen draws as the text means it.
    private static bool IsEscapedInProse(char c)
    {
        return char.IsControl(c) || c is LineSeparator or ParagraphSeparator || IsDirectionControl(c);
    }

    // The left-to-right and right-to-left marks, the Arabic letter mark, the embeddings and overrides (U+202A to U+202E)
    // and the isolates (U+2066 to U+2069).
    private static bool IsDirectionControl(char c)
    {
        return c is '\u200E' or '\u200F' or '\u061C' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');
    }

    // No format character comes before the soft hyphen (U+00AD), so an ASCII name, nearly every one, is read without
    // asking each character's category.
    private static bool IsFormat(char c)
    {
        return c >= SoftHyphen && char.GetUnicodeCategory(c) == UnicodeCategory.Format;
    }

    private static void AppendEscaped(StringBuilder text, char c, bool escaped)
    {
        switch (c)
        {
            case '\n':
                text.Append(@"\n");
                break;
            case '\r':
                text.Append(@"\r");
                break;
            case '\t':
                text.Append(@"\t");
                break;
            default:
                if (escaped)
                {
                    text.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    text.Append(c);
                }

                break;
        }
    }
}
