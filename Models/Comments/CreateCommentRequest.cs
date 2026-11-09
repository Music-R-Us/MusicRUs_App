namespace MusicRUs.Models;

public sealed class CreateCommentRequest
{
	public string Author { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;
}
