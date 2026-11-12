namespace MusicRUs.Models;

public sealed class ArtistSubscription
{
    public int UserId { get; set; }
    public int ArtistId { get; set; }
    public MusicArtist? Artist { get; set; }

    public string CountryCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? City { get; set; }

    public bool NotifyEmail { get; set; }
    public bool NotifyFeed { get; set; }
}