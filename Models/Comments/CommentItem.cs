namespace MusicRUs.Models;

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
