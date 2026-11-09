namespace MusicRUs.Models;

public sealed class MusicTrack
{
	public MusicAlbum? Album { get; set; }

	public List<MusicArtist> Artists { get; set; } = [];

	public int Id { get; set; }

	public string SpotifyId { get; set; } = string.Empty;

	public string Title { get; set; } = string.Empty;

	public int? AlbumId { get; set; }

	public int? TrackNumber { get; set; }

	public int? DiscNumber { get; set; }

	public int? DurationMs { get; set; }

	public bool? Explicit { get; set; }

	public string PreviewUrl { get; set; } = string.Empty;

	public string SpotifyUrl { get; set; } = string.Empty;

	public string DurationFormatted => DurationMs is > 0
		? $"{DurationMs.Value / 60000}:{(DurationMs.Value % 60000) / 1000:D2}"
		: string.Empty;
}
