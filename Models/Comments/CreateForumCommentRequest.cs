namespace MusicRUs.Models;

public sealed class CreateForumCommentRequest
{
	public int PostId { get; set; }

	public int UserId { get; set; }

	public string Content { get; set; } = string.Empty;
}
