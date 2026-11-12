using System.Text.Json;
using System.Text.Json.Serialization;

namespace ILSight.Core;

/// <summary>Reads <see cref="TextListJson"/> from one string or a list of strings, for <see cref="FeedJson"/>'s
/// shapes.</summary>
internal sealed class TextListConverter : JsonConverter<TextListJson>
{
    public override TextListJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new TextListJson([reader.GetString() ?? ""]);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected a string or a list of them.");
        }

        var items = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                items.Add(reader.GetString() ?? "");
            }
            else
            {
                reader.Skip();
            }
        }
        return new TextListJson(items);
    }

    public override void Write(Utf8JsonWriter writer, TextListJson value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value.Items)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
