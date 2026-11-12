namespace MusicRUs.Models;

public sealed class SubscribeArtistRequest
{
    public string CountryCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? City { get; set; }
}