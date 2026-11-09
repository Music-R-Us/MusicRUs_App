namespace MusicRUs.Models;

public sealed class PostItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public int PostId { get; set; }

	public int UserId { get; set; }

	public int? TrackId { get; set; }

	public int? AlbumId { get; set; }

	public int? ArtistId { get; set; }

	public List<GenreOption> Genres { get; set; } = [];

	public DateTimeOffset? UpdatedAtUtc { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string Artist { get; set; } = string.Empty;

	public string ArtistImageUrl { get; set; } = string.Empty;

	public string Album { get; set; } = string.Empty;

	public string AlbumImageUrl { get; set; } = string.Empty;

	public string Song { get; set; } = string.Empty;

	public int? TrackAlbumId { get; set; }

	public string TrackImageUrl { get; set; } = string.Empty;

	public string TrackArtistName { get; set; } = string.Empty;

	public List<string> Tags { get; set; } = [];

	public string Author { get; set; } = string.Empty;

	public DateTimeOffset CreatedAtUtc { get; set; } = DisplayTime.Now;

	public int CommentCount { get; set; }

	public List<CommentItem> Comments { get; set; } = [];

	public string RouteId => PostId > 0 ? PostId.ToString() : Id.ToString();
}
