namespace MusicRUs.Models;

public sealed class Concert
{
    public int Id { get; set; }
    public int ArtistId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public string VenueTimeZone { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
