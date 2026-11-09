namespace MusicRUs.Models;

public sealed class MusicSelection
{
	public string AlbumImageUrl { get; set; } = string.Empty;

	public int TrackId { get; set; }

	public string Track { get; set; } = string.Empty;

	public int ArtistId { get; set; }

	public string Artist { get; set; } = string.Empty;

	public int AlbumId { get; set; }

	public string Album { get; set; } = string.Empty;
}
