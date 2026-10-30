namespace MusicRUs.Models;

public sealed class PostItem
{
	public Guid Id { get; set; } = Guid.NewGuid();

	public int PostId { get; set; }

	public int UserId { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string Artist { get; set; } = string.Empty;

	public string Album { get; set; } = string.Empty;

	public string Song { get; set; } = string.Empty;

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
	public int ArtistId { get; set; }

	public string Artist { get; set; } = string.Empty;

	public int AlbumId { get; set; }

	public string Album { get; set; } = string.Empty;
}

public sealed class PostDraft
{
	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public string Genre { get; set; } = string.Empty;

	public string TagsText { get; set; } = string.Empty;

	public bool RequiresMusicSelection { get; set; } = true;

	public MusicSelection Selection { get; set; } = new();
}
