namespace MusicRUs.Models;

public sealed class AlbumArtistInfo
{
	public int Id { get; set; }

	public string Name { get; set; } = string.Empty;

	public int ArtistId { get; set; }

	public string ArtistName { get; set; } = string.Empty;
}
