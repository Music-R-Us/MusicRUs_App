namespace MusicRUs;

public static class DisplayTime
{
	private static readonly TimeZoneInfo AppTimeZone = ResolveAppTimeZone();

	public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, AppTimeZone);

	// Formats a timestamp in the application's Copenhagen time zone.
	public static string Format(DateTimeOffset dateTime, string format = "g")
	{
		return TimeZoneInfo.ConvertTime(dateTime, AppTimeZone).ToString(format);
	}

	// Tries the Copenhagen time zone IDs used by IANA and Windows, then the system zone.
	private static TimeZoneInfo ResolveAppTimeZone()
	{
		foreach (var timeZoneId in new[] { "Europe/Copenhagen", "Romance Standard Time" })
		{
			try
			{
				return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
			}
			catch (TimeZoneNotFoundException)
			{
			}
			catch (InvalidTimeZoneException)
			{
			}
		}

		return TimeZoneInfo.Local;
	}
}
