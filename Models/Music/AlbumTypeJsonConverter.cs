using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicRUs.Models;

// The API returns enum values as numbers; older catalog responses use strings.
public sealed class AlbumTypeJsonConverter : JsonConverter<string>
{
	// Reads a numeric or string album type into the model's string representation.
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
		reader.TokenType == JsonTokenType.Number
			? reader.GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture)
			: reader.GetString() ?? string.Empty;

	// Writes numeric album types as JSON numbers and other values as strings.
	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		if (int.TryParse(value, out var albumType))
		{
			writer.WriteNumberValue(albumType);
		}
		else
		{
			writer.WriteStringValue(value);
		}
	}
}
