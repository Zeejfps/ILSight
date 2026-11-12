using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ZGF.Gui.Immediate;
using ILSight.BuiltInArea;

namespace ILSight.App.Tests;

public sealed class IconThemeSchemaTests
{
    private const string CodepointPattern = "^[0-9A-Fa-f]{1,6}$";

    [Fact]
    public void The_checked_in_schema_lists_every_icon_key()
    {
        var generated = Generate();
        var checkedIn = BuiltInIconThemes.ReadSchema().ReplaceLineEndings("\n");

        if (generated != checkedIn)
        {
            var received = Path.Combine(Path.GetTempPath(), "icon-theme.schema.received.json");
            File.WriteAllText(received, generated);
            Assert.Fail($"ILSight/Assets/IconThemes/icon-theme.schema.json is out of date; the generated schema is at {received}.");
        }
    }

    private static string Generate()
    {
        using var stream = new MemoryStream();
        var options = new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            WriteSchema(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteSchema(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteString("$schema", "http://json-schema.org/draft-07/schema#");
        w.WriteString("title", "ILSight icon theme");
        w.WriteString("description", "An icon theme: what each icon draws, a glyph of a font or an SVG picture. Its colors come from the color theme. Comments and trailing commas are allowed. The file's name without .json is the theme's id.");
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartObject("properties");
        WriteString(w, "$schema", "The schema icon theme files follow.");
        WriteName(w);
        WriteString(w, "extends", "The id of a built-in or user icon theme to build on; what this theme leaves out comes from it, and what no theme sets from lucide.");
        WriteFonts(w);
        WriteIcons(w);
        w.WriteEndObject();
        WriteDefinitions(w);
        w.WriteEndObject();
    }

    private static void WriteString(Utf8JsonWriter w, string name, string description)
    {
        w.WriteStartObject(name);
        w.WriteString("description", description);
        w.WriteString("type", "string");
        w.WriteEndObject();
    }

    private static void WriteName(Utf8JsonWriter w)
    {
        w.WriteStartObject("name");
        w.WriteString("description", "The name the icon theme is listed by: one for every language, or one per language tag, such as en or pt-BR, where English stands in for a language without one.");
        w.WriteStartArray("oneOf");
        w.WriteStartObject();
        w.WriteString("type", "string");
        w.WriteEndObject();
        w.WriteStartObject();
        w.WriteString("type", "object");
        w.WriteStartObject("additionalProperties");
        w.WriteString("type", "string");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteFonts(Utf8JsonWriter w)
    {
        w.WriteStartObject("fonts");
        w.WriteString("description", "TrueType or OpenType font files by id, for icons to name. A path is relative to this file and must stay inside its folder. The fonts of the themes extended, and lucide's font, lucide, can be named too; declaring one's id again draws its icons in this font.");
        w.WriteString("type", "object");
        w.WriteStartObject("additionalProperties");
        w.WriteString("type", "string");
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteIcons(Utf8JsonWriter w)
    {
        w.WriteStartObject("icons");
        w.WriteString("description", "What each icon draws. Those left out come from the theme extended, else from lucide.");
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartObject("properties");
        foreach (var key in AppIcons.Keys)
        {
            WriteIconKey(w, key);
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteIconKey(Utf8JsonWriter w, IconKey key)
    {
        w.WriteStartObject(key.Key);
        w.WriteString("description", key.Description);
        w.WriteString("$ref", "#/definitions/icon");
        w.WriteEndObject();
    }

    private static void WriteDefinitions(Utf8JsonWriter w)
    {
        w.WriteStartObject("definitions");
        w.WriteStartObject("icon");
        w.WriteStartArray("oneOf");
        WriteGlyphIcon(w);
        WriteSvgIcon(w);
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteGlyphIcon(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteString("description", "A glyph of a font.");
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartArray("required");
        w.WriteStringValue("font");
        w.WriteStringValue("glyph");
        w.WriteEndArray();
        w.WriteStartObject("properties");
        WriteString(w, "font", "The id of a font in fonts.");
        w.WriteStartObject("glyph");
        w.WriteString("description", "The glyph's codepoint in hex digits, such as E0F2.");
        w.WriteString("type", "string");
        w.WriteString("pattern", CodepointPattern);
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteSvgIcon(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteString("description", "An SVG picture. One that uses currentColor is drawn in the icon's color; one that does not keeps its own colors.");
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartArray("required");
        w.WriteStringValue("svg");
        w.WriteEndArray();
        w.WriteStartObject("properties");
        WriteString(w, "svg", "The SVG file, relative to this file and inside its folder.");
        w.WriteEndObject();
        w.WriteEndObject();
    }
}
