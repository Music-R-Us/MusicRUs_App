namespace MusicRUs.Models;

public sealed class MusicArtist
{
	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string ImageUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;
}
