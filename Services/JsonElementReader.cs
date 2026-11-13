using System.Text.Json;

namespace MusicRUs.Services;

internal static class JsonElementReader
{
	/// <summary>
	/// Returns the first string found under one of the supplied JSON property names.
	/// </summary>
	public static string? GetString(JsonElement element, params string[] propertyNames)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		foreach (var propertyName in propertyNames)
		{
			if (TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String)
			{
				return value.GetString();
			}
		}

		return null;
	}

	/// <summary>
	/// Accepts integer values encoded as either JSON numbers or strings.
	/// </summary>
	public static int? GetInt(JsonElement element, params string[] propertyNames)
	{
		if (element.ValueKind != JsonValueKind.Object)
		{
			return null;
		}

		foreach (var propertyName in propertyNames)
		{
			if (!TryGetProperty(element, propertyName, out var value))
			{
				continue;
			}

			if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
			{
				return intValue;
			}

			if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
			{
				return intValue;
			}
		}

		return null;
	}

	/// <summary>
	/// Tries an exact property match before accepting different capitalization.
	/// </summary>
	public static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
	{
		if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
		{
			return true;
		}

		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in element.EnumerateObject())
			{
				if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
				{
					value = property.Value;
					return true;
				}
			}
		}

		value = default;
		return false;
	}
}
