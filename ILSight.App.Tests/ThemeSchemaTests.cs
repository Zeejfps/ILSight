using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ZGF.Gui.Immediate;
using ILSight.CodeView;
using ILSight.BuiltInAssets;

namespace ILSight.App.Tests;

public sealed class ThemeSchemaTests
{
    private const string HexPattern = "^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$";
    private const string PaletteNamePattern = "^[^#]";

    [Fact]
    public void The_checked_in_schema_lists_every_themeable_color()
    {
        var generated = Generate();
        var checkedIn = BuiltInThemes.ReadSchema().ReplaceLineEndings("\n");

        if (generated != checkedIn)
        {
            var received = Path.Combine(Path.GetTempPath(), "theme.schema.received.json");
            File.WriteAllText(received, generated);
            Assert.Fail($"ILSight/Assets/Themes/theme.schema.json is out of date; the generated schema is at {received}.");
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
        w.WriteString("title", "ILSight color theme");
        w.WriteString("description", "A color theme. Comments and trailing commas are allowed. The file's name without .json is the theme's id.");
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartObject("properties");
        WriteString(w, "$schema", "The schema theme files follow.");
        WriteName(w);
        WriteType(w);
        WriteString(w, "extends", "The id of a built-in or user theme to build on; what this theme leaves out comes from it.");
        WritePalette(w);
        WriteSection(w, "ui", DescribeUi(), UiTheme.ColorKeys);
        WriteSection(w, "code", "The code view's colors. Those left out come from the theme extended, else from the built-in theme of the type.", CodeViewPalette.ColorKeys);
        WriteSection(w, "icons", "The tree's icon colors. Those left out come from the theme extended, else from the built-in theme of the type.", NodeIconPalette.ColorKeys);
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
        w.WriteString("description", "The name the theme is listed by: one for every language, or one per language tag, such as en or pt-BR, where English stands in for a language without one.");
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

    private static void WriteType(Utf8JsonWriter w)
    {
        w.WriteStartObject("type");
        w.WriteString("description", "Whether the theme is dark or light. A theme that extends no other takes the code and icon colors of the built-in theme of its type.");
        w.WriteStartArray("enum");
        w.WriteStringValue("dark");
        w.WriteStringValue("light");
        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WritePalette(Utf8JsonWriter w)
    {
        w.WriteStartObject("palette");
        w.WriteString("description", "Named colors the ui, code and icons sections can use by name. A theme's palette is merged over those of the themes it extends.");
        w.WriteString("type", "object");
        w.WriteStartObject("additionalProperties");
        w.WriteString("$ref", "#/definitions/hexColor");
        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static string DescribeUi()
    {
        var required = UiTheme.ColorKeys
            .Where(k => k.IsRequired)
            .Select(k => k.Key);
        return $"The interface's colors. Required unless an extended theme sets them: {string.Join(", ", required)}. The others are derived from these when left out.";
    }

    private static void WriteSection(Utf8JsonWriter w, string name, string description, IReadOnlyList<ThemeColorKey> keys)
    {
        w.WriteStartObject(name);
        w.WriteString("description", description);
        w.WriteString("type", "object");
        w.WriteBoolean("additionalProperties", false);
        w.WriteStartObject("properties");
        foreach (var key in keys)
        {
            WriteColorKey(w, key);
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteColorKey(Utf8JsonWriter w, ThemeColorKey key)
    {
        w.WriteStartObject(key.Key);
        if (key.Description.Length > 0)
        {
            w.WriteString("description", key.Description);
        }

        w.WriteString("$ref", "#/definitions/color");
        w.WriteEndObject();
    }

    private static void WriteDefinitions(Utf8JsonWriter w)
    {
        w.WriteStartObject("definitions");
        w.WriteStartObject("hexColor");
        w.WriteString("description", "#RRGGBB, or #RRGGBBAA with alpha. #00000000 reads as unset, so it is refused; for an invisible color write another transparent one, such as #FFFFFF00.");
        w.WriteString("type", "string");
        w.WriteString("pattern", HexPattern);
        w.WriteStartObject("not");
        w.WriteString("const", "#00000000");
        w.WriteEndObject();
        w.WriteEndObject();
        w.WriteStartObject("color");
        w.WriteStartArray("anyOf");
        w.WriteStartObject();
        w.WriteString("$ref", "#/definitions/hexColor");
        w.WriteEndObject();
        w.WriteStartObject();
        w.WriteString("description", "The name of a palette color.");
        w.WriteString("type", "string");
        w.WriteString("pattern", PaletteNamePattern);
        w.WriteEndObject();
        w.WriteEndArray();
        w.WriteEndObject();
        w.WriteEndObject();
    }
}
