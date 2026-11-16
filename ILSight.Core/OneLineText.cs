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
                    AppendEscaped(text, c);
                    break;
            }
        }

        text.Append(Quote);
        return text.ToString();
    }

    /// <summary><paramref name="message"/>, prose (an exception's message), on one line: each line break a space, as a
    /// sentence runs on, and any other control character and every format character escaped (<c>\t</c>, <c>\u000B</c>,
    /// <c>\u202E</c>); its backslashes and
    /// quotes as they are, as prose names nothing by its spelling.</summary>
    public static string Message(string message)
    {
        if (!HoldsEscaped(message))
        {
            return message;
        }

        var text = new StringBuilder(message.Length + 8);
        foreach (var c in message.ReplaceLineEndings(" "))
        {
            AppendEscaped(text, c);
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

    // No format character comes before the soft hyphen (U+00AD), so an ASCII name, nearly every one, is read without
    // asking each character's category.
    private static bool IsFormat(char c)
    {
        return c >= SoftHyphen && char.GetUnicodeCategory(c) == UnicodeCategory.Format;
    }

    private static void AppendEscaped(StringBuilder text, char c)
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
                if (IsEscaped(c))
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
