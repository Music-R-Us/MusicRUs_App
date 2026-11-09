namespace MusicRUs.Models;

public sealed class MusicAlbum
{
	public List<MusicTrack> Tracks { get; set; } = [];

	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	[System.Text.Json.Serialization.JsonConverter(typeof(AlbumTypeJsonConverter))]
	public string AlbumType { get; set; } = string.Empty;

	public string ReleaseDate { get; set; } = string.Empty;

	public int? TotalTracks { get; set; }

	public string ImageUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;

	public List<AlbumArtistInfo> Artists { get; set; } = [];

	// Convenience helpers populated from Artists list
	public int ArtistId => Artists.Select(artist => artist.ArtistId > 0 ? artist.ArtistId : artist.Id)
		.FirstOrDefault(id => id > 0);

	public string ArtistName => string.Join(", ", Artists.Select(a => string.IsNullOrWhiteSpace(a.ArtistName) ? a.Name : a.ArtistName).Where(n => !string.IsNullOrWhiteSpace(n)));
}
