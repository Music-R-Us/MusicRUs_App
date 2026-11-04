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

public sealed class CommentItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public int CommentId { get; set; }

	public int PostId { get; set; }

	public int UserId { get; set; }

	public string Author { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public string Comment { get; set; } = string.Empty;

	public DateTimeOffset CreatedAtUtc { get; set; } = DisplayTime.Now;

	public string DisplayBody => string.IsNullOrWhiteSpace(Body) ? Comment : Body;
}

public sealed class MusicSelection
{
	public int TrackId { get; set; }

	public string Track { get; set; } = string.Empty;

	public int ArtistId { get; set; }

	public string Artist { get; set; } = string.Empty;

	public int AlbumId { get; set; }

	public string Album { get; set; } = string.Empty;
}

public sealed class PostDraft
{
	public MusicLinkType MusicType { get; set; } = MusicLinkType.Album;

	public List<int> GenreIds { get; set; } = [];

	[System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter a post title.")]
	[System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "The title must be at least 8 characters long.")]
	public string Title { get; set; } = string.Empty;

	[System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Enter post content.")]
	[System.ComponentModel.DataAnnotations.MinLength(8, ErrorMessage = "The post content must be at least 8 characters long.")]
	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string TagsText { get; set; } = string.Empty;

	public bool RequiresMusicSelection { get; set; } = true;

	public MusicSelection Selection { get; set; } = new();
}

public enum MusicLinkType { None, Artist, Album, Track }
