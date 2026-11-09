namespace MusicRUs.Models;

public sealed class CreateForumPostRequest
{
	public int UserId { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Content { get; set; } = string.Empty;

	public int? TrackId { get; set; }

	public int? AlbumId { get; set; }

	public int? ArtistId { get; set; }

	public List<int> GenreIds { get; set; } = [];
}
